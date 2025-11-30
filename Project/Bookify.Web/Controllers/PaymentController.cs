using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            _logger.LogInformation("Request received - RoomId: {RoomId}, Amount: {Amount}, CheckIn: {CheckIn}, CheckOut: {CheckOut}, Guests: {Guests}", 
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

            _logger.LogInformation("Creating payment intent - UserId: {UserId}, RoomId: {RoomId}, Amount: {Amount}, CheckIn: {CheckIn}, CheckOut: {CheckOut}", 
                userId, request.RoomId, request.Amount, checkInDate, checkOutDate);

            var clientSecret = await _paymentService.CreatePaymentIntentAsync(
                request.Amount,
                request.Currency ?? "usd",
                userId,
                request.RoomId,
                checkInDate,
                checkOutDate,
                request.NumberOfGuests
            );

            _logger.LogInformation("Payment intent created successfully - ClientSecret received");
            return Ok(new { clientSecret });
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

            _logger.LogInformation("Confirming payment - UserId: {UserId}, PaymentIntentId: {PaymentIntentId}", 
                userId, request.PaymentIntentId);

            var success = await _paymentService.ConfirmPaymentAndCreateBookingAsync(
                request.PaymentIntentId,
                userId,
                request.RoomId,
                checkInDate,
                checkOutDate,
                request.NumberOfGuests,
                request.SpecialRequests
            );

            if (!success)
            {
                return BadRequest(new { error = "Payment confirmation failed or already processed" });
            }

            // Get the booking ID from the payment
            var payment = await _unitOfWork.BookingPayments
                .FirstOrDefaultAsync(p => p.PaymentIntentId == request.PaymentIntentId);

            if (payment == null)
            {
                _logger.LogWarning("Payment record not found for PaymentIntentId: {PaymentIntentId}", request.PaymentIntentId);
                return StatusCode(500, new { error = "Payment confirmed but booking not found" });
            }

            // Send confirmation email
            try
            {
                var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(payment.BookingId);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending confirmation emails for booking {BookingId}", payment.BookingId);
                // Don't fail the whole request if email fails
            }

            return Ok(new { bookingId = payment.BookingId, success = true });
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
    public int RoomId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string CheckIn { get; set; } = string.Empty;
    public string CheckOut { get; set; } = string.Empty;
    public int NumberOfGuests { get; set; }
}

public class ConfirmPaymentRequest
{
    public string PaymentIntentId { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string CheckIn { get; set; } = string.Empty;
    public string CheckOut { get; set; } = string.Empty;
    public int NumberOfGuests { get; set; }
    public string? SpecialRequests { get; set; }
}

