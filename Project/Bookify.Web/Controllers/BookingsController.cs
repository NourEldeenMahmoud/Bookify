using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Security.Claims;

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
    public async Task<IActionResult> Checkout(int roomId, string? checkIn, string? checkOut, int? numberOfGuests)
    {
        try
        {
            _logger.LogInformation("Checkout GET called - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}, Guests: {Guests}", 
                roomId, checkIn, checkOut, numberOfGuests);
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted checkout - redirecting to login");
                var returnUrl = Url.Action("Checkout", "Bookings", new { roomId, checkIn, checkOut, numberOfGuests });
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            _logger.LogInformation("Authenticated user {UserId} attempting checkout", userId);

            // Validation
            if (roomId <= 0)
            {
                _logger.LogWarning("Invalid roomId provided: {RoomId}", roomId);
                TempData["Error"] = "Invalid room selected.";
                return RedirectToAction("Index", "Home");
            }

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
            
            _logger.LogInformation("Parsed dates - CheckIn: {CheckIn}, CheckOut: {CheckOut}", checkInDate, checkOutDate);

            var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(roomId);
            if (room == null || room.RoomType == null)
            {
                _logger.LogWarning("Room {RoomId} not found", roomId);
                TempData["Error"] = "Room not found.";
                return RedirectToAction("Index", "Home");
            }

            var numberOfGuestsValue = numberOfGuests ?? room.RoomType.MaxOccupancy;

            // Validate dates - but allow user to change them on checkout page
            if (checkInDate >= checkOutDate)
            {
                _logger.LogWarning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", checkInDate, checkOutDate);
                TempData["Warning"] = "Please select valid dates. Check-in date must be before check-out date.";
                checkInDate = DateTime.Today.AddDays(1);
                checkOutDate = DateTime.Today.AddDays(2);
            }

            if (checkInDate < DateTime.Today)
            {
                _logger.LogWarning("Check-in date is in the past: {CheckIn}", checkInDate);
                TempData["Warning"] = "Check-in date cannot be in the past. Using tomorrow's date.";
                checkInDate = DateTime.Today.AddDays(1);
                checkOutDate = DateTime.Today.AddDays(2);
            }

            if (numberOfGuestsValue <= 0 || numberOfGuestsValue > room.RoomType.MaxOccupancy)
            {
                _logger.LogWarning("Invalid numberOfGuests provided: {NumberOfGuests}", numberOfGuestsValue);
                TempData["Warning"] = $"Number of guests must be between 1 and {room.RoomType.MaxOccupancy}. Using default value.";
                numberOfGuestsValue = Math.Min(room.RoomType.MaxOccupancy, Math.Max(1, numberOfGuestsValue));
            }

            var numberOfNights = (checkOutDate - checkInDate).Days;
            var pricePerNight = room.RoomType.PricePerNight;
            var subtotal = pricePerNight * numberOfNights;
            var taxRate = 0.14m; 
            var taxAmount = subtotal * taxRate;
            var discount = 0m; 
            var totalAmount = subtotal + taxAmount - discount;

            // Get room image
            var roomImageUrl = room.GalleryImages?.FirstOrDefault()?.ImageUrl ?? room.RoomType.ImageUrl;
            if (string.IsNullOrEmpty(roomImageUrl))
            {
                roomImageUrl = Url.Content("~/images/G1.jpg");
            }
            else if (!roomImageUrl.StartsWith("http") && !roomImageUrl.StartsWith("/") && !roomImageUrl.StartsWith("~/"))
            {
                // Only add ~/images/ if the URL doesn't already contain it
                roomImageUrl = Url.Content($"~/images/{roomImageUrl}");
            }
            else if (roomImageUrl.StartsWith("~/"))
            {
                // If it already starts with ~/, just use Url.Content to convert it
                roomImageUrl = Url.Content(roomImageUrl);
            }

            var includedServices = new List<string>();
            var typeName = room.RoomType.Name?.ToLower() ?? string.Empty;
            
            if (typeName.Contains("suite"))
            {
                includedServices = new List<string> { "Cleaning", "Open Buffet", "Room Service", "Concierge" };
            }
            else if (typeName.Contains("deluxe"))
            {
                includedServices = new List<string> { "Breakfast", "Airport Pickup", "WiFi", "Room Service" };
            }
            else 
            {
                includedServices = new List<string> { "WiFi", "Breakfast", "Air Conditioning" };
            }

            var amenities = new List<string> 
            { 
                "Free WiFi", 
                "Flat-screen TV", 
                "Air Conditioning", 
                "Mini Bar", 
                "Room Service",
                "Private Bathroom",
                "Safe"
            };

            var viewModel = new CheckoutViewModel
            {
                RoomId = roomId,
                RoomNumber = room.RoomNumber,
                RoomTypeName = room.RoomType!.Name ?? string.Empty,
                RoomImageUrl = roomImageUrl,
                RoomDescription = room.RoomType!.Description,
                MaxOccupancy = room.RoomType!.MaxOccupancy,
                CheckIn = checkInDate,
                CheckOut = checkOutDate,
                NumberOfGuests = numberOfGuestsValue,
                PricePerNight = pricePerNight,
                NumberOfNights = numberOfNights,
                Subtotal = subtotal,
                TaxRate = taxRate,
                TaxAmount = taxAmount,
                Discount = discount,
                TotalAmount = totalAmount,
                IncludedServices = includedServices,
                Amenities = amenities,
                StripePublishableKey = _configuration["Stripe:PublishableKey"]
            };

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
    public async Task<IActionResult> Confirmation(int bookingId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted to view confirmation");
                return RedirectToAction("Login", "Account");
            }

            if (bookingId <= 0)
            {
                _logger.LogWarning("Invalid bookingId provided: {BookingId}", bookingId);
                return NotFound();
            }

            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId);
            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingId} not found", bookingId);
                TempData["Error"] = "Booking not found.";
                return RedirectToAction("Index", "Profile");
            }

            if (booking.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to access booking {BookingId} belonging to another user", userId, bookingId);
                TempData["Error"] = "Access denied.";
                return RedirectToAction("Index", "Profile");
            }

            return View(booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading confirmation - BookingId: {BookingId}", bookingId);
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

