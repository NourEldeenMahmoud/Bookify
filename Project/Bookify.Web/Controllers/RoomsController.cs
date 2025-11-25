using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using Serilog;

namespace Bookify.Web.Controllers;

public class RoomsController : Controller
{
    private readonly ILogger<RoomsController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoomAvailabilityService _roomAvailabilityService;

    public RoomsController(ILogger<RoomsController> logger, IUnitOfWork unitOfWork, IRoomAvailabilityService roomAvailabilityService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _roomAvailabilityService = roomAvailabilityService ?? throw new ArgumentNullException(nameof(roomAvailabilityService));
    }
    public async Task<IActionResult> Index()
    {
        try
        {
            _logger.LogInformation("Retrieving all rooms from database");
            var rooms = await _unitOfWork.Rooms.GetAllAsync();

            _logger.LogInformation("Successfully retrieved {0} rooms from database", rooms.Count());

            return View(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving rooms at {time}", DateTime.UtcNow);

            return View("Error");
        }

    }
    public async Task<IActionResult> Details(int id, DateTime? checkIn, DateTime? checkOut)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid room ID provided: {RoomId}", id);
                return NotFound();
            }

            _logger.LogInformation("Viewing room details - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}",
                id, checkIn, checkOut);

            var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(id);
            if (room == null)
            {
                _logger.LogWarning("Room with ID {RoomId} not found", id);
                return NotFound();
            }

            ViewBag.CheckInDate = checkIn;
            ViewBag.CheckOutDate = checkOut;

            if (checkIn.HasValue && checkOut.HasValue)
            {
                // Validate dates
                if (checkIn.Value >= checkOut.Value)
                {
                    _logger.LogWarning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}",
                        checkIn.Value, checkOut.Value);
                    TempData["Error"] = "Check-in date must be before check-out date.";
                }
                else if (checkIn.Value < DateTime.Today)
                {
                    _logger.LogWarning("Check-in date is in the past: {CheckIn}", checkIn.Value);
                    TempData["Error"] = "Check-in date cannot be in the past.";
                }
                else
                {
                    var isAvailable = await _roomAvailabilityService.CheckRoomAvailabilityAsync(id, checkIn.Value, checkOut.Value);
                    ViewBag.IsAvailable = isAvailable;
                    _logger.LogDebug("Room {RoomId} availability check result: {IsAvailable}", id, isAvailable);
                }
            }

            return View(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error viewing room details - RoomId: {RoomId}", id);
            TempData["Error"] = "An error occurred while loading room details. Please try again.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddToCart(int roomId, DateTime checkIn, DateTime checkOut, int numberOfGuests)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Add to cart attempt - UserId: {UserId}, RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}, Guests: {NumberOfGuests}",
                userId, roomId, checkIn, checkOut, numberOfGuests);

            if (!User.Identity?.IsAuthenticated ?? true)
            {
                _logger.LogWarning("Unauthenticated user attempted to add room to cart");
                return RedirectToAction("Login", "Account");
            }

            // Validation
            if (roomId <= 0)
            {
                _logger.LogWarning("Invalid roomId provided: {RoomId}", roomId);
                TempData["Error"] = "Invalid room selected.";
                return RedirectToAction("Index", "Home");
            }

            if (checkIn >= checkOut)
            {
                _logger.LogWarning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", checkIn, checkOut);
                TempData["Error"] = "Check-in date must be before check-out date.";
                return RedirectToAction("Details", new { id = roomId, checkIn, checkOut });
            }

            if (checkIn < DateTime.Today)
            {
                _logger.LogWarning("Check-in date is in the past: {CheckIn}", checkIn);
                TempData["Error"] = "Check-in date cannot be in the past.";
                return RedirectToAction("Details", new { id = roomId, checkIn, checkOut });
            }

            if (numberOfGuests <= 0)
            {
                _logger.LogWarning("Invalid numberOfGuests provided: {NumberOfGuests}", numberOfGuests);
                TempData["Error"] = "Number of guests must be greater than zero.";
                return RedirectToAction("Details", new { id = roomId, checkIn, checkOut });
            }

            var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(roomId);
            if (room == null)
            {
                _logger.LogWarning("Room with ID {RoomId} not found", roomId);
                TempData["Error"] = "Room not found.";
                return RedirectToAction("Index", "Home");
            }

            if (numberOfGuests > room.RoomType.MaxOccupancy)
            {
                _logger.LogWarning("Number of guests {NumberOfGuests} exceeds room capacity {Capacity}",
                    numberOfGuests, room.RoomType.MaxOccupancy);
                TempData["Error"] = $"Number of guests exceeds room capacity ({room.RoomType.MaxOccupancy}).";
                return RedirectToAction("Details", new { id = roomId, checkIn, checkOut });
            }

            var isAvailable = await _roomAvailabilityService.CheckRoomAvailabilityAsync(roomId, checkIn, checkOut);
            if (!isAvailable)
            {
                _logger.LogWarning("Room {RoomId} is not available for dates {CheckIn} to {CheckOut}", roomId, checkIn, checkOut);
                TempData["Error"] = "Room is not available for the selected dates.";
                return RedirectToAction("Details", new { id = roomId, checkIn, checkOut });
            }

            // Calculate total amount
            var nights = (checkOut - checkIn).Days;
            var totalAmount = room.RoomType.PricePerNight * nights;

            // add to session cart
            var cartItem = new ReservationCartItem
            {
                RoomId = roomId,
                RoomNumber = room.RoomNumber,
                RoomTypeName = room.RoomType.Name,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                NumberOfGuests = numberOfGuests,
                TotalAmount = totalAmount,
                AddedAt = DateTime.UtcNow
            };

            var cart = HttpContext.Session.GetString("Cart");
            var cartItems = new List<ReservationCartItem>();
            
            if (!string.IsNullOrEmpty(cart))
            {
                try
                {
                    cartItems = JsonSerializer.Deserialize<List<ReservationCartItem>>(cart)
                        ?? new List<ReservationCartItem>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deserializing cart. Starting with empty cart.");
                    cartItems = new List<ReservationCartItem>();
                }

            cartItems.Add(cartItem);
            HttpContext.Session.SetString("Cart", JsonSerializer.Serialize(cartItems));
            }

            _logger.LogInformation("Room {RoomId} added to cart successfully for user {UserId}", roomId, userId);
            TempData["Success"] = "Room added to cart successfully!";
            return RedirectToAction("Cart", "Bookings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding room to cart - RoomId: {RoomId}", roomId);
            TempData["Error"] = "An error occurred while adding room to cart. Please try again.";
            return RedirectToAction("Details", new { id = roomId, checkIn, checkOut });
        }
    }
}

