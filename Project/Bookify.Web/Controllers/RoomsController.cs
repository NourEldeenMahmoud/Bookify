using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Web.Controllers
{
    public class RoomsController : Controller
    {
        private readonly ILogger<RoomsController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRoomAvailabilityService _roomAvailabilityService;

        public RoomsController(ILogger<RoomsController> logger, IUnitOfWork unitOfWork, IRoomAvailabilityService roomAvailabilityService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _roomAvailabilityService = roomAvailabilityService;
        }
        public async Task<IActionResult> Index()
        {
            try
            {

                var rooms = await _unitOfWork.Rooms.GetAllAsync();

                _logger.LogInformation($"Successfully retrieved {0} rooms from database", rooms.Count());

                return View(rooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving rooms at {time}", DateTime.UtcNow);

                return View("Error");
            }
            
        }

        public async Task<IActionResult> Details(int roomId, DateTime? checkIn, DateTime? checkOut)
        {
            try
            {

                var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(roomId);
                if (room == null)
                {
                    _logger.LogWarning($"Room with ID {roomId} not found at {DateTime.UtcNow}");
                    return NotFound();
                }

                ViewBag.CheckInDate = checkIn;
                ViewBag.CheckOutDate = checkOut;

                if (checkIn.HasValue && checkOut.HasValue)
                {
                    var isAvailable = await _roomAvailabilityService.CheckRoomAvailabilityAsync(roomId, checkIn.Value, checkOut.Value);
                    ViewBag.IsAvailable = isAvailable;

                    _logger.LogInformation($"Room {roomId} availability checked: {isAvailable}");
                }

                return View(room);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred in Details action for RoomId {roomId} at {DateTime.UtcNow}");
                return View("Error");
            }
        }

        [HttpPost]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToCart(int roomId, DateTime checkIn, DateTime checkOut, int numberOfGuests)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToAction("Login", "Account");
            }

            var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(roomId);
            if (room == null)
            {
                return NotFound();
            }

            var isAvailable = await _roomAvailabilityService.CheckRoomAvailabilityAsync(roomId, checkIn, checkOut);
            if (!isAvailable)
            {
                TempData["Error"] = "Room is not available for the selected dates.";
                return RedirectToAction("Details", new { id = roomId, checkIn, checkOut });
            }

            // Calculate total amount
            var nights = (checkOut - checkIn).Days;
            var totalAmount = room.RoomType.PricePerNight * nights;

            // Add to session cart
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
                cartItems = System.Text.Json.JsonSerializer.Deserialize<List<ReservationCartItem>>(cart) ?? new List<ReservationCartItem>();
            }

            cartItems.Add(cartItem);
            HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cartItems));

            TempData["Success"] = "Room added to cart successfully!";
            return RedirectToAction("Cart", "Bookings");
        }
    }
}
