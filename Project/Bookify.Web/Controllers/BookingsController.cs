using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Web.Controllers;

[Authorize]
public class BookingsController : Controller
{
    private readonly ILogger<BookingsController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;
    private readonly IEmailService _emailService;

    public BookingsController(
        ILogger<BookingsController> logger,
        IUnitOfWork unitOfWork,
        IReservationService reservationService,
        IPaymentService paymentService,
        IEmailService emailService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    public IActionResult Cart()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogDebug("Cart page accessed - UserId: {UserId}", userId);

            var cartJson = HttpContext.Session.GetString("Cart");
            var cartItems = new List<ReservationCartItem>();

            if (!string.IsNullOrEmpty(cartJson))
            {
                try
                {
                    cartItems = System.Text.Json.JsonSerializer.Deserialize<List<ReservationCartItem>>(cartJson)
                        ?? new List<ReservationCartItem>();
                    _logger.LogDebug("Loaded {Count} items from cart", cartItems.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deserializing cart. Starting with empty cart.");
                    cartItems = new List<ReservationCartItem>();
                    HttpContext.Session.Remove("Cart");
                }
            }

            return View(cartItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cart");
            TempData["Error"] = "An error occurred while loading your cart. Please try again.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    public IActionResult RemoveFromCart(int index)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Remove from cart attempt - UserId: {UserId}, Index: {Index}", userId, index);

            if (index < 0)
            {
                _logger.LogWarning("Invalid index provided: {Index}", index);
                TempData["Error"] = "Invalid item selected.";
                return RedirectToAction("Cart");
            }

            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson))
            {
                _logger.LogWarning("Attempt to remove from empty cart");
                TempData["Error"] = "Cart is empty.";
                return RedirectToAction("Cart");
            }

            var cartItems = System.Text.Json.JsonSerializer.Deserialize<List<ReservationCartItem>>(cartJson)
                ?? new List<ReservationCartItem>();

            if (index >= cartItems.Count)
            {
                _logger.LogWarning("Index {Index} out of range for cart with {Count} items", index, cartItems.Count);
                TempData["Error"] = "Invalid item selected.";
                return RedirectToAction("Cart");
            }

            var removedItem = cartItems[index];
            cartItems.RemoveAt(index);
            HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cartItems));

            _logger.LogInformation("Item removed from cart - RoomId: {RoomId}, UserId: {UserId}", removedItem.RoomId, userId);
            TempData["Success"] = "Item removed from cart successfully.";
            return RedirectToAction("Cart");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from cart - Index: {Index}", index);
            TempData["Error"] = "An error occurred while removing item from cart. Please try again.";
            return RedirectToAction("Cart");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(int index)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Checkout attempt - UserId: {UserId}, Index: {Index}", userId, index);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted checkout");
                return RedirectToAction("Login", "Account");
            }

            if (index < 0)
            {
                _logger.LogWarning("Invalid index provided: {Index}", index);
                TempData["Error"] = "Invalid item selected.";
                return RedirectToAction("Cart");
            }

            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson))
            {
                _logger.LogWarning("Checkout attempt with empty cart - UserId: {UserId}", userId);
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Cart");
            }

            var cartItems = System.Text.Json.JsonSerializer.Deserialize<List<ReservationCartItem>>(cartJson)
                ?? new List<ReservationCartItem>();

            if (index >= cartItems.Count)
            {
                _logger.LogWarning("Index {Index} out of range for cart with {Count} items", index, cartItems.Count);
                TempData["Error"] = "Invalid item selected.";
                return RedirectToAction("Cart");
            }

            var cartItem = cartItems[index];

            // Validate cart item
            if (cartItem.RoomId <= 0)
            {
                _logger.LogWarning("Invalid RoomId in cart item: {RoomId}", cartItem.RoomId);
                TempData["Error"] = "Invalid room selected.";
                return RedirectToAction("Cart");
            }

            if (cartItem.CheckInDate >= cartItem.CheckOutDate)
            {
                _logger.LogWarning("Invalid date range in cart item");
                TempData["Error"] = "Invalid date range selected.";
                return RedirectToAction("Cart");
            }

            // Create booking
            var booking = await _reservationService.CreateReservationAsync(
                userId,
                cartItem.RoomId,
                cartItem.CheckInDate,
                cartItem.CheckOutDate,
                cartItem.NumberOfGuests
            );

            _logger.LogInformation("Booking created successfully - BookingId: {BookingId}, UserId: {UserId}", booking.Id, userId);

            // Remove from cart
            cartItems.RemoveAt(index);
            HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cartItems));

            // Create Stripe checkout session
            var successUrl = Url.Action("PaymentSuccess", "Payment", new { bookingId = booking.Id }, Request.Scheme);
            var cancelUrl = Url.Action("PaymentCancel", "Payment", new { bookingId = booking.Id }, Request.Scheme);

            if (string.IsNullOrEmpty(successUrl) || string.IsNullOrEmpty(cancelUrl))
            {
                _logger.LogError("Failed to generate payment URLs");
                TempData["Error"] = "An error occurred while processing payment. Please try again.";
                return RedirectToAction("Cart");
            }

            var checkoutUrl = await _paymentService.CreateStripeCheckoutSessionAsync(booking.Id, successUrl, cancelUrl);

            _logger.LogInformation("Redirecting to Stripe checkout - BookingId: {BookingId}", booking.Id);
            return Redirect(checkoutUrl);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error during checkout - UserId: {UserId}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            TempData["Error"] = ex.Message;
            return RedirectToAction("Cart");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error during checkout - UserId: {UserId}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            TempData["Error"] = ex.Message;
            return RedirectToAction("Cart");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during checkout - UserId: {UserId}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            TempData["Error"] = "An error occurred during checkout. Please try again.";
            return RedirectToAction("Cart");
        }
    }

    public async Task<IActionResult> MyBookings()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted to view bookings");
                return RedirectToAction("Login", "Account");
            }

            _logger.LogInformation("Viewing bookings - UserId: {UserId}", userId);

            var bookings = await _unitOfWork.Bookings.GetUserBookingsById(userId);
            var bookingsList = bookings.ToList();
            _logger.LogDebug("Retrieved {Count} bookings for user {UserId}", bookingsList.Count, userId);

            return View(bookingsList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bookings - UserId: {UserId}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            TempData["Error"] = "An error occurred while loading your bookings. Please try again.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted to cancel booking");
                return RedirectToAction("Login", "Account");
            }

            if (id <= 0)
            {
                _logger.LogWarning("Invalid booking ID provided: {BookingId}", id);
                TempData["Error"] = "Invalid booking selected.";
                return RedirectToAction("MyBookings");
            }

            _logger.LogInformation("Cancelling booking - BookingId: {BookingId}, UserId: {UserId}", id, userId);

            var result = await _reservationService.CancelReservationAsync(id, userId);
            if (result)
            {
                _logger.LogInformation("Booking {BookingId} cancelled successfully by user {UserId}", id, userId);
                TempData["Success"] = "Booking cancelled successfully.";
            }
            else
            {
                _logger.LogWarning("Unable to cancel booking {BookingId} for user {UserId}", id, userId);
                TempData["Error"] = "Unable to cancel booking. It may have already been cancelled or completed.";
            }

            return RedirectToAction("MyBookings");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error cancelling booking {BookingId}", id);
            TempData["Error"] = ex.Message;
            return RedirectToAction("MyBookings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId} - UserId: {UserId}",
                id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            TempData["Error"] = "An error occurred while cancelling the booking. Please try again.";
            return RedirectToAction("MyBookings");
        }
    }
}

