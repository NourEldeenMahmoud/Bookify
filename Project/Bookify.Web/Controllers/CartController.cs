using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

namespace Bookify.Web.Controllers;

[Authorize]
[IgnoreAntiforgeryToken]
public class CartController : Controller
{
    private readonly ILogger<CartController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoomAvailabilityService _roomAvailabilityService;
    private const string CartSessionKey = "Cart";

    public CartController(
        ILogger<CartController> logger,
        IUnitOfWork unitOfWork,
        IRoomAvailabilityService roomAvailabilityService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _roomAvailabilityService = roomAvailabilityService ?? throw new ArgumentNullException(nameof(roomAvailabilityService));
    }

    private CartViewModel GetCart()
    {
        var cartJson = HttpContext.Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(cartJson))
        {
            return new CartViewModel();
        }

        try
        {
            return JsonSerializer.Deserialize<CartViewModel>(cartJson) ?? new CartViewModel();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deserializing cart from session");
            return new CartViewModel();
        }
    }

    private void SaveCart(CartViewModel cart)
    {
        try
        {
            var cartJson = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CartSessionKey, cartJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving cart to session");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddToCartRequest request)
    {
        try
        {
            if (request == null || request.RoomId <= 0)
            {
                return BadRequest(new { error = "Invalid room ID" });
            }

            // Parse dates
            DateTime checkInDate;
            DateTime checkOutDate;

            if (!string.IsNullOrEmpty(request.CheckIn) && DateTime.TryParse(request.CheckIn, out var parsedCheckIn))
            {
                checkInDate = parsedCheckIn;
            }
            else
            {
                checkInDate = DateTime.Today.AddDays(1);
            }

            if (!string.IsNullOrEmpty(request.CheckOut) && DateTime.TryParse(request.CheckOut, out var parsedCheckOut))
            {
                checkOutDate = parsedCheckOut;
            }
            else
            {
                checkOutDate = checkInDate.AddDays(1);
            }

            if (checkOutDate <= checkInDate)
            {
                return BadRequest(new { error = "Check-out date must be after check-in date" });
            }

            // Get room details
            var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(request.RoomId);
            if (room == null || room.RoomType == null)
            {
                return NotFound(new { error = "Room not found" });
            }

            // Check if room is already in cart
            var cart = GetCart();
            var existingItem = cart.Items.FirstOrDefault(item => item.RoomId == request.RoomId);
            
            if (existingItem != null)
            {
                // Update existing item
                existingItem.CheckIn = checkInDate;
                existingItem.CheckOut = checkOutDate;
                existingItem.NumberOfGuests = request.NumberOfGuests ?? room.RoomType.MaxOccupancy;
            }
            else
            {
                // Add new item
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
                    NumberOfGuests = request.NumberOfGuests ?? room.RoomType.MaxOccupancy
                };

                cart.Items.Add(cartItem);
            }

            SaveCart(cart);

            _logger.LogInformation("Room {RoomId} added to cart", request.RoomId);
            return Ok(new { success = true, itemCount = cart.Items.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding room to cart");
            return StatusCode(500, new { error = "An error occurred while adding room to cart" });
        }
    }

    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            var cart = GetCart();
            return View(cart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cart");
            TempData["Error"] = "An error occurred while loading your cart.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    public IActionResult Update([FromBody] UpdateCartItemRequest request)
    {
        try
        {
            if (request == null || request.RoomId <= 0)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            if (!DateTime.TryParse(request.CheckIn, out var checkInDate) ||
                !DateTime.TryParse(request.CheckOut, out var checkOutDate))
            {
                return BadRequest(new { error = "Invalid date format" });
            }

            if (checkOutDate <= checkInDate)
            {
                return BadRequest(new { error = "Check-out date must be after check-in date" });
            }

            if (request.NumberOfGuests <= 0)
            {
                return BadRequest(new { error = "Number of guests must be greater than zero" });
            }

            var cart = GetCart();
            var item = cart.Items.FirstOrDefault(i => i.RoomId == request.RoomId);
            
            if (item == null)
            {
                return NotFound(new { error = "Item not found in cart" });
            }

            item.CheckIn = checkInDate;
            item.CheckOut = checkOutDate;
            item.NumberOfGuests = request.NumberOfGuests;

            SaveCart(cart);

            _logger.LogInformation("Cart item {RoomId} updated", request.RoomId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cart item");
            return StatusCode(500, new { error = "An error occurred while updating cart item" });
        }
    }

    [HttpPost]
    public IActionResult Remove(int roomId)
    {
        try
        {
            if (roomId <= 0)
            {
                return BadRequest(new { error = "Invalid room ID" });
            }

            var cart = GetCart();
            var item = cart.Items.FirstOrDefault(i => i.RoomId == roomId);
            
            if (item == null)
            {
                return NotFound(new { error = "Item not found in cart" });
            }

            cart.Items.Remove(item);
            SaveCart(cart);

            _logger.LogInformation("Room {RoomId} removed from cart", roomId);
            return Ok(new { success = true, itemCount = cart.Items.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from cart");
            return StatusCode(500, new { error = "An error occurred while removing item from cart" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ProceedToCheckout()
    {
        try
        {
            var cart = GetCart();
            
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Your cart is empty. Please add rooms to your cart first.";
                return RedirectToAction("Index");
            }

            // Validate all items
            var errors = new List<string>();

            foreach (var item in cart.Items)
            {
                // Validate dates
                if (item.CheckOut <= item.CheckIn)
                {
                    errors.Add($"Room {item.RoomNumber}: Check-out date must be after check-in date.");
                    continue;
                }

                // Validate guests
                if (item.NumberOfGuests <= 0 || item.NumberOfGuests > item.MaxOccupancy)
                {
                    errors.Add($"Room {item.RoomNumber}: Number of guests must be between 1 and {item.MaxOccupancy}.");
                    continue;
                }

                // Check availability
                var isAvailable = await _roomAvailabilityService.CheckRoomAvailabilityAsync(
                    item.RoomId, 
                    item.CheckIn, 
                    item.CheckOut);

                if (!isAvailable)
                {
                    errors.Add($"Room {item.RoomNumber} is not available for the selected dates.");
                }
            }

            if (errors.Any())
            {
                TempData["Error"] = string.Join(" ", errors);
                return RedirectToAction("Index");
            }

            // Calculate totals
            var subtotal = cart.Subtotal;
            var taxRate = cart.TaxRate;
            var taxAmount = cart.TaxAmount;
            var totalAmount = cart.TotalAmount;

            // Store cart in TempData for checkout
            TempData["Cart"] = JsonSerializer.Serialize(cart);

            _logger.LogInformation("Proceeding to checkout with {ItemCount} items, Total: {TotalAmount}", 
                cart.Items.Count, totalAmount);

            return RedirectToAction("Checkout", "Bookings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proceeding to checkout");
            TempData["Error"] = "An error occurred while processing your request. Please try again.";
            return RedirectToAction("Index");
        }
    }

    [HttpGet]
    public IActionResult GetCartCount()
    {
        try
        {
            var cart = GetCart();
            return Ok(new { count = cart.Items.Count });
        }
        catch
        {
            return Ok(new { count = 0 });
        }
    }

    [HttpPost]
    public IActionResult Clear()
    {
        try
        {
            HttpContext.Session.Remove(CartSessionKey);
            _logger.LogInformation("Cart cleared successfully");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cart");
            return StatusCode(500, new { error = "An error occurred while clearing cart" });
        }
    }
}

public class AddToCartRequest
{
    public int RoomId { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public int? NumberOfGuests { get; set; }
}

public class UpdateCartItemRequest
{
    public int RoomId { get; set; }
    public string CheckIn { get; set; } = string.Empty;
    public string CheckOut { get; set; } = string.Empty;
    public int NumberOfGuests { get; set; }
}

