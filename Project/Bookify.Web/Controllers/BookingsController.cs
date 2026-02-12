using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

namespace Bookify.Web.Controllers;

[Authorize]
public class BookingsController : Controller
{
    private readonly ILogger<BookingsController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public BookingsController(
        ILogger<BookingsController> logger,
        IUnitOfWork unitOfWork,
        IReservationService reservationService,
        IPaymentService paymentService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int? roomId, string? checkIn, string? checkOut, int? numberOfGuests)
    {
        try
        {
            _logger.LogInformation("Checkout GET called - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}, Guests: {Guests}", 
                roomId, checkIn, checkOut, numberOfGuests);
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted checkout - redirecting to login");
                var returnUrl = Url.Action("Checkout", "Bookings");
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            _logger.LogInformation("Authenticated user {UserId} attempting checkout", userId);

            // Check if cart data exists in TempData (from CartController.ProceedToCheckout)
            CartViewModel? cartViewModel = null;
            if (TempData["Cart"] != null)
            {
                try
                {
                    var cartJson = TempData["Cart"]?.ToString();
                    if (!string.IsNullOrEmpty(cartJson))
                    {
                        cartViewModel = System.Text.Json.JsonSerializer.Deserialize<CartViewModel>(cartJson);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deserializing cart from TempData");
                }
            }

            // If no cart data, check for legacy single room checkout
            if (cartViewModel == null || cartViewModel.Items == null || !cartViewModel.Items.Any())
            {
                if (roomId.HasValue && roomId.Value > 0)
                {
                    // Legacy single room checkout - convert to cart format
                    DateTime checkInDate;
                    DateTime checkOutDate;
                    
                    if (!string.IsNullOrEmpty(checkIn) && DateTime.TryParse(checkIn, out var parsedCheckIn))
                    {
                        checkInDate = parsedCheckIn;
                    }
                    else
                    {
                        checkInDate = DateTime.Today.AddDays(1);
                    }
                    
                    if (!string.IsNullOrEmpty(checkOut) && DateTime.TryParse(checkOut, out var parsedCheckOut))
                    {
                        checkOutDate = parsedCheckOut;
                    }
                    else
                    {
                        checkOutDate = DateTime.Today.AddDays(2);
                    }

                    var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(roomId.Value);
                    if (room == null || room.RoomType == null)
                    {
                        _logger.LogWarning("Room {RoomId} not found", roomId.Value);
                        TempData["Error"] = "Room not found.";
                        return RedirectToAction("Index", "Home");
                    }

                    var numberOfGuestsValue = numberOfGuests ?? room.RoomType.MaxOccupancy;

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

                    var cartItem = new CartItemViewModel
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
                        NumberOfGuests = numberOfGuestsValue
                    };

                    cartViewModel = new CartViewModel
                    {
                        Items = new List<CartItemViewModel> { cartItem }
                    };
                }
                else
                {
                    // No cart and no roomId - redirect to cart
                    TempData["Error"] = "Your cart is empty. Please add rooms to your cart first.";
                    return RedirectToAction("Index", "Cart");
                }
            }

            // Calculate totals
            var subtotal = cartViewModel.Items.Sum(item => item.Subtotal);
            var taxRate = 0.14m;
            var taxAmount = subtotal * taxRate;
            var totalAmount = subtotal + taxAmount;

            // Get included services and amenities from all room types in cart (without duplicates)
            var allIncludedServices = new List<string>();
            var allAmenities = new List<string>();
            
            foreach (var item in cartViewModel.Items)
            {
                var roomTypeName = item.RoomTypeName?.ToLower() ?? string.Empty;
                var itemServices = new List<string>();
                
                // Determine services based on room type
                if (roomTypeName.Contains("suite"))
                {
                    itemServices = new List<string> { "Cleaning", "Open Buffet", "Room Service", "Concierge" };
                }
                else if (roomTypeName.Contains("deluxe"))
                {
                    itemServices = new List<string> { "Breakfast", "Airport Pickup", "WiFi", "Room Service" };
                }
                else 
                {
                    itemServices = new List<string> { "WiFi", "Breakfast", "Air Conditioning" };
                }
                
                // Add services to the collection (will be deduplicated later)
                allIncludedServices.AddRange(itemServices);
                
                // All rooms have the same amenities
                var itemAmenities = new List<string> 
                { 
                    "Free WiFi", 
                    "Flat-screen TV", 
                    "Air Conditioning", 
                    "Mini Bar", 
                    "Room Service",
                    "Private Bathroom",
                    "Safe"
                };
                
                allAmenities.AddRange(itemAmenities);
            }
            
            // Remove duplicates and sort
            var includedServices = allIncludedServices.Distinct().OrderBy(s => s).ToList();
            var amenities = allAmenities.Distinct().OrderBy(a => a).ToList();

            var viewModel = new CheckoutViewModel
            {
                CartItems = cartViewModel.Items,
                Subtotal = subtotal,
                TaxRate = taxRate,
                TaxAmount = taxAmount,
                Discount = 0m,
                TotalAmount = totalAmount,
                IncludedServices = includedServices,
                Amenities = amenities,
                StripePublishableKey = _configuration["Stripe:PublishableKey"]
            };

            // Store cart in TempData for payment confirmation
            TempData["Cart"] = System.Text.Json.JsonSerializer.Serialize(cartViewModel);

            return View(viewModel);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error during checkout - UserId: {UserId}",User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during checkout - UserId: {UserId}",User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            TempData["Error"] = "An error occurred during checkout. Please try again.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int? bookingId, string? paymentIntentId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted to view confirmation");
                return RedirectToAction("Login", "Account");
            }

            List<Booking> bookings = new List<Booking>();

            // If paymentIntentId is provided, get all bookings for that payment
            if (!string.IsNullOrEmpty(paymentIntentId))
            {
                _logger.LogInformation("Loading confirmation for PaymentIntentId: {PaymentIntentId}", paymentIntentId);
                
                var allPayments = (await _unitOfWork.BookingPayments.GetAllAsync())
                    .Where(p => p.PaymentIntentId == paymentIntentId)
                    .ToList();

                if (!allPayments.Any())
                {
                    _logger.LogWarning("No payments found for PaymentIntentId: {PaymentIntentId}", paymentIntentId);
                    TempData["Error"] = "Booking not found.";
                    return RedirectToAction("Index", "Profile");
                }

                var bookingIds = allPayments.Select(p => p.BookingId).Distinct().ToList();
                
                foreach (var id in bookingIds)
                {
                    var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(id);
                    if (booking != null && booking.UserId == userId)
                    {
                        bookings.Add(booking);
                    }
                }

                if (!bookings.Any())
                {
                    _logger.LogWarning("No bookings found for PaymentIntentId: {PaymentIntentId} and UserId: {UserId}", paymentIntentId, userId);
                    TempData["Error"] = "Access denied.";
                    return RedirectToAction("Index", "Profile");
            }
            }
            // If bookingId is provided, get single booking
            else if (bookingId.HasValue && bookingId.Value > 0)
            {
                var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId.Value);
            if (booking == null)
            {
                    _logger.LogWarning("Booking {BookingId} not found", bookingId.Value);
                TempData["Error"] = "Booking not found.";
                return RedirectToAction("Index", "Profile");
            }

            if (booking.UserId != userId)
            {
                    _logger.LogWarning("User {UserId} attempted to access booking {BookingId} belonging to another user", userId, bookingId.Value);
                TempData["Error"] = "Access denied.";
                return RedirectToAction("Index", "Profile");
            }

                bookings.Add(booking);
            }
            else
            {
                _logger.LogWarning("Neither bookingId nor paymentIntentId provided");
                return NotFound();
            }

            // If only one booking, use the existing view model (single booking)
            if (bookings.Count == 1)
            {
                return View(bookings[0]);
            }
            else
            {
                // Multiple bookings - pass to view with a different model
                ViewBag.Bookings = bookings;
                ViewBag.IsMultiple = true;
                // Use the first booking as the main model for backward compatibility
                return View(bookings[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading confirmation - BookingId: {BookingId}, PaymentIntentId: {PaymentIntentId}", bookingId, paymentIntentId);
            TempData["Error"] = "An error occurred while loading confirmation. Please try again.";
            return RedirectToAction("Index", "Profile");
        }
    }

    public async Task<IActionResult> MyBookings()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted to cancel booking");
                return RedirectToAction("Login", "Account");
            }

            if (id <= 0)
            {
                _logger.LogWarning("Invalid booking ID provided: {BookingId}", id);
                TempData["Error"] = "Invalid booking selected.";
                return RedirectToAction("Index", "Profile");
            }

            _logger.LogInformation("Cancelling booking - BookingId: {BookingId}, UserId: {UserId}", id, userId);

            var result = await _reservationService.CancelReservationAsync(id, userId);
            if (result)
            {
                _logger.LogInformation("Booking {BookingId} cancelled successfully by user {UserId}", id, userId);
                TempData["Success"] = "Booking cancelled successfully.";
                
                // send cancellation email ( don't stop cancellation if email fails)
                try
                {
                    var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(id);
                    if (booking != null && booking.User != null && !string.IsNullOrWhiteSpace(booking.User.Email))
                    {
                        var userName = !string.IsNullOrWhiteSpace(booking.User.FirstName) 
                            ? $"{booking.User.FirstName} {booking.User.LastName}".Trim() 
                            : booking.User.UserName ?? "Guest";
                        
                        await _emailService.SendBookingCancellationAsync(
                            booking.User.Email,
                            userName,
                            id
                        );
                        
                        _logger.LogInformation("Booking cancellation email sent successfully - BookingId: {BookingId}, Email: {Email}", 
                            id, booking.User.Email);
                    }
                    else
                    {
                        _logger.LogWarning("User not found or email is empty - BookingId: {BookingId}, UserId: {UserId}", 
                            id, userId);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the cancellation - email is non-critical
                    _logger.LogError(ex, "Failed to send booking cancellation email - BookingId: {BookingId}, UserId: {UserId}. Booking was still cancelled successfully.", 
                        id, userId);
                }
            }
            else
            {
                _logger.LogWarning("Unable to cancel booking {BookingId} for user {UserId}", id, userId);
                TempData["Error"] = "Unable to cancel booking. It may have already been cancelled or completed.";
            }

            return RedirectToAction("Index", "Profile");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error cancelling booking {BookingId}", id);
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Profile");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId} - UserId: {UserId}",
                id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            TempData["Error"] = "An error occurred while cancelling the booking. Please try again.";
            return RedirectToAction("Index", "Profile");
        }
    }
}

