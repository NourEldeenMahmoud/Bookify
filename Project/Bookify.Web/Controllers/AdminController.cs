using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ILogger<AdminController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebHostEnvironment _environment;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(ILogger<AdminController> logger, IUnitOfWork unitOfWork, IWebHostEnvironment environment, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
    }

    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Admin dashboard accessed - AdminId: {AdminId}", adminId);

            var totalBookings = await _unitOfWork.Bookings.CountAsync();
            var pendingBookings = await _unitOfWork.Bookings.CountAsync(b => b.Status == BookingStatus.Pending);
            var paidBookings = await _unitOfWork.Bookings.CountAsync(b => b.Status == BookingStatus.Paid);
            var totalRooms = await _unitOfWork.Rooms.CountAsync();
            var availableRooms = await _unitOfWork.Rooms.CountAsync(r => r.IsAvailable);
            var totalRevenue = (await _unitOfWork.BookingPayments.GetAllAsync())
                .Where(p => p.PaymentStatus == PaymentStatus.Completed)
                .Sum(p => p.Amount);

            ViewBag.TotalBookings = totalBookings;
            ViewBag.PendingBookings = pendingBookings;
            ViewBag.PaidBookings = paidBookings;
            ViewBag.TotalRooms = totalRooms;
            ViewBag.AvailableRooms = availableRooms;
            ViewBag.TotalRevenue = totalRevenue;

            // Calculate occupancy rate
            var occupiedRooms = totalRooms - availableRooms;
            var occupancyRate = totalRooms > 0 ? (double)occupiedRooms / totalRooms * 100 : 0;
            ViewBag.OccupancyRate = Math.Round(occupancyRate, 2);

            // Revenue data for last 6 months
            var revenueData = new List<decimal>();
            var revenueLabels = new List<string>();
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = DateTime.Today.AddMonths(-i).AddDays(1 - DateTime.Today.Day);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var monthRevenue = (await _unitOfWork.BookingPayments.GetAllAsync())
                    .Where(p => p.PaymentStatus == PaymentStatus.Completed &&
                               p.TransactionDate >= monthStart &&
                               p.TransactionDate <= monthEnd)
                    .Sum(p => p.Amount);
                revenueData.Add(monthRevenue);
                revenueLabels.Add(monthStart.ToString("MMM yyyy"));
            }
            ViewBag.RevenueData = revenueData;
            ViewBag.RevenueLabels = revenueLabels;

            // Occupancy data for last 6 months
            var occupancyData = new List<double>();
            var occupancyLabels = new List<string>();
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = DateTime.Today.AddMonths(-i).AddDays(1 - DateTime.Today.Day);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                // Calculate average occupancy for the month
                var bookingsInMonth = (await _unitOfWork.Bookings.GetAllAsync())
                    .Where(b => b.CheckInDate <= monthEnd && b.CheckOutDate >= monthStart &&
                               (b.Status == BookingStatus.Paid || b.Status == BookingStatus.Completed))
                    .ToList();

                // Simple occupancy calculation: count unique rooms booked
                var uniqueRoomsBooked = bookingsInMonth.Select(b => b.RoomId).Distinct().Count();
                var monthOccupancy = totalRooms > 0 ? (double)uniqueRoomsBooked / totalRooms * 100 : 0;
                occupancyData.Add(Math.Round(monthOccupancy, 2));
                occupancyLabels.Add(monthStart.ToString("MMM yyyy"));
            }
            ViewBag.OccupancyData = occupancyData;
            ViewBag.OccupancyLabels = occupancyLabels;

            _logger.LogDebug("Dashboard data loaded - TotalBookings: {TotalBookings}, TotalRooms: {TotalRooms}, Revenue: {Revenue}",
                totalBookings, totalRooms, totalRevenue);


            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading admin dashboard");
            TempData["Error"] = "An error occurred while loading the dashboard. Please try again.";
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRecentBookings()
    {
        try
        {
            var allBookings = await _unitOfWork.Bookings.GetAllAsync();
            var bookingsList = allBookings.OrderByDescending(b => b.CreatedAt).Take(10).ToList();
            
            var result = new List<object>();
            foreach (var booking in bookingsList)
            {
                var user = await _userManager.FindByIdAsync(booking.UserId);
                var room = await _unitOfWork.Rooms.GetByIdAsync(booking.RoomId);
                
                result.Add(new
                {
                    id = booking.Id,
                    customerName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "N/A",
                    roomNumber = room?.RoomNumber ?? "N/A",
                    checkInDate = booking.CheckInDate.ToString("yyyy-MM-dd"),
                    status = booking.Status.ToString(),
                    totalAmount = booking.TotalAmount
                });
            }

            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recent bookings");
            return Json(new List<object>());
        }
    }

    #region RoomTypes CRUD
    public async Task<IActionResult> RoomTypes()
    {
        var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
        return View(roomTypes);
    }

    [HttpGet]
    public IActionResult CreateRoomType()
    {
        return View(new RoomTypeViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)] // 10MB limit
    [RequestFormLimits(MultipartBodyLengthLimit = 10_485_760)]
    public async Task<IActionResult> CreateRoomType(RoomTypeViewModel model)
    {
        _logger.LogInformation("CreateRoomType POST action called - ImageFile: {HasFile}, FileSize: {FileSize}", 
            model.ImageFile != null, model.ImageFile?.Length ?? 0);
        
        try
        {
            if (model == null)
            {
                _logger.LogWarning("CreateRoomType attempt with null model");
                ModelState.AddModelError("", "Invalid room type data.");
                return View(new RoomTypeViewModel());
            }

            _logger.LogInformation("Creating room type - Name: {Name}", model.Name);

            if (ModelState.IsValid)
            {
                // Validate image file if provided
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    // Validate file size (max 5MB)
                    if (model.ImageFile.Length > 5 * 1024 * 1024)
                {
                        _logger.LogWarning("Image file too large: {Size} bytes", model.ImageFile.Length);
                        ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
                        return View(model);
                }

                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                {
                        _logger.LogWarning("Invalid file type: {Extension}", fileExtension);
                        ModelState.AddModelError("ImageFile", "Invalid file type. Allowed types: JPG, JPEG, PNG, GIF.");
                        return View(model);
                }
                }

                // Create RoomType entity from ViewModel
                var roomType = new RoomType
                {
                    Name = model.Name,
                    Description = model.Description,
                    PricePerNight = model.PricePerNight,
                    MaxOccupancy = model.MaxOccupancy
                };

                // Handle image upload
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    try
                    {
                        roomType.ImageUrl = await SaveImageAsync(model.ImageFile, "roomtypes");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving image for room type");
                        ModelState.AddModelError("ImageFile", "An error occurred while uploading the image. Please try again.");
                        return View(model);
                    }
                }

                await _unitOfWork.RoomTypes.AddAsync(roomType);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Room type created successfully - Id: {Id}, Name: {Name}", roomType.Id, roomType.Name);
                TempData["Success"] = "Room type created successfully.";
                return RedirectToAction(nameof(RoomTypes));
            }

            _logger.LogWarning("CreateRoomType attempt with invalid model state");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room type");
            TempData["Error"] = "An error occurred while creating the room type. Please try again.";
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditRoomType(int id)
    {
        var roomType = await _unitOfWork.RoomTypes.GetByIdAsync(id);
        if (roomType == null) return NotFound();
        
        // Map Entity to ViewModel
        var model = new RoomTypeViewModel
        {
            Id = roomType.Id,
            Name = roomType.Name,
            Description = roomType.Description,
            PricePerNight = roomType.PricePerNight,
            MaxOccupancy = roomType.MaxOccupancy,
            ImageUrl = roomType.ImageUrl
        };
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)] // 10MB limit
    [RequestFormLimits(MultipartBodyLengthLimit = 10_485_760)]
    public async Task<IActionResult> EditRoomType(int id, RoomTypeViewModel model)
    {
        if (id != model.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existingRoomType = await _unitOfWork.RoomTypes.GetByIdAsync(id);
                if (existingRoomType == null)
                {
                    TempData["Error"] = "Room type not found.";
                    return RedirectToAction(nameof(RoomTypes));
                }

                existingRoomType.Name = model.Name;
                existingRoomType.Description = model.Description;
                existingRoomType.PricePerNight = model.PricePerNight;
                existingRoomType.MaxOccupancy = model.MaxOccupancy;

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    // validate file size (max 5MB)
                    if (model.ImageFile.Length > 5 * 1024 * 1024)
                {
                        _logger.LogWarning("Image file too large: {Size} bytes for room type {Id}", model.ImageFile.Length, id);
                        ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
                        return View(model);
                    }

                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        _logger.LogWarning("Invalid file type: {Extension} for room type {Id}", fileExtension, id);
                        ModelState.AddModelError("ImageFile", "Invalid file type. Allowed types: JPG, JPEG, PNG, GIF.");
                        return View(model);
                    }

                    try
                    {
                        existingRoomType.ImageUrl = await SaveImageAsync(model.ImageFile, "roomtypes");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving image for room type {Id}", id);
                        ModelState.AddModelError("ImageFile", "An error occurred while uploading the image. Please try again.");
                        return View(model);
                    }
                }

                _unitOfWork.RoomTypes.Update(existingRoomType);
                await _unitOfWork.SaveChangesAsync();
                
                _logger.LogInformation("Room type updated successfully - Id: {Id}, Name: {Name}", id, model.Name);
                TempData["Success"] = "Room type updated successfully.";
                return RedirectToAction(nameof(RoomTypes));
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency exception updating room type {Id}", id);
                ModelState.AddModelError("", "The room type was modified by another user. Please refresh and try again.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating room type {Id}", id);
                TempData["Error"] = "An error occurred while updating the room type. Please try again.";
                return View(model);
            }
        }
        
        // Log ModelState errors for debugging
        _logger.LogWarning("EditRoomType POST - ModelState invalid for room type {Id}. Errors: {Errors}", 
            id, string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
        
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RoomTypeDetails(int id)
    {
        var roomType = await _unitOfWork.RoomTypes.GetByIdAsync(id);
        if (roomType == null) return NotFound();

        var rooms = await _unitOfWork.Rooms.GetRoomsByTypeAsync(id);
        ViewBag.Rooms = rooms;
        return View(roomType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoomType(int id)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid room type ID provided: {Id}", id);
                return NotFound();
            }

            _logger.LogInformation("Deleting room type - Id: {Id}", id);

            var roomType = await _unitOfWork.RoomTypes.GetByIdAsync(id);
            if (roomType == null)
            {
                _logger.LogWarning("Room type {Id} not found", id);
                return NotFound();
            }

            // Check if room type is being used
            var roomsUsingType = await _unitOfWork.Rooms.CountAsync(r => r.RoomTypeId == id);
            if (roomsUsingType > 0)
            {
                _logger.LogWarning("Cannot delete room type {Id} - {Count} rooms are using it", id, roomsUsingType);
                TempData["Error"] = $"Cannot delete room type. {roomsUsingType} room(s) are using this type.";
                return RedirectToAction(nameof(RoomTypes));
            }

            _unitOfWork.RoomTypes.Remove(roomType);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Room type deleted successfully - Id: {Id}", id);
            TempData["Success"] = "Room type deleted successfully.";
            return RedirectToAction(nameof(RoomTypes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room type - Id: {Id}", id);
            TempData["Error"] = "An error occurred while deleting the room type. Please try again.";
            return RedirectToAction(nameof(RoomTypes));
        }
    }
    #endregion

    #region Rooms CRUD
    public async Task<IActionResult> Rooms()
    {
        var rooms = await _unitOfWork.Rooms.GetAllRoomsWithRoomTypeAsync();
        return View(rooms);
    }

    [HttpGet]
    public async Task<IActionResult> CreateRoom()
    {
        var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
        ViewBag.RoomTypes = new SelectList(roomTypes, "Id", "Name");
        return View(new RoomViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(RoomViewModel model)
    {
        try
        {
            if (model == null)
            {
                _logger.LogWarning("CreateRoom attempt with null model");
                ModelState.AddModelError("", "Invalid room data.");
                var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
                ViewBag.RoomTypes = new SelectList(roomTypes, "Id", "Name");
                return View(new RoomViewModel());
            }

            _logger.LogInformation("Creating room - RoomNumber: {RoomNumber}, RoomTypeId: {RoomTypeId}",
                model.RoomNumber, model.RoomTypeId);

            if (ModelState.IsValid)
            {
                // Check if room number already exists
                var existingRoom = await _unitOfWork.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == model.RoomNumber);
                if (existingRoom != null)
                {
                    _logger.LogWarning("Room number already exists: {RoomNumber}", model.RoomNumber);
                    ModelState.AddModelError("RoomNumber", "Room number already exists.");
                    var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
                    ViewBag.RoomTypes = new SelectList(roomTypes, "Id", "Name");
                    return View(model);
                }

                // Create Room entity from ViewModel
                var room = new Room
                {
                    RoomNumber = model.RoomNumber,
                    RoomTypeId = model.RoomTypeId,
                    IsAvailable = model.IsAvailable,
                    Notes = model.Notes ?? string.Empty
                };

                await _unitOfWork.Rooms.AddAsync(room);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Room created successfully - Id: {Id}, RoomNumber: {RoomNumber}",
                    room.Id, room.RoomNumber);
                TempData["Success"] = "Room created successfully.";
                return RedirectToAction(nameof(Rooms));
            }

            _logger.LogWarning("CreateRoom attempt with invalid model state");
            var roomTypesList = await _unitOfWork.RoomTypes.GetAllAsync();
            ViewBag.RoomTypes = new SelectList(roomTypesList, "Id", "Name");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            TempData["Error"] = "An error occurred while creating the room. Please try again.";
            var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
            ViewBag.RoomTypes = new SelectList(roomTypes, "Id", "Name");
            return View(model ?? new RoomViewModel());
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditRoom(int id)
    {
        var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(id);
        if (room == null) return NotFound();

        // Map Entity to ViewModel
        var model = new RoomViewModel
        {
            Id = room.Id,
            RoomNumber = room.RoomNumber,
            RoomTypeId = room.RoomTypeId,
            IsAvailable = room.IsAvailable,
            Notes = room.Notes
        };

        var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
        ViewBag.RoomTypes = new SelectList(roomTypes, "Id", "Name", model.RoomTypeId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoom(int id, RoomViewModel model)
    {
        if (id != model.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existingRoom = await _unitOfWork.Rooms.GetByIdAsync(id);
                if (existingRoom == null)
                {
                    TempData["Error"] = "Room not found.";
                    return RedirectToAction(nameof(Rooms));
                }

                existingRoom.RoomNumber = model.RoomNumber;
                existingRoom.RoomTypeId = model.RoomTypeId;
                existingRoom.IsAvailable = model.IsAvailable;
                existingRoom.Notes = model.Notes ?? string.Empty;

                _unitOfWork.Rooms.Update(existingRoom);
                await _unitOfWork.SaveChangesAsync();
                
                TempData["Success"] = "Room updated successfully.";
                return RedirectToAction(nameof(Rooms));
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "The room was modified by another user. Please refresh and try again.");
                var roomTypes = await _unitOfWork.RoomTypes.GetAllAsync();
                ViewBag.RoomTypes = new SelectList(roomTypes, "Id", "Name", model.RoomTypeId);
                return View(model);
            }
        }
        
        var roomTypesList = await _unitOfWork.RoomTypes.GetAllAsync();
        ViewBag.RoomTypes = new SelectList(roomTypesList, "Id", "Name", model.RoomTypeId);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RoomDetails(int id)
    {
        var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(id);
        if (room == null) return NotFound();

        var bookings = await _unitOfWork.Bookings.GetBookingsByRoomAsync(id);
        ViewBag.Bookings = bookings;
        return View(room);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid room ID provided: {RoomId}", id);
                return NotFound();
            }

            var room = await _unitOfWork.Rooms.GetByIdAsync(id);
            if (room == null)
            {
                _logger.LogWarning("Room {RoomId} not found", id);
                return NotFound();
            }

            // Check if room has active bookings
            var activeBookings = await _unitOfWork.Bookings.FindAsync(b => 
                b.RoomId == id && 
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid) &&
                b.CheckOutDate >= DateTime.Today);

            if (activeBookings.Any())
            {
                _logger.LogWarning("Cannot delete room {RoomId} - has active bookings", id);
                TempData["Error"] = "Cannot delete room. Room has active bookings.";
                return RedirectToAction(nameof(Rooms));
            }

            // Delete related gallery images
            var galleryImages = await _unitOfWork.GalleryImages.FindAsync(g => g.RoomId == id);
            foreach (var image in galleryImages)
            {
                _unitOfWork.GalleryImages.Remove(image);
            }

            _unitOfWork.Rooms.Remove(room);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Room deleted successfully - Id: {Id}, RoomNumber: {RoomNumber}", id, room.RoomNumber);
            TempData["Success"] = "Room deleted successfully.";
            return RedirectToAction(nameof(Rooms));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room - RoomId: {RoomId}", id);
            TempData["Error"] = "An error occurred while deleting the room. Please try again.";
            return RedirectToAction(nameof(Rooms));
        }
    }

    [HttpPost]
    public async Task<IActionResult> UploadRoomImage(int roomId, IFormFile imageFile, string? altText, bool isPrimary = false)
    {
        try
        {
            if (roomId <= 0)
            {
                _logger.LogWarning("Invalid roomId provided: {RoomId}", roomId);
                return Json(new { success = false, message = "Invalid room ID." });
            }

            if (imageFile == null || imageFile.Length == 0)
            {
                _logger.LogWarning("No file uploaded for room {RoomId}", roomId);
                return Json(new { success = false, message = "No file uploaded." });
            }

            // Validate file size (max 5MB)
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                _logger.LogWarning("Image file too large: {Size} bytes for room {RoomId}", imageFile.Length, roomId);
                return Json(new { success = false, message = "Image file size must be less than 5MB." });
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                _logger.LogWarning("Invalid file type: {Extension} for room {RoomId}", fileExtension, roomId);
                return Json(new { success = false, message = "Invalid file type. Allowed types: JPG, JPEG, PNG, GIF." });
            }

            // Verify room exists
            var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
            if (room == null)
            {
                _logger.LogWarning("Room {RoomId} not found for image upload", roomId);
                return Json(new { success = false, message = "Room not found." });
            }

            _logger.LogInformation("Uploading image for room {RoomId}", roomId);

            var imageUrl = await SaveImageAsync(imageFile, "rooms");
            var galleryImage = new GalleryImage
            {
                RoomId = roomId,
                ImageUrl = imageUrl,
                AltText = altText,

            };

            await _unitOfWork.GalleryImages.AddAsync(galleryImage);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Image uploaded successfully - ImageId: {ImageId}, RoomId: {RoomId}",
                galleryImage.Id, roomId);
            return Json(new { success = true, message = "Image uploaded successfully.", imageId = galleryImage.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image for room {RoomId}", roomId);
            return Json(new { success = false, message = "An error occurred while uploading the image." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteRoomImage(int id)
    {
        var image = await _unitOfWork.GalleryImages.GetByIdAsync(id);
        if (image == null) return NotFound();

        _unitOfWork.GalleryImages.Remove(image);
        await _unitOfWork.SaveChangesAsync();
        return Json(new { success = true, message = "Image deleted successfully." });
    }
    #endregion

    #region Bookings Management
    public async Task<IActionResult> Bookings()
    {
        try
        {
            // Get bookings with user and room information
            var allBookings = await _unitOfWork.Bookings.GetAllAsync();
            var bookingsList = allBookings.ToList();
            
            // Load users and rooms for each booking
            var bookingsWithDetails = new List<Booking>();
            foreach (var booking in bookingsList)
            {
                booking.User = await _userManager.FindByIdAsync(booking.UserId);
                booking.Room = await _unitOfWork.Rooms.GetRoomDetailsAsync(booking.RoomId);
                bookingsWithDetails.Add(booking);
            }
            
            return View(bookingsWithDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bookings");
            TempData["Error"] = "An error occurred while loading bookings. Please try again.";
            return View(new List<Booking>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> BookingDetails(int id)
    {
        var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(id);
        if (booking == null) return NotFound();
        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBookingStatus(int id, BookingStatus status, string? notes)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid booking ID provided: {BookingId}", id);
                return NotFound();
            }

            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Updating booking status - BookingId: {BookingId}, NewStatus: {Status}, AdminId: {AdminId}",
                id, status, adminId);

            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(id);
            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingId} not found", id);
                return NotFound();
            }

            if (booking.Status == status)
            {
                _logger.LogWarning("Booking {BookingId} already has status {Status}", id, status);
                TempData["Warning"] = "Booking already has this status.";
                return RedirectToAction(nameof(BookingDetails), new { id });
            }

            var previousStatus = booking.Status;
            booking.Status = status;
            booking.UpdatedAt = DateTime.UtcNow;

            var statusHistory = new BookingStatusHistory
            {
                BookingId = id,
                PreviousStatus = previousStatus,
                NewStatus = status,
                ChangedByUserId = adminId ?? "",
                ChangedAt = DateTime.UtcNow,
                Notes = notes ?? $"Status changed to {status} by admin"
            };

            await _unitOfWork.BookingStatusHistory.AddAsync(statusHistory);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Booking status updated successfully - BookingId: {BookingId}, {PreviousStatus} -> {NewStatus}",
                id, previousStatus, status);
            TempData["Success"] = "Booking status updated successfully.";
            return RedirectToAction(nameof(BookingDetails), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking status - BookingId: {BookingId}", id);
            TempData["Error"] = "An error occurred while updating the booking status. Please try again.";
            return RedirectToAction(nameof(BookingDetails), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBooking(int id)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid booking ID provided: {BookingId}", id);
                return NotFound();
            }

            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Deleting booking - BookingId: {BookingId}, AdminId: {AdminId}", id, adminId);

            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(id);
            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingId} not found", id);
                return NotFound();
            }

            // Check if booking can be deleted (e.g., only pending or cancelled bookings)
            if (booking.Status == BookingStatus.Paid || booking.Status == BookingStatus.Completed)
            {
                TempData["Error"] = "Cannot delete a paid or completed booking. Please cancel it instead.";
                return RedirectToAction(nameof(Bookings));
            }

            // Delete related records first
            var statusHistory = await _unitOfWork.BookingStatusHistory.GetAllAsync();
            var bookingStatusHistory = statusHistory.Where(h => h.BookingId == id).ToList();
            foreach (var history in bookingStatusHistory)
            {
                _unitOfWork.BookingStatusHistory.Remove(history);
            }

            var payments = await _unitOfWork.BookingPayments.GetAllAsync();
            var bookingPayments = payments.Where(p => p.BookingId == id).ToList();
            foreach (var payment in bookingPayments)
            {
                _unitOfWork.BookingPayments.Remove(payment);
            }

            // Delete the booking
            _unitOfWork.Bookings.Remove(booking);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Booking deleted successfully - BookingId: {BookingId}, AdminId: {AdminId}", id, adminId);
            TempData["Success"] = "Booking deleted successfully.";
            return RedirectToAction(nameof(Bookings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting booking - BookingId: {BookingId}", id);
            TempData["Error"] = "An error occurred while deleting the booking. Please try again.";
            return RedirectToAction(nameof(Bookings));
        }
    }
    #endregion

    #region Users Management
    public async Task<IActionResult> Users()
    {
        try
        {
            var users = await _userManager.Users
                .Include(u => u.Bookings)
                .ToListAsync();

            var usersWithRoles = new List<UserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "Customer";
                
                usersWithRoles.Add(new UserViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email ?? "",
                    PhoneNumber = user.PhoneNumber,
                    Role = userRole,
                    TotalBookings = user.Bookings.Count,
                    CreatedAt = user.CreatedAt,
                    IsLockedOut = await _userManager.IsLockedOutAsync(user),
                    LockoutEnd = await _userManager.GetLockoutEndDateAsync(user)
                });
            }

            return View(usersWithRoles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading users");
            TempData["Error"] = "An error occurred while loading users. Please try again.";
            return View(new List<UserViewModel>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> UserDetails(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.Users
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r!.RoomType)
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Payments)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var userRole = roles.FirstOrDefault() ?? "Customer";
            var allRoles = await _roleManager.Roles
                .Select(r => r.Name!)
                .Where(r => r != "Receptionist")
                .ToListAsync();

            ViewBag.UserRole = userRole;
            ViewBag.AllRoles = allRoles;
            ViewBag.IsLockedOut = await _userManager.IsLockedOutAsync(user);
            ViewBag.LockoutEnd = await _userManager.GetLockoutEndDateAsync(user);

            return View(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user details - UserId: {UserId}", id);
            TempData["Error"] = "An error occurred while loading user details. Please try again.";
            return RedirectToAction(nameof(Users));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(string userId, string newRole)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(newRole))
            {
                _logger.LogWarning("Invalid parameters for UpdateUserRole - UserId: {UserId}, NewRole: {NewRole}", userId, newRole);
                TempData["Error"] = "Invalid parameters.";
                return RedirectToAction(nameof(UserDetails), new { id = userId });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            if (!await _roleManager.RoleExistsAsync(newRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(newRole));
            }

            await _userManager.AddToRoleAsync(user, newRole);

            _logger.LogInformation("User role updated - UserId: {UserId}, NewRole: {NewRole}", userId, newRole);
            TempData["Success"] = "User role updated successfully.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role - UserId: {UserId}, NewRole: {NewRole}", userId, newRole);
            TempData["Error"] = "An error occurred while updating the user role. Please try again.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockUser(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            await _userManager.SetLockoutEnabledAsync(user, true);

            _logger.LogInformation("User locked - UserId: {UserId}", userId);
            TempData["Success"] = "User locked successfully.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking user - UserId: {UserId}", userId);
            TempData["Error"] = "An error occurred while locking the user. Please try again.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockUser(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            await _userManager.SetLockoutEndDateAsync(user, null);

            _logger.LogInformation("User unlocked - UserId: {UserId}", userId);
            TempData["Success"] = "User unlocked successfully.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking user - UserId: {UserId}", userId);
            TempData["Error"] = "An error occurred while unlocking the user. Please try again.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Check if user has bookings
            var bookingsCount = await _unitOfWork.Bookings.CountAsync(b => b.UserId == userId);
            if (bookingsCount > 0)
            {
                TempData["Error"] = $"Cannot delete user. User has {bookingsCount} booking(s).";
                return RedirectToAction(nameof(UserDetails), new { id = userId });
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("User deleted - UserId: {UserId}", userId);
                TempData["Success"] = "User deleted successfully.";
                return RedirectToAction(nameof(Users));
            }
            else
            {
                TempData["Error"] = "An error occurred while deleting the user.";
                return RedirectToAction(nameof(UserDetails), new { id = userId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user - UserId: {UserId}", userId);
            TempData["Error"] = "An error occurred while deleting the user. Please try again.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }
    }
    #endregion

    private async Task<string> SaveImageAsync(IFormFile file, string folder)
    {
        _logger.LogInformation("SaveImageAsync called - FileName: {FileName}, FileSize: {FileSize}, Folder: {Folder}", 
            file?.FileName, file?.Length ?? 0, folder);
        
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is null or empty", nameof(file));
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new ArgumentException("Folder name cannot be null or empty", nameof(folder));
        }

        string filePath = string.Empty;
        try
        {
            _logger.LogDebug("WebRootPath: {WebRootPath}", _environment.WebRootPath);
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
            _logger.LogDebug("Uploads folder path: {UploadsFolder}", uploadsFolder);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
                _logger.LogDebug("Created uploads directory: {Folder}", uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            _logger.LogDebug("Image saved successfully - Path: {Path}", filePath);
            return $"/uploads/{folder}/{uniqueFileName}";
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found - WebRootPath: {WebRootPath}, Folder: {Folder}", 
                _environment.WebRootPath, folder);
            throw new InvalidOperationException("Unable to save image. Directory not found.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access - Path: {Path}", filePath);
            throw new InvalidOperationException("Unable to save image. Access denied.", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error saving image - FileName: {FileName}, Path: {Path}", file.FileName, filePath);
            throw new InvalidOperationException("Unable to save image. IO error occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error saving image - Folder: {Folder}, FileName: {FileName}", 
                folder, file?.FileName);
            throw;
        }
    }
}
