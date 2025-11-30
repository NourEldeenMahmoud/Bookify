using System;
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
        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation("Home page accessed");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading home page");
                TempData["Error"] = "An error occurred while loading the page. Please try again.";
                return View();
            }
        }

        private (List<Room> pagedRooms, int currentPage, int totalPages) ApplyPagination(IEnumerable<Room> rooms, int page, int pageSize)
        {
            var roomsList = rooms.ToList();
            var totalRooms = roomsList.Count;
            var totalPages = (int)Math.Ceiling(totalRooms / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
            
            var pagedRooms = roomsList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            
            return (pagedRooms, page, totalPages);
        }

        private void SetPaginationViewBag(int currentPage, int totalPages, int pageSize, int totalRooms)
        {
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRooms = totalRooms;
        }

        [HttpGet]
        public async Task<IActionResult> GetRoomsPartial(int page = 1, int pageSize = 8)
        {
            try
            {
                _logger.LogInformation("GetRoomsPartial called - Page: {Page}, PageSize: {PageSize}", page, pageSize);
                
                var allRooms = await _unitOfWork.Rooms.GetAllRoomsWithRoomTypeAsync();
                
                var today = DateTime.Today;
                var availableRooms = allRooms
                    .Where(r => r.IsAvailable && 
                        !r.Bookings.Any(b => 
                            b.Status != BookingStatus.Cancelled &&
                            b.CheckOutDate > today)) 
                    .ToList();
                
                // Apply pagination
                var (pagedRooms, currentPage, totalPages) = ApplyPagination(availableRooms, page, pageSize);
                SetPaginationViewBag(currentPage, totalPages, pageSize, availableRooms.Count);
                
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

                ViewBag.HideTitle = true;

                // validation
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

                IEnumerable<Room> filteredRooms;

                if (checkIn.HasValue && checkOut.HasValue)
                {
                    filteredRooms = await _roomAvailabilityService.GetAvailableRoomsAsync(
                        checkIn.Value, 
                        checkOut.Value, 
                        roomTypeId: null, 
                        minCapacity: guests);
                }
                else
                {
                    var allRooms = await _unitOfWork.Rooms.GetAllRoomsWithRoomTypeAsync();
                    filteredRooms = allRooms.Where(r => r.IsAvailable);
                }

                if (guests.HasValue && guests.Value > 0 && (!checkIn.HasValue || !checkOut.HasValue))
                {
                    filteredRooms = filteredRooms.Where(r => r.RoomType.MaxOccupancy >= guests.Value);
                }


                if (minPrice.HasValue && minPrice.Value > 0)
                {
                    filteredRooms = filteredRooms.Where(r => r.RoomType.PricePerNight >= minPrice.Value);
                }

                if (maxPrice.HasValue && maxPrice.Value > 0)
                {
                    filteredRooms = filteredRooms.Where(r => r.RoomType.PricePerNight <= maxPrice.Value);
                }

                var allFilteredRooms = filteredRooms.ToList();

                // Apply pagination
                var (pagedRooms, currentPage, totalPages) = ApplyPagination(allFilteredRooms, page, pageSize);
                SetPaginationViewBag(currentPage, totalPages, pageSize, allFilteredRooms.Count);
                ViewBag.HideTitle = true;

                // Store search parameters for pagination
                ViewBag.CheckIn = checkIn;
                ViewBag.CheckOut = checkOut;
                ViewBag.Guests = guests;
                ViewBag.MinPrice = minPrice;
                ViewBag.MaxPrice = maxPrice;

                _logger.LogInformation("Search completed - Found {0} rooms matching criteria, showing page {1} of {2}", 
                    allFilteredRooms.Count, currentPage, totalPages);

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
                return View(new ErrorViewModel { RequestId = requestId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in error page handler");
                return View(new ErrorViewModel { RequestId = "Unknown" });
            }
        }
    }
}
