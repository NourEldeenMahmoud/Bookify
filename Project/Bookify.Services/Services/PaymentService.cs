using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Stripe;
using Stripe.Checkout;

namespace Bookify.Services.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly Serilog.ILogger _logger;

    public PaymentService(IUnitOfWork unitOfWork, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = Log.ForContext<PaymentService>();

        var apiKey = _configuration["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.Warning("Stripe API key is not configured");
        }
        else
        {
            StripeConfiguration.ApiKey = apiKey;
        }
    }

    public async Task<string> CreateStripeCheckoutSessionAsync(int bookingId, string successUrl, string cancelUrl)
    {
        try
        {
            _logger.Information("Creating Stripe checkout session for BookingId: {BookingId}", bookingId);

            if (bookingId <= 0)
            {
                _logger.Warning("Invalid bookingId provided: {BookingId}", bookingId);
                throw new ArgumentException("BookingId must be greater than zero", nameof(bookingId));
            }

            if (string.IsNullOrWhiteSpace(successUrl))
            {
                _logger.Warning("Invalid successUrl provided");
                throw new ArgumentException("SuccessUrl cannot be null or empty", nameof(successUrl));
            }

            if (string.IsNullOrWhiteSpace(cancelUrl))
            {
                _logger.Warning("Invalid cancelUrl provided");
                throw new ArgumentException("CancelUrl cannot be null or empty", nameof(cancelUrl));
            }

            var apiKey = _configuration["Stripe:SecretKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.Error("Stripe API key is not configured");
                throw new InvalidOperationException("Stripe API key is not configured");
            }

            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId);
            if (booking == null)
            {
                _logger.Warning("Booking with ID {BookingId} not found", bookingId);
                throw new ArgumentException("Booking not found.", nameof(bookingId));
            }

            if (booking.Status != BookingStatus.Pending)
            {
                _logger.Warning("Booking {BookingId} is not in pending status. Current status: {Status}", bookingId, booking.Status);
                throw new InvalidOperationException("Booking is not in pending status.");
            }

            if (booking.Room == null)
            {
                _logger.Warning("Room information not found for booking {BookingId}", bookingId);
                throw new InvalidOperationException("Room information not found for booking.");
            }

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(booking.TotalAmount * 100), // Convert to cents
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Room Booking - {booking.Room.RoomNumber}",
                                Description = $"Check-in: {booking.CheckInDate:yyyy-MM-dd}, Check-out: {booking.CheckOutDate:yyyy-MM-dd}"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                ClientReferenceId = bookingId.ToString(),
                Metadata = new Dictionary<string, string>
                {
                    { "bookingId", bookingId.ToString() },
                    { "userId", booking.UserId }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            _logger.Information("Successfully created Stripe checkout session {SessionId} for BookingId: {BookingId}", session.Id, bookingId);
            return session.Url;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (StripeException ex)
        {
            _logger.Error(ex, "Stripe error creating checkout session for BookingId: {BookingId}", bookingId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating Stripe checkout session for BookingId: {BookingId}", bookingId);
            throw;
        }
    }

    public async Task<bool> ProcessStripeWebhookAsync(string json, string signature)
    {
        try
        {
            _logger.Information("Processing Stripe webhook");

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.Warning("Invalid webhook JSON provided");
                throw new ArgumentException("Webhook JSON cannot be null or empty", nameof(json));
            }

            if (string.IsNullOrWhiteSpace(signature))
            {
                _logger.Warning("Invalid webhook signature provided");
                throw new ArgumentException("Webhook signature cannot be null or empty", nameof(signature));
            }

            var webhookSecret = _configuration["Stripe:WebhookSecret"];
            if (string.IsNullOrEmpty(webhookSecret))
            {
                _logger.Error("Stripe webhook secret is not configured");
                return false;
            }

            var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
            _logger.Debug("Stripe webhook event type: {EventType}, ID: {EventId}", stripeEvent.Type, stripeEvent.Id);

            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Session;
                if (session != null && int.TryParse(session.ClientReferenceId, out int bookingId))
                {
                    _logger.Information("Processing checkout.session.completed for BookingId: {BookingId}", bookingId);
                    await ProcessPaymentSuccessAsync(bookingId, session);
                }
                else
                {
                    _logger.Warning("Invalid session data in checkout.session.completed event");
                }
            }
            else if (stripeEvent.Type == "payment_intent.succeeded")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                if (paymentIntent != null)
                {
                    _logger.Information("Payment intent succeeded: {PaymentIntentId}", paymentIntent.Id);
                }
            }
            else
            {
                _logger.Debug("Unhandled webhook event type: {EventType}", stripeEvent.Type);
            }

            return true;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (StripeException ex)
        {
            _logger.Error(ex, "Stripe webhook processing failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error processing Stripe webhook");
            return false;
        }
    }

    private async Task ProcessPaymentSuccessAsync(int bookingId, Stripe.Checkout.Session session)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.Information("Processing payment success for BookingId: {BookingId}, SessionId: {SessionId}", bookingId, session.Id);

            if (bookingId <= 0)
            {
                _logger.Warning("Invalid bookingId provided: {BookingId}", bookingId);
                await _unitOfWork.RollbackTransactionAsync();
                throw new ArgumentException("BookingId must be greater than zero", nameof(bookingId));
            }

            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId);
            if (booking == null)
            {
                _logger.Warning("Booking {BookingId} not found for payment processing", bookingId);
                await _unitOfWork.RollbackTransactionAsync();
                return;
            }

            // Update booking status
            var previousStatus = booking.Status;
            booking.Status = BookingStatus.Paid;
            booking.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Bookings.Update(booking);

            // Create payment record
            var payment = new BookingPayment
            {
                BookingId = bookingId,
                StripeSessionId = session.Id,
                PaymentIntentId = session.PaymentIntentId,
                Amount = booking.TotalAmount,
                Currency = session.Currency ?? "usd",
                PaymentStatus = PaymentStatus.Completed,
                TransactionDate = DateTime.UtcNow
            };

            await _unitOfWork.BookingPayments.AddAsync(payment);

            // Create status history
            var statusHistory = new BookingStatusHistory
            {
                BookingId = bookingId,
                PreviousStatus = previousStatus,
                NewStatus = BookingStatus.Paid,
                ChangedByUserId = booking.UserId,
                ChangedAt = DateTime.UtcNow,
                Notes = $"Payment processed via Stripe. Session ID: {session.Id}"
            };

            await _unitOfWork.BookingStatusHistory.AddAsync(statusHistory);
            await _unitOfWork.CommitTransactionAsync();

            _logger.Information("Payment processed successfully for booking {BookingId}", bookingId);
        }
        catch (ArgumentException)
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing payment for booking {BookingId}", bookingId);
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> RefundPaymentAsync(int bookingId)
    {
        try
        {
            _logger.Information("Processing refund for BookingId: {BookingId}", bookingId);

            if (bookingId <= 0)
            {
                _logger.Warning("Invalid bookingId provided: {BookingId}", bookingId);
                throw new ArgumentException("BookingId must be greater than zero", nameof(bookingId));
            }

            var apiKey = _configuration["Stripe:SecretKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.Error("Stripe API key is not configured");
                throw new InvalidOperationException("Stripe API key is not configured");
            }

            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId);
            if (booking == null)
            {
                _logger.Warning("Booking with ID {BookingId} not found", bookingId);
                return false;
            }

            if (booking.Status != BookingStatus.Paid)
            {
                _logger.Warning("Booking {BookingId} is not in Paid status. Current status: {Status}", bookingId, booking.Status);
                return false;
            }

            var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == PaymentStatus.Completed);
            if (payment == null)
            {
                _logger.Warning("No successful payment found for booking {BookingId}", bookingId);
                return false;
            }

            if (string.IsNullOrEmpty(payment.PaymentIntentId))
            {
                _logger.Warning("Payment intent ID is missing for booking {BookingId}", bookingId);
                return false;
            }

            var refundService = new RefundService();
            var refundOptions = new RefundCreateOptions
            {
                PaymentIntent = payment.PaymentIntentId,
                Amount = (long)(payment.Amount * 100) // Convert to cents
            };

            var refund = await refundService.CreateAsync(refundOptions);
            _logger.Debug("Refund created with status: {Status}, ID: {RefundId}", refund.Status, refund.Id);

            if (refund.Status == "succeeded")
            {
                // Begin transaction for atomic operation
                await _unitOfWork.BeginTransactionAsync();
                
                try
                {
                    // Update booking status to cancelled
                    booking.Status = BookingStatus.Cancelled;
                    booking.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Bookings.Update(booking);

                    var statusHistory = new BookingStatusHistory
                    {
                        BookingId = bookingId,
                        PreviousStatus = BookingStatus.Paid,
                        NewStatus = BookingStatus.Cancelled,
                        ChangedByUserId = booking.UserId,
                        ChangedAt = DateTime.UtcNow,
                        Notes = $"Refund processed. Refund ID: {refund.Id}"
                    };

                    await _unitOfWork.BookingStatusHistory.AddAsync(statusHistory);
                    await _unitOfWork.CommitTransactionAsync();

                    _logger.Information("Successfully processed refund for booking {BookingId}, RefundId: {RefundId}", bookingId, refund.Id);
                    return true;
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }

            _logger.Warning("Refund failed for booking {BookingId}. Refund status: {Status}", bookingId, refund.Status);
            return false;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (StripeException ex)
        {
            _logger.Error(ex, "Stripe error processing refund for booking {BookingId}", bookingId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing refund for booking {BookingId}", bookingId);
            return false;
        }
    }
}

