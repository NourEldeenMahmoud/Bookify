using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Web.Controllers;

[Authorize]
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

    public async Task<IActionResult> PaymentSuccess(int bookingId)
    {
        try
        {
            if (bookingId <= 0)
            {
                _logger.LogWarning("Invalid bookingId provided: {BookingId}", bookingId);
                return NotFound();
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Payment success - BookingId: {BookingId}, UserId: {UserId}", bookingId, userId);

            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId);
            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingId} not found for payment success", bookingId);
                TempData["Error"] = "Booking not found.";
                return RedirectToAction("MyBookings", "Bookings");
            }

            // Verify booking belongs to user
            if (!string.IsNullOrEmpty(userId) && booking.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to access payment success for booking {BookingId} belonging to another user",
                    userId, bookingId);
                TempData["Error"] = "Access denied.";
                return RedirectToAction("MyBookings", "Bookings");
            }

            // Send confirmation email
            try
            {
                var user = booking.User;
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    await _emailService.SendPaymentConfirmationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        bookingId,
                        booking.TotalAmount
                    );

                    await _emailService.SendBookingConfirmationAsync(
                        user.Email,
                        bookingId,
                        $"{user.FirstName} {user.LastName}",
                        booking.CheckInDate,
                        booking.CheckOutDate,
                        booking.TotalAmount
                    );

                    _logger.LogInformation("Confirmation emails sent for booking {BookingId}", bookingId);
                }
                else
                {
                    _logger.LogWarning("User information missing for booking {BookingId}", bookingId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending confirmation emails for booking {BookingId}", bookingId);
                // Don't fail the whole request if email fails
            }

            TempData["Success"] = "Payment successful! Your booking has been confirmed.";
            return View(booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment success - BookingId: {BookingId}", bookingId);
            TempData["Error"] = "An error occurred while processing payment confirmation. Please contact support.";
            return RedirectToAction("MyBookings", "Bookings");
        }
    }

    public async Task<IActionResult> PaymentCancel(int bookingId)
    {
        try
        {
            if (bookingId <= 0)
            {
                _logger.LogWarning("Invalid bookingId provided: {BookingId}", bookingId);
                return NotFound();
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Payment cancelled - BookingId: {BookingId}, UserId: {UserId}", bookingId, userId);

            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId);
            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingId} not found for payment cancel", bookingId);
                TempData["Error"] = "Booking not found.";
                return RedirectToAction("MyBookings", "Bookings");
            }

            // Verify booking belongs to user
            if (!string.IsNullOrEmpty(userId) && booking.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to access payment cancel for booking {BookingId} belonging to another user",
                    userId, bookingId);
                TempData["Error"] = "Access denied.";
                return RedirectToAction("MyBookings", "Bookings");
            }

            TempData["Error"] = "Payment was cancelled. Your booking is still pending.";
            return RedirectToAction("MyBookings", "Bookings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment cancel - BookingId: {BookingId}", bookingId);
            TempData["Error"] = "An error occurred. Please contact support.";
            return RedirectToAction("MyBookings", "Bookings");
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            _logger.LogInformation("Stripe webhook received");

            var json = await new System.IO.StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();

            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("Empty webhook payload received");
                return BadRequest("Empty payload");
            }

            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Missing Stripe signature in webhook");
                return BadRequest("Missing signature");
            }

            var result = await _paymentService.ProcessStripeWebhookAsync(json, signature);

            if (result)
            {
                _logger.LogInformation("Stripe webhook processed successfully");
                return Ok();
            }

            _logger.LogWarning("Stripe webhook processing failed");
            return BadRequest("Webhook processing failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return StatusCode(500, "Internal server error");
        }
    }
}

