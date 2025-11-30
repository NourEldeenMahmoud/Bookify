using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Linq;

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
    public IActionResult Index(DateTime? checkIn, DateTime? checkOut, int? guests, decimal? minPrice, decimal? maxPrice)
    {
        return RedirectToAction("Index", "Home", new {
            checkIn,
            checkOut, 
            guests, 
            minPrice, 
            maxPrice 
        });
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

            _logger.LogInformation("Viewing room details - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}",id, checkIn, checkOut);

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

    /// <summary>
    /// Returns the room details partial view for use inside a modal (AJAX).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DetailsPartial(int id)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid room ID provided for DetailsPartial: {RoomId}", id);
                return BadRequest("Invalid room id.");
            }

            var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(id);
            if (room == null)
            {
                _logger.LogWarning("Room with ID {RoomId} not found for DetailsPartial", id);
                return NotFound();
            }

            return PartialView("~/Views/Rooms/_RoomDetailsPartial.cshtml", room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading room details partial - RoomId: {RoomId}", id);
            return StatusCode(500, "An error occurred while loading room details.");
        }
    }

}

