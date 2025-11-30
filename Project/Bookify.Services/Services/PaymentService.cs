using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Stripe;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Services.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly Serilog.ILogger _logger;

    public PaymentService(IUnitOfWork unitOfWork, IConfiguration configuration, IEmailService emailService, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
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

    public async Task<string> CreatePaymentIntentAsync(decimal amount, string currency, string userId, int roomId, DateTime checkIn, DateTime checkOut, int numberOfGuests)
    {
        try
        {
            _logger.Information("Creating payment intent - Amount: {Amount}, Currency: {Currency}, UserId: {UserId}, RoomId: {RoomId}", 
                amount, currency, userId, roomId);

            if (amount <= 0)
            {
                _logger.Warning("Invalid amount provided: {Amount}", amount);
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));
            }

            var apiKey = _configuration["Stripe:SecretKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.Error("Stripe API key is not configured");
                throw new InvalidOperationException("Stripe API key is not configured");
            }

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Convert to cents
                Currency = currency.ToLower(),
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId },
                    { "roomId", roomId.ToString() },
                    { "checkIn", checkIn.ToString("yyyy-MM-dd") },
                    { "checkOut", checkOut.ToString("yyyy-MM-dd") },
                    { "numberOfGuests", numberOfGuests.ToString() }
                }
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);

            _logger.Information("Successfully created payment intent {PaymentIntentId} for RoomId: {RoomId}", paymentIntent.Id, roomId);
            return paymentIntent.ClientSecret ?? throw new InvalidOperationException("Payment intent created but client secret is null");
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
            _logger.Error(ex, "Stripe error creating payment intent for RoomId: {RoomId}", roomId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating payment intent for RoomId: {RoomId}", roomId);
            throw;
        }
    }

    public async Task<bool> ConfirmPaymentAndCreateBookingAsync(string paymentIntentId, string userId, int roomId, DateTime checkIn, DateTime checkOut, int numberOfGuests, string? specialRequests = null)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.Information("Confirming payment and creating booking - PaymentIntentId: {PaymentIntentId}, UserId: {UserId}, RoomId: {RoomId}", 
                paymentIntentId, userId, roomId);

            if (string.IsNullOrWhiteSpace(paymentIntentId))
            {
                _logger.Warning("Invalid paymentIntentId provided");
                await _unitOfWork.RollbackTransactionAsync();
                throw new ArgumentException("PaymentIntentId cannot be null or empty", nameof(paymentIntentId));
            }

            // Check if payment intent already processed (idempotency)
            var existingPayment = await _unitOfWork.BookingPayments
                .FirstOrDefaultAsync(p => p.PaymentIntentId == paymentIntentId);

            if (existingPayment != null)
            {
                _logger.Warning("Payment intent {PaymentIntentId} already processed. BookingId: {BookingId}", 
                    paymentIntentId, existingPayment.BookingId);
                await _unitOfWork.RollbackTransactionAsync();
                return false; // Already processed
            }

            // Verify payment intent with Stripe
            var apiKey = _configuration["Stripe:SecretKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.Error("Stripe API key is not configured");
                await _unitOfWork.RollbackTransactionAsync();
                throw new InvalidOperationException("Stripe API key is not configured");
            }

            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);

            if (paymentIntent.Status != "succeeded")
            {
                _logger.Warning("Payment intent {PaymentIntentId} status is {Status}, expected succeeded", 
                    paymentIntentId, paymentIntent.Status);
                await _unitOfWork.RollbackTransactionAsync();
                return false;
            }

            // Verify metadata matches
            if (paymentIntent.Metadata["userId"] != userId || 
                paymentIntent.Metadata["roomId"] != roomId.ToString())
            {
                _logger.Warning("Payment intent metadata mismatch - PaymentIntentId: {PaymentIntentId}", paymentIntentId);
                await _unitOfWork.RollbackTransactionAsync();
                throw new InvalidOperationException("Payment intent metadata does not match booking details");
            }

            // Re-check room availability (race condition protection)
            var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(roomId);
            if (room == null)
            {
                _logger.Warning("Room {RoomId} not found", roomId);
                await _unitOfWork.RollbackTransactionAsync();
                throw new ArgumentException("Room not found", nameof(roomId));
            }

            // Check for overlapping bookings
            var overlappingBookings = await _unitOfWork.Bookings.GetOverlappingBookingsAsync(roomId, checkIn, checkOut);
            if (overlappingBookings.Any())
            {
                _logger.Warning("Room {RoomId} is no longer available for dates {CheckIn} to {CheckOut}", 
                    roomId, checkIn, checkOut);
                await _unitOfWork.RollbackTransactionAsync();
                throw new InvalidOperationException("Room is no longer available for the selected dates.");
            }

            // Calculate total amount (server-side verification)
            var roomType = room.RoomType;
            if (roomType == null)
            {
                _logger.Warning("RoomType not found for Room {RoomId}", roomId);
                await _unitOfWork.RollbackTransactionAsync();
                throw new InvalidOperationException("Room type information not found");
            }
            
            var totalNights = (checkOut - checkIn).Days;
            var pricePerNight = roomType.PricePerNight;
            var subtotal = pricePerNight * totalNights;
            var taxRate = 0.14m; // 14% tax (must match Checkout calculation)
            var taxAmount = subtotal * taxRate;
            var discount = 0m; // Can be calculated based on promotions
            var totalAmount = subtotal + taxAmount - discount;

            _logger.Information("Calculated booking total - Subtotal: {Subtotal}, Tax: {Tax}, Total: {Total}, Nights: {Nights}", 
                subtotal, taxAmount, totalAmount, totalNights);

            // Verify amount matches payment intent
            var paidAmount = paymentIntent.Amount / 100m;
            if (Math.Abs(paidAmount - totalAmount) > 0.01m) // Allow small rounding differences
            {
                _logger.Warning("Amount mismatch - Paid: {PaidAmount}, Expected: {TotalAmount}, Subtotal: {Subtotal}, Tax: {Tax}", 
                    paidAmount, totalAmount, subtotal, taxAmount);
                await _unitOfWork.RollbackTransactionAsync();
                throw new InvalidOperationException("Payment amount does not match booking total.");
            }

            // Create booking
            var booking = new Booking
            {
                UserId = userId,
                RoomId = roomId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                NumberOfGuests = numberOfGuests,
                SpecialRequests = specialRequests,
                TotalAmount = totalAmount,
                Status = BookingStatus.Paid,
                CreatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.Bookings.AddAsync(booking);
            await _unitOfWork.SaveChangesAsync(); // Save to get booking ID

            // Update room availability to false when booking is created (regardless of check-in date)
            // This keeps behavior consistent with ReservationService and ensures the room
            // disappears from available lists and shows as unavailable in the admin grid
            if (room.IsAvailable)
            {
                room.IsAvailable = false;
                _unitOfWork.Rooms.Update(room);
                _logger.Information(
                    "Updated room {RoomId} availability to false due to confirmed booking (CheckIn: {CheckIn}, CheckOut: {CheckOut})",
                    roomId, checkIn, checkOut);
            }

            // Create payment record
            var payment = new BookingPayment
            {
                BookingId = booking.Id,
                PaymentIntentId = paymentIntentId,
                Amount = totalAmount,
                Currency = paymentIntent.Currency,
                PaymentStatus = PaymentStatus.Completed,
                TransactionDate = DateTime.UtcNow
            };

            await _unitOfWork.BookingPayments.AddAsync(payment);

            // Create status history
            var statusHistory = new BookingStatusHistory
            {
                BookingId = booking.Id,
                PreviousStatus = BookingStatus.Pending,
                NewStatus = BookingStatus.Paid,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
                Notes = $"Payment confirmed via Stripe PaymentIntent. PaymentIntentId: {paymentIntentId}"
            };

            await _unitOfWork.BookingStatusHistory.AddAsync(statusHistory);
            await _unitOfWork.CommitTransactionAsync();

            _logger.Information("Successfully confirmed payment and created booking - BookingId: {BookingId}, PaymentIntentId: {PaymentIntentId}", 
                booking.Id, paymentIntentId);

            // Send booking confirmation email (non-blocking - don't fail booking if email fails)
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var userName = !string.IsNullOrWhiteSpace(user.FirstName) 
                        ? $"{user.FirstName} {user.LastName}".Trim() 
                        : user.UserName ?? "Guest";
                    
                    await _emailService.SendBookingConfirmationAsync(
                        user.Email, 
                        booking.Id, 
                        userName, 
                        checkIn, 
                        checkOut, 
                        totalAmount
                    );
                    
                    _logger.Information("Booking confirmation email sent successfully - BookingId: {BookingId}, Email: {Email}", 
                        booking.Id, user.Email);
                }
                else
                {
                    _logger.Warning("User not found or email is empty - BookingId: {BookingId}, UserId: {UserId}", 
                        booking.Id, userId);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the booking - email is non-critical
                _logger.Error(ex, "Failed to send booking confirmation email - BookingId: {BookingId}, UserId: {UserId}. Booking was still created successfully.", 
                    booking.Id, userId);
            }

            return true;
        }
        catch (ArgumentException)
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
        catch (InvalidOperationException)
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
        catch (StripeException ex)
        {
            _logger.Error(ex, "Stripe error confirming payment - PaymentIntentId: {PaymentIntentId}", paymentIntentId);
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error confirming payment and creating booking - PaymentIntentId: {PaymentIntentId}", paymentIntentId);
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}

