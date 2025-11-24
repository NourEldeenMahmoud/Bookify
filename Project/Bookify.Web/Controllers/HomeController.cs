using System.Diagnostics;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Web.Models;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bookify.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRoomAvailabilityService _roomAvailabilityService;


        public HomeController(ILogger<HomeController> logger , IUnitOfWork unitOfWork, IRoomAvailabilityService roomAvailabilityService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _roomAvailabilityService = roomAvailabilityService ?? throw new ArgumentNullException(nameof(roomAvailabilityService));
        }
        public async Task<IActionResult> Index(RoomSearchViewModel? searchModel)
        {
            try
            {
                _logger.LogInformation("Home page accessed");
                var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
                ViewBag.RoomTypes = new SelectList(roomTypes, "Id", "Name", searchModel?.RoomTypeId);
                if (searchModel?.CheckInDate != null && searchModel?.CheckOutDate != null)
                {
                    // validate dates
                    if (searchModel.CheckInDate.Value >= searchModel.CheckOutDate.Value)
                    {
                        _logger.LogWarning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}",
                            searchModel.CheckInDate.Value, searchModel.CheckOutDate.Value);
                        ModelState.AddModelError("", "Check-in date must be before check-out date.");
                        return View();
                    }

                    if (searchModel.CheckInDate.Value < DateTime.Today)
                    {
                        _logger.LogWarning("Check-in date is in the past: {CheckIn}", searchModel.CheckInDate.Value);
                        ModelState.AddModelError("", "Check-in date cannot be in the past.");
                        return View();
                    }

                    _logger.LogInformation("Searching for available rooms - CheckIn: {CheckIn}, CheckOut: {CheckOut}, RoomTypeId: {RoomTypeId}, MinCapacity: {MinCapacity}",
                        searchModel.CheckInDate.Value, searchModel.CheckOutDate.Value, searchModel.RoomTypeId, searchModel.MinCapacity);

                    var availableRooms = await _roomAvailabilityService.GetAvailableRoomsAsync(
                        searchModel.CheckInDate.Value,
                        searchModel.CheckOutDate.Value,
                        searchModel.RoomTypeId,
                        searchModel.MinCapacity
                    );

                    if (searchModel.MaxPrice.HasValue)
                    {
                        if (searchModel.MaxPrice.Value < 0)
                        {
                            _logger.LogWarning("Invalid MaxPrice provided: {MaxPrice}", searchModel.MaxPrice.Value);
                            ModelState.AddModelError("", "Maximum price cannot be negative.");
                            return View();
                        }

                        availableRooms = availableRooms.Where(r => r.RoomType.PricePerNight <= searchModel.MaxPrice.Value);
                    }

                    var roomsList = availableRooms.ToList();
                    _logger.LogInformation("Found {Count} available rooms", roomsList.Count);

                    ViewBag.AvailableRooms = roomsList;
                    ViewBag.SearchModel = searchModel;
                }

                return View();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading home page");
                TempData["Error"] = "An error occurred while loading the page. Please try again.";
                return View();
                throw;
            }
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            try
            {
                var requestId = HttpContext.TraceIdentifier;
                _logger.LogWarning("Error page accessed - RequestId: {RequestId}", requestId);
                return View(new Models.ErrorViewModel { RequestId = requestId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in error page handler");
                return View(new Models.ErrorViewModel { RequestId = "Unknown" });
            }
        }
    }
}
