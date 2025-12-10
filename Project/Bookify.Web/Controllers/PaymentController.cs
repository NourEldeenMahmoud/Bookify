using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace Bookify.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/payments")]
[IgnoreAntiforgeryToken]
public class PaymentController : Controller
{
    private readonly ILogger<PaymentController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IEmailService _emailService;

    public PaymentController(
        ILogger<PaymentController> logger,
        IUnitOfWork unitOfWork,
        IPaymentService paymentService,
        IEmailService emailService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    [HttpPost("create-payment-intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
    {
        try
        {
            // Log ModelState errors if any
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                    .ToList();
                
                _logger.LogWarning("ModelState validation failed - Errors: {Errors}", string.Join("; ", errors));
                return BadRequest(new { error = "Invalid request data", details = errors });
            }

            _logger.LogInformation("CreatePaymentIntent called - Request: {@Request}", request);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted to create payment intent");
                return Unauthorized(new { error = "User not authenticated" });
            }

            if (request == null)
            {
                _logger.LogWarning("Payment intent request is null");
                return BadRequest(new { error = "Request body is required" });
            }

            List<CartItemDto> cartItemDtos;

            // Check if this is a cart request
            if (request.CartItems != null && request.CartItems.Any())
            {
                _logger.LogInformation("Creating payment intent for cart - ItemCount: {ItemCount}, Amount: {Amount}", 
                    request.CartItems.Count, request.Amount);

                if (request.Amount <= 0)
                {
                    _logger.LogWarning("Invalid Amount: {Amount}", request.Amount);
                    return BadRequest(new { error = $"Amount must be greater than zero. Received: {request.Amount}" });
                }

                // Convert CartItemViewModel to CartItemDto
                cartItemDtos = request.CartItems.Select(item => new CartItemDto
                {
                    RoomId = item.RoomId,
                    RoomNumber = item.RoomNumber,
                    RoomTypeName = item.RoomTypeName,
                    PricePerNight = item.PricePerNight,
                    MaxOccupancy = item.MaxOccupancy,
                    ImageUrl = item.ImageUrl,
                    Description = item.Description,
                    CheckIn = DateTime.Parse(item.CheckIn),
                    CheckOut = DateTime.Parse(item.CheckOut),
                    NumberOfGuests = item.NumberOfGuests
                }).ToList();
            }
            else
            {
                // Convert single room request to cart format
                _logger.LogInformation("Converting single room request to cart format - RoomId: {RoomId}, Amount: {Amount}, CheckIn: {CheckIn}, CheckOut: {CheckOut}, Guests: {Guests}", 
                request.RoomId, request.Amount, request.CheckIn, request.CheckOut, request.NumberOfGuests);

            // Parse dates from strings
            if (string.IsNullOrEmpty(request.CheckIn))
            {
                _logger.LogWarning("CheckIn is null or empty");
                return BadRequest(new { error = "Check-in date is required" });
            }

            if (!DateTime.TryParse(request.CheckIn, out var checkInDate))
            {
                _logger.LogWarning("Invalid CheckIn date format: {CheckIn}", request.CheckIn);
                return BadRequest(new { error = $"Invalid check-in date format: '{request.CheckIn}'. Use YYYY-MM-DD" });
            }

            if (string.IsNullOrEmpty(request.CheckOut))
            {
                _logger.LogWarning("CheckOut is null or empty");
                return BadRequest(new { error = "Check-out date is required" });
            }

            if (!DateTime.TryParse(request.CheckOut, out var checkOutDate))
            {
                _logger.LogWarning("Invalid CheckOut date format: {CheckOut}", request.CheckOut);
                return BadRequest(new { error = $"Invalid check-out date format: '{request.CheckOut}'. Use YYYY-MM-DD" });
            }

            if (request.RoomId <= 0)
            {
                _logger.LogWarning("Invalid RoomId: {RoomId}", request.RoomId);
                return BadRequest(new { error = $"Invalid room ID: {request.RoomId}" });
            }

            if (request.Amount <= 0)
            {
                _logger.LogWarning("Invalid Amount: {Amount}", request.Amount);
                return BadRequest(new { error = $"Amount must be greater than zero. Received: {request.Amount}" });
            }

            if (checkOutDate <= checkInDate)
            {
                _logger.LogWarning("Check-out must be after check-in - CheckIn: {CheckIn}, CheckOut: {CheckOut}", checkInDate, checkOutDate);
                return BadRequest(new { error = "Check-out date must be after check-in date" });
            }

                // Get room details to convert to cart format
                var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(request.RoomId);
                if (room == null || room.RoomType == null)
                {
                    _logger.LogWarning("Room {RoomId} not found", request.RoomId);
                    return BadRequest(new { error = "Room not found" });
                }

                // Get room image
                var roomImageUrl = room.GalleryImages?.FirstOrDefault()?.ImageUrl ?? room.RoomType.ImageUrl;
                if (string.IsNullOrEmpty(roomImageUrl))
                {
                    roomImageUrl = "~/images/G1.jpg";
                }
                else if (!roomImageUrl.StartsWith("http") && !roomImageUrl.StartsWith("/") && !roomImageUrl.StartsWith("~/"))
                {
                    roomImageUrl = $"~/images/{roomImageUrl}";
                }

                // Convert single room to cart item
                cartItemDtos = new List<CartItemDto>
                {
                    new CartItemDto
                    {
                        RoomId = room.Id,
                        RoomNumber = room.RoomNumber,
                        RoomTypeName = room.RoomType.Name ?? string.Empty,
                        PricePerNight = room.RoomType.PricePerNight,
                        MaxOccupancy = room.RoomType.MaxOccupancy,
                        ImageUrl = roomImageUrl,
                        Description = room.RoomType.Description,
                        CheckIn = checkInDate,
                        CheckOut = checkOutDate,
                        NumberOfGuests = request.NumberOfGuests
                    }
                };
            }

            // Use cart method for both single room and multiple rooms
            var cartClientSecret = await _paymentService.CreatePaymentIntentForCartAsync(
                request.Amount,
                request.Currency ?? "usd",
                userId,
                cartItemDtos
            );

            _logger.LogInformation("Payment intent created successfully - ClientSecret received");
            return Ok(new { clientSecret = cartClientSecret });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error creating payment intent: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error creating payment intent: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment intent: {Message}", ex.Message);
            return StatusCode(500, new { error = "An error occurred while creating payment intent" });
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted to confirm payment");
                return Unauthorized(new { error = "User not authenticated" });
            }

            if (request == null || string.IsNullOrEmpty(request.PaymentIntentId))
            {
                _logger.LogWarning("Invalid confirm payment request");
                return BadRequest(new { error = "Invalid request parameters" });
            }

            List<CartItemDto> cartItemDtos;

            // Check if this is a cart request
            if (request.CartItems != null && request.CartItems.Any())
            {
                _logger.LogInformation("Confirming payment for cart - UserId: {UserId}, PaymentIntentId: {PaymentIntentId}, ItemCount: {ItemCount}", 
                    userId, request.PaymentIntentId, request.CartItems.Count);

                // Convert CartItemViewModel to CartItemDto
                cartItemDtos = request.CartItems.Select(item => new CartItemDto
                {
                    RoomId = item.RoomId,
                    RoomNumber = item.RoomNumber,
                    RoomTypeName = item.RoomTypeName,
                    PricePerNight = item.PricePerNight,
                    MaxOccupancy = item.MaxOccupancy,
                    ImageUrl = item.ImageUrl,
                    Description = item.Description,
                    CheckIn = DateTime.Parse(item.CheckIn),
                    CheckOut = DateTime.Parse(item.CheckOut),
                    NumberOfGuests = item.NumberOfGuests
                }).ToList();
            }
            else
            {
                // Convert single room request to cart format
                _logger.LogInformation("Converting single room request to cart format - RoomId: {RoomId}, PaymentIntentId: {PaymentIntentId}", 
                    request.RoomId, request.PaymentIntentId);

                // Parse dates from strings
                if (!DateTime.TryParse(request.CheckIn, out var checkInDate))
                {
                    _logger.LogWarning("Invalid CheckIn date format: {CheckIn}", request.CheckIn);
                    return BadRequest(new { error = "Invalid check-in date format. Use YYYY-MM-DD" });
                }

                if (!DateTime.TryParse(request.CheckOut, out var checkOutDate))
                {
                    _logger.LogWarning("Invalid CheckOut date format: {CheckOut}", request.CheckOut);
                    return BadRequest(new { error = "Invalid check-out date format. Use YYYY-MM-DD" });
                }

                if (request.RoomId <= 0)
                {
                    _logger.LogWarning("Invalid RoomId: {RoomId}", request.RoomId);
                    return BadRequest(new { error = "Invalid room ID" });
                }

                // Get room details to convert to cart format
                var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(request.RoomId);
                if (room == null || room.RoomType == null)
                {
                    _logger.LogWarning("Room {RoomId} not found", request.RoomId);
                    return BadRequest(new { error = "Room not found" });
                }

                // Get room image
                var roomImageUrl = room.GalleryImages?.FirstOrDefault()?.ImageUrl ?? room.RoomType.ImageUrl;
                if (string.IsNullOrEmpty(roomImageUrl))
                {
                    roomImageUrl = "~/images/G1.jpg";
                }
                else if (!roomImageUrl.StartsWith("http") && !roomImageUrl.StartsWith("/") && !roomImageUrl.StartsWith("~/"))
                {
                    roomImageUrl = $"~/images/{roomImageUrl}";
                }

                // Convert single room to cart item
                cartItemDtos = new List<CartItemDto>
                {
                    new CartItemDto
                    {
                        RoomId = room.Id,
                        RoomNumber = room.RoomNumber,
                        RoomTypeName = room.RoomType.Name ?? string.Empty,
                        PricePerNight = room.RoomType.PricePerNight,
                        MaxOccupancy = room.RoomType.MaxOccupancy,
                        ImageUrl = roomImageUrl,
                        Description = room.RoomType.Description,
                        CheckIn = checkInDate,
                        CheckOut = checkOutDate,
                        NumberOfGuests = request.NumberOfGuests
                    }
                };
            }

            // Use cart method for both single room and multiple rooms
                var cartSuccess = await _paymentService.ConfirmPaymentAndCreateBookingsAsync(
                    request.PaymentIntentId,
                    userId,
                    cartItemDtos,
                    request.SpecialRequests
                );

                if (!cartSuccess)
                {
                    return BadRequest(new { error = "Payment confirmation failed or already processed" });
                }

                // Get all booking IDs directly from BookingPayments table (more reliable)
                var allPayments = (await _unitOfWork.BookingPayments.GetAllAsync())
                    .Where(p => p.PaymentIntentId == request.PaymentIntentId)
                    .ToList();

                if (!allPayments.Any())
                {
                    _logger.LogWarning("No payments found for PaymentIntentId: {PaymentIntentId}", request.PaymentIntentId);
                    return StatusCode(500, new { error = "Payment confirmed but bookings not found" });
                }

                var bookingIds = allPayments.Select(p => p.BookingId).Distinct().ToList();

                _logger.LogInformation("Found {Count} bookings for PaymentIntentId: {PaymentIntentId}", bookingIds.Count, request.PaymentIntentId);

            // Send confirmation emails for all bookings
            try
            {
                foreach (var bookingId in bookingIds)
            {
                    var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId);
                if (booking?.User != null && !string.IsNullOrEmpty(booking.User.Email))
                {
                    await _emailService.SendPaymentConfirmationAsync(
                        booking.User.Email,
                        $"{booking.User.FirstName} {booking.User.LastName}",
                        booking.Id,
                        booking.TotalAmount
                    );

                    await _emailService.SendBookingConfirmationAsync(
                        booking.User.Email,
                        booking.Id,
                        $"{booking.User.FirstName} {booking.User.LastName}",
                        booking.CheckInDate,
                        booking.CheckOutDate,
                        booking.TotalAmount
                    );

                    _logger.LogInformation("Confirmation emails sent for booking {BookingId}", booking.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending confirmation emails for PaymentIntentId: {PaymentIntentId}", request.PaymentIntentId);
                // Don't fail the whole request if email fails
            }

            // Clear cart after successful payment
            try
            {
                HttpContext.Session.Remove("Cart");
                _logger.LogInformation("Cart cleared after successful payment - PaymentIntentId: {PaymentIntentId}", request.PaymentIntentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear cart after payment - PaymentIntentId: {PaymentIntentId}", request.PaymentIntentId);
                // Don't fail the payment if cart clearing fails
            }

            return Ok(new { 
                bookingIds = bookingIds, 
                firstBookingId = bookingIds.First(),
                paymentIntentId = request.PaymentIntentId,
                success = true 
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error confirming payment");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error confirming payment");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment");
            return StatusCode(500, new { error = "An error occurred while confirming payment" });
        }
    }
}

public class CreatePaymentIntentRequest
{
    // Legacy single room checkout
    public int RoomId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string CheckIn { get; set; } = string.Empty;
    public string CheckOut { get; set; } = string.Empty;
    public int NumberOfGuests { get; set; }
    
    // Cart checkout
    public List<CartItemRequest>? CartItems { get; set; }
}

public class ConfirmPaymentRequest
{
    public string PaymentIntentId { get; set; } = string.Empty;
    
    // Legacy single room checkout
    public int RoomId { get; set; }
    public string CheckIn { get; set; } = string.Empty;
    public string CheckOut { get; set; } = string.Empty;
    public int NumberOfGuests { get; set; }
    
    // Cart checkout
    public List<CartItemRequest>? CartItems { get; set; }
    
    public string? SpecialRequests { get; set; }
}

public class CartItemRequest
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int MaxOccupancy { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string CheckIn { get; set; } = string.Empty;
    public string CheckOut { get; set; } = string.Empty;
    public int NumberOfGuests { get; set; }
}

