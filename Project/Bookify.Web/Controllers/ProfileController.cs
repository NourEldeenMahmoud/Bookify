using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Bookify.Web.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ILogger<ProfileController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserProfileService _userProfileService;

        public ProfileController(ILogger<ProfileController> logger, IUnitOfWork unitOfWork,UserManager<ApplicationUser> userManager, IUserProfileService userProfileService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User ID not found in claims");TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Index", "Home");
                }
                _logger.LogInformation("Viewing profile - UserId: {UserId}", userId);

                var user = await _userProfileService.GetUserProfileAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found", userId);
                    TempData["Error"] = "User profile not found.";
                    return RedirectToAction("Index", "Home");
                }
                var upcomingBookings = await _userProfileService.GetUpcomingBookingsAsync(userId);
                var pastBookings = await _userProfileService.GetPastBookingsAsync(userId);
                ViewBag.UpcomingBookings = upcomingBookings;
                ViewBag.PastBookings = pastBookings;

                _logger.LogDebug("Profile loaded successfully - UserId: {UserId}, Upcoming: {UpcomingCount}, Past: {PastCount}",
                    userId, upcomingBookings.Count(), pastBookings.Count());

                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile index");
                TempData["ErrorMessage"] = "An error occurred while loading your profile.";
                return RedirectToAction("Index","Home");
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model)
        {
            try
            {
                var userId = User.FindFirst( ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unauthenticated user attempted to update profile");
                    return RedirectToAction("Login", "Account");
                }

                if (model == null)
                {
                    _logger.LogWarning("Update profile attempt with null model - UserId: {UserId}", userId);
                    TempData["Error"] = "Invalid profile data.";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation("Updating profile - UserId: {UserId}", userId);

                var user = await _userProfileService.GetUserProfileAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found", userId);
                    TempData["Error"] = "User profile not found.";
                    return RedirectToAction("Index", "Home");
                }

                // Update user object with model values for display
                if (!string.IsNullOrEmpty(model.FirstName)) user.FirstName = model.FirstName;
                if (!string.IsNullOrEmpty(model.LastName)) user.LastName = model.LastName;
                if (model.DateOfBirth.HasValue) user.DateOfBirth = model.DateOfBirth;
                if (!string.IsNullOrEmpty(model.Address)) user.Address = model.Address;
                if (!string.IsNullOrEmpty(model.City)) user.City = model.City;
                if (!string.IsNullOrEmpty(model.PostalCode)) user.PostalCode = model.PostalCode;
                if (!string.IsNullOrEmpty(model.Country)) user.Country = model.Country;

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Update profile attempt with invalid model state - UserId: {UserId}", userId);
                    var upcomingBookings = await _userProfileService.GetUpcomingBookingsAsync(userId);
                    var pastBookings = await _userProfileService.GetPastBookingsAsync(userId);
                    ViewBag.UpcomingBookings = upcomingBookings;
                    ViewBag.PastBookings = pastBookings;
                    return View("Index", user);
                }

                // Additional validation
                if (model.DateOfBirth.HasValue && model.DateOfBirth.Value > DateTime.Today)
                {
                    _logger.LogWarning("Invalid date of birth provided - UserId: {UserId}, DateOfBirth: {DateOfBirth}",userId, model.DateOfBirth.Value);
                    ModelState.AddModelError("", "Date of birth cannot be in the future.");
                    var upcomingBookings = await _userProfileService.GetUpcomingBookingsAsync(userId);
                    var pastBookings = await _userProfileService.GetPastBookingsAsync(userId);
                    ViewBag.UpcomingBookings = upcomingBookings;
                    ViewBag.PastBookings = pastBookings;
                    return View("Index", user);
                }

                var result = await _userProfileService.UpdateUserProfileAsync(
                    userId,
                    model.FirstName,
                    model.LastName,
                    model.DateOfBirth,
                    model.Address,
                    model.City,
                    model.PostalCode,
                    model.Country
                );

                if (result)
                {
                    _logger.LogInformation("Profile updated successfully - UserId: {UserId}", userId);
                    TempData["Success"] = "Profile updated successfully.";
                }
                else
                {
                    _logger.LogWarning("Failed to update profile - UserId: {UserId}", userId);
                    TempData["Error"] = "Failed to update profile. Please try again.";
                }

                return RedirectToAction("Index");
            }
            catch (ArgumentException ex)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogWarning(ex, "Validation error updating profile - UserId: {UserId}", userId);
                ModelState.AddModelError("", ex.Message);
                
                var user = await _userProfileService.GetUserProfileAsync(userId ?? "");
                if (user != null)
                {
                    var upcomingBookings = await _userProfileService.GetUpcomingBookingsAsync(userId ?? "");
                    var pastBookings = await _userProfileService.GetPastBookingsAsync(userId ?? "");
                    ViewBag.UpcomingBookings = upcomingBookings;
                    ViewBag.PastBookings = pastBookings;
                    return View("Index", user);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile - UserId: {UserId}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                TempData["Error"] = "An error occurred while updating your profile. Please try again.";
                return RedirectToAction("Index");
            }
        }

    }
}
