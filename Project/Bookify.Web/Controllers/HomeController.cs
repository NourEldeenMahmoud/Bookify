using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Services.Services;
using Bookify.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Diagnostics;

namespace Bookify.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRoomAvailabilityService _roomAvailabilityService;

        public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork,
                    IRoomAvailabilityService roomAvailabilityService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _roomAvailabilityService = roomAvailabilityService;
        }

        public async Task<IActionResult> Index(RoomSearchViewModel? model)
        {
            try
            {
                var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
                ViewBag.RoomTypes = new SelectList(roomTypes, "Id", "Name", model?.RoomTypeId);

                if (model?.CheckInDate != null && model?.CheckOutDate != null)
                {
                    var availablerooms = await _roomAvailabilityService.GetAvailableRoomsAsync(
                        model.CheckInDate, model.CheckOutDate, model.RoomTypeId, model.MinCapacity);

                    if (model.MaxPrice.HasValue)
                    {
                        availablerooms = availablerooms
                            .Where(r => r.RoomType.PricePerNight <= model.MaxPrice.Value);
                    }

                    ViewBag.AvailableRooms = availablerooms;
                    ViewBag.SearchModel = model;
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading rooms in Index action.");

                return RedirectToAction("Error", "Home");
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}