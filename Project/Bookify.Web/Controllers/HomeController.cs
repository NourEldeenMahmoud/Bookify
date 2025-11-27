using System.Diagnostics;
using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
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
        public async Task<IActionResult> Index(int page = 1, int pageSize = 8)
        {
            try
            {
                _logger.LogInformation("Home page accessed - Page: {Page}, PageSize: {PageSize}", page, pageSize);
                
                // Get all available rooms for display on home page
                var allRooms = await _unitOfWork.Rooms.GetAllRoomsWithRoomTypeAsync();
                
                // Filter rooms that are available and don't have any active or future bookings
                var today = DateTime.Today;
                var availableRooms = allRooms
                    .Where(r => r.IsAvailable && 
                        !r.Bookings.Any(b => 
                            b.Status != BookingStatus.Cancelled &&
                            b.CheckOutDate > today)) // Exclude rooms with any non-cancelled bookings (active or future)
                    .ToList();
                
                // Calculate pagination
                var totalRooms = availableRooms.Count;
                var totalPages = (int)Math.Ceiling(totalRooms / (double)pageSize);
                page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
                
                var pagedRooms = availableRooms
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRooms = totalRooms;
                
                if (!availableRooms.Any())
                {
                    _logger.LogWarning("No available rooms found");
                }
                else
                {
                    _logger.LogInformation("Found {Count} available rooms, showing page {Page} of {TotalPages}", 
                        totalRooms, page, totalPages);
                }
                
                return View(pagedRooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading home page");
                TempData["Error"] = "An error occurred while loading the page. Please try again.";
                return View();
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetRoomsPartial(int page = 1, int pageSize = 8)
        {
            try
            {
                _logger.LogInformation("GetRoomsPartial called - Page: {Page}, PageSize: {PageSize}", page, pageSize);
                
                // Get all available rooms for display on home page
                var allRooms = await _unitOfWork.Rooms.GetAllRoomsWithRoomTypeAsync();
                
                // Filter rooms that are available and don't have any active or future bookings
                var today = DateTime.Today;
                var availableRooms = allRooms
                    .Where(r => r.IsAvailable && 
                        !r.Bookings.Any(b => 
                            b.Status != BookingStatus.Cancelled &&
                            b.CheckOutDate > today)) // Exclude rooms with any non-cancelled bookings (active or future)
                    .ToList();
                
                // Calculate pagination
                var totalRooms = availableRooms.Count;
                var totalPages = (int)Math.Ceiling(totalRooms / (double)pageSize);
                page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
                
                var pagedRooms = availableRooms
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRooms = totalRooms;
                
                return PartialView("_RoomsPartial", pagedRooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rooms partial");
                return PartialView("_RoomsPartial", new List<Room>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchRooms(DateTime? checkIn, DateTime? checkOut, int? guests, decimal? minPrice, decimal? maxPrice, int page = 1, int pageSize = 8)
        {
            try
            {
                _logger.LogInformation("SearchRooms called - CheckIn: {CheckIn}, CheckOut: {CheckOut}, Guests: {Guests}, MinPrice: {MinPrice}, MaxPrice: {MaxPrice}, Page: {Page}",
                    checkIn, checkOut, guests, minPrice, maxPrice, page);

                // Hide title when called via AJAX
                ViewBag.HideTitle = true;

                // Validation
                if (checkIn.HasValue && checkOut.HasValue)
                {
                    if (checkIn.Value >= checkOut.Value)
                    {
                        ViewBag.HideTitle = true;
                        return PartialView("_RoomsPartial", new List<Room>());
                    }

                    if (checkIn.Value < DateTime.Today)
                    {
                        ViewBag.HideTitle = true;
                        return PartialView("_RoomsPartial", new List<Room>());
                    }
                }

                if (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value)
                {
                    ViewBag.HideTitle = true;
                    return PartialView("_RoomsPartial", new List<Room>());
                }

                var allRooms = await _unitOfWork.Rooms.GetAllRoomsWithRoomTypeAsync();
                var filteredRooms = allRooms.Where(r => r.IsAvailable).AsQueryable();

                // Filter by check-in and check-out dates (availability)
                if (checkIn.HasValue && checkOut.HasValue)
                {
                    var availableRoomIds = new List<int>();
                    foreach (var room in allRooms)
                    {
                        var isAvailable = await _roomAvailabilityService.CheckRoomAvailabilityAsync(room.Id, checkIn.Value, checkOut.Value);
                        if (isAvailable)
                        {
                            availableRoomIds.Add(room.Id);
                        }
                    }
                    filteredRooms = filteredRooms.Where(r => availableRoomIds.Contains(r.Id));
                }

                // Filter by guests (capacity)
                if (guests.HasValue && guests.Value > 0)
                {
                    filteredRooms = filteredRooms.Where(r => r.RoomType.MaxOccupancy >= guests.Value);
                }

                // Filter by price range
                if (minPrice.HasValue && minPrice.Value > 0)
                {
                    filteredRooms = filteredRooms.Where(r => r.RoomType.PricePerNight >= minPrice.Value);
                }

                if (maxPrice.HasValue && maxPrice.Value > 0)
                {
                    filteredRooms = filteredRooms.Where(r => r.RoomType.PricePerNight <= maxPrice.Value);
                }

                var allFilteredRooms = filteredRooms.ToList();

                // Calculate pagination
                var totalRooms = allFilteredRooms.Count;
                var totalPages = (int)Math.Ceiling(totalRooms / (double)pageSize);
                page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

                var pagedRooms = allFilteredRooms
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRooms = totalRooms;
                ViewBag.HideTitle = true;

                // Store search parameters for pagination
                ViewBag.CheckIn = checkIn;
                ViewBag.CheckOut = checkOut;
                ViewBag.Guests = guests;
                ViewBag.MinPrice = minPrice;
                ViewBag.MaxPrice = maxPrice;

                _logger.LogInformation("Search completed - Found {0} rooms matching criteria, showing page {1} of {2}", 
                    totalRooms, page, totalPages);

                return PartialView("_RoomsPartial", pagedRooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching rooms");
                ViewBag.HideTitle = true;
                return PartialView("_RoomsPartial", new List<Room>());
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
