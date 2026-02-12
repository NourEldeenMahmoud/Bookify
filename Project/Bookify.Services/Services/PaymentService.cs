using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Services.DTOs;
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

    public async Task<string> CreatePaymentIntentForCartAsync(decimal amount, string currency, string userId, List<CartItemDto> cartItems)
    {
        try
        {
            _logger.Information("Creating payment intent for cart - Amount: {Amount}, Currency: {Currency}, UserId: {UserId}, ItemCount: {ItemCount}", 
                amount, currency, userId, cartItems?.Count ?? 0);

            if (amount <= 0)
            {
                _logger.Warning("Invalid amount provided: {Amount}", amount);
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));
            }

            if (cartItems == null || !cartItems.Any())
            {
                _logger.Warning("Cart items list is null or empty");
                throw new ArgumentException("Cart items cannot be null or empty", nameof(cartItems));
            }

            var apiKey = _configuration["Stripe:SecretKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.Error("Stripe API key is not configured");
                throw new InvalidOperationException("Stripe API key is not configured");
            }

            var metadata = new Dictionary<string, string>
            {
                { "userId", userId },
                { "itemCount", cartItems.Count.ToString() },
                { "isCart", "true" }
            };

            // Add room IDs to metadata (comma-separated)
            var roomIds = string.Join(",", cartItems.Select(item => item.RoomId.ToString()));
            metadata.Add("roomIds", roomIds);

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Convert to cents
                Currency = currency.ToLower(),
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = metadata
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);

            _logger.Information("Successfully created payment intent {PaymentIntentId} for cart with {ItemCount} items", paymentIntent.Id, cartItems.Count);
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
            _logger.Error(ex, "Stripe error creating payment intent for cart");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating payment intent for cart");
            throw;
        }
    }

    public async Task<bool> ConfirmPaymentAndCreateBookingsAsync(string paymentIntentId, string userId, List<CartItemDto> cartItems, string? specialRequests = null)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.Information("Confirming payment and creating bookings for cart - PaymentIntentId: {PaymentIntentId}, UserId: {UserId}, ItemCount: {ItemCount}", 
                paymentIntentId, userId, cartItems?.Count ?? 0);

            if (string.IsNullOrWhiteSpace(paymentIntentId))
            {
                _logger.Warning("Invalid paymentIntentId provided");
                await _unitOfWork.RollbackTransactionAsync();
                throw new ArgumentException("PaymentIntentId cannot be null or empty", nameof(paymentIntentId));
            }

            if (cartItems == null || !cartItems.Any())
            {
                _logger.Warning("Cart items list is null or empty");
                await _unitOfWork.RollbackTransactionAsync();
                throw new ArgumentException("Cart items cannot be null or empty", nameof(cartItems));
            }

            // Check if payment intent already processed (idempotency)
            var existingPayment = await _unitOfWork.BookingPayments.FirstOrDefaultAsync(p => p.PaymentIntentId == paymentIntentId);

            if (existingPayment != null)
            {
                _logger.Warning("Payment intent {PaymentIntentId} already processed. BookingId: {BookingId}", 
                    paymentIntentId, existingPayment.BookingId);
                await _unitOfWork.RollbackTransactionAsync();
                return false; // Already processed
            }

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
                paymentIntent.Metadata["isCart"] != "true")
            {
                _logger.Warning("Payment intent metadata mismatch - PaymentIntentId: {PaymentIntentId}", paymentIntentId);
                await _unitOfWork.RollbackTransactionAsync();
                throw new InvalidOperationException("Payment intent metadata does not match cart details");
            }

            var taxRate = 0.14m; // must match Checkout calculation
            var totalAmount = 0m;
            var createdBookings = new List<Booking>();

            foreach (var cartItem in cartItems)
            {
                //race condition protection
                var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(cartItem.RoomId);
                if (room == null)
                {
                    _logger.Warning("Room {RoomId} not found", cartItem.RoomId);
                    await _unitOfWork.RollbackTransactionAsync();
                    throw new ArgumentException($"Room {cartItem.RoomId} not found", nameof(cartItem.RoomId));
                }

                // Check for overlapping bookings
                var overlappingBookings = await _unitOfWork.Bookings.GetOverlappingBookingsAsync(cartItem.RoomId, cartItem.CheckIn, cartItem.CheckOut);
                if (overlappingBookings.Any())
                {
                    _logger.Warning("Room {RoomId} is no longer available for dates {CheckIn} to {CheckOut}", cartItem.RoomId, cartItem.CheckIn, cartItem.CheckOut);
                    await _unitOfWork.RollbackTransactionAsync();
                    throw new InvalidOperationException($"Room {cartItem.RoomNumber} is no longer available for the selected dates.");
                }

                var subtotal = cartItem.Subtotal;
                var taxAmount = subtotal * taxRate;
                var bookingTotal = subtotal + taxAmount;
                totalAmount += bookingTotal;

                var booking = new Booking
                {
                    UserId = userId,
                    RoomId = cartItem.RoomId,
                    CheckInDate = cartItem.CheckIn,
                    CheckOutDate = cartItem.CheckOut,
                    NumberOfGuests = cartItem.NumberOfGuests,
                    SpecialRequests = specialRequests,
                    TotalAmount = bookingTotal,
                    Status = BookingStatus.Paid,
                    CreatedAt = DateTime.UtcNow,
                };

                await _unitOfWork.Bookings.AddAsync(booking);
                await _unitOfWork.SaveChangesAsync(); 

                createdBookings.Add(booking);

                // Update room availability
                if (room.IsAvailable)
                {
                    room.IsAvailable = false;
                    _unitOfWork.Rooms.Update(room);
                    _logger.Information(
                        "Updated room {RoomId} availability to false due to confirmed booking (CheckIn: {CheckIn}, CheckOut: {CheckOut})",
                        cartItem.RoomId, cartItem.CheckIn, cartItem.CheckOut);
                }

                var statusHistory = new BookingStatusHistory
                {
                    BookingId = booking.Id,
                    PreviousStatus = BookingStatus.Pending,
                    NewStatus = BookingStatus.Paid,
                    ChangedByUserId = userId,
                    ChangedAt = DateTime.UtcNow,
                    Notes = $"Payment confirmed via Stripe PaymentIntent (Cart). PaymentIntentId: {paymentIntentId}"
                };

                await _unitOfWork.BookingStatusHistory.AddAsync(statusHistory);
            }

            // Verify total amount matches payment intent
            var paidAmount = paymentIntent.Amount / 100m;
            if (Math.Abs(paidAmount - totalAmount) > 0.01m) // Allow small rounding differences
            {
                _logger.Warning("Amount mismatch - Paid: {PaidAmount}, Expected: {TotalAmount}", 
                    paidAmount, totalAmount);
                await _unitOfWork.RollbackTransactionAsync();
                throw new InvalidOperationException("Payment amount does not match cart total.");
            }

            // Create payment record for each booking (each booking has its own payment record)
            // All payment records share the same PaymentIntentId to link them together
            foreach (var booking in createdBookings)
            {
                var payment = new BookingPayment
                {
                    BookingId = booking.Id,
                    PaymentIntentId = paymentIntentId,
                    Amount = booking.TotalAmount, // Individual booking amount
                    Currency = paymentIntent.Currency,
                    PaymentStatus = PaymentStatus.Completed,
                    TransactionDate = DateTime.UtcNow
                };

                await _unitOfWork.BookingPayments.AddAsync(payment);
            }

            await _unitOfWork.CommitTransactionAsync();

            _logger.Information("Successfully confirmed payment and created {BookingCount} bookings - PaymentIntentId: {PaymentIntentId}, TotalAmount: {TotalAmount}", 
                createdBookings.Count, paymentIntentId, totalAmount);

            // Send booking confirmation emails for all bookings (non-blocking)
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var userName = !string.IsNullOrWhiteSpace(user.FirstName) 
                        ? $"{user.FirstName} {user.LastName}".Trim() 
                        : user.UserName ?? "Guest";
                    
                    // Send email for each booking
                    foreach (var booking in createdBookings)
                    {
                        try
                        {
                            await _emailService.SendBookingConfirmationAsync(
                                user.Email, 
                                booking.Id, 
                                userName, 
                                booking.CheckInDate, 
                                booking.CheckOutDate, 
                                booking.TotalAmount
                            );
                            
                            _logger.Information("Booking confirmation email sent successfully - BookingId: {BookingId}, Email: {Email}", 
                                booking.Id, user.Email);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Failed to send booking confirmation email for BookingId: {BookingId}", booking.Id);
                        }
                    }
                }
                else
                {
                    _logger.Warning("User not found or email is empty - UserId: {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the bookings - email is non-critical
                _logger.Error(ex, "Failed to send booking confirmation emails. Bookings were still created successfully.");
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
            _logger.Error(ex, "Stripe error confirming payment for cart - PaymentIntentId: {PaymentIntentId}", paymentIntentId);
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error confirming payment and creating bookings for cart - PaymentIntentId: {PaymentIntentId}", paymentIntentId);
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}

