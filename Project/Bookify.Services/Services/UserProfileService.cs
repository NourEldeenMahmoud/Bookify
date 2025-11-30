using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger _logger = Log.ForContext<UserProfileService>();
        
        public UserProfileService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }
        public async Task<IEnumerable<Booking>> GetPastBookingsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId is required", nameof(userId));

            _logger.Information("Fetching past bookings for user with ID: {UserId}", userId);
            try
            {
                var bookings = await _unitOfWork.Bookings.GetUserBookingsById(userId);

                var pastBookings = bookings
                    .Where(b => b.CheckOutDate < DateTime.UtcNow && b.Status != BookingStatus.Completed)
                    .ToList();

                _logger.Debug("Successfully fetched {Count} past bookings for user with ID: {UserId}", pastBookings.Count(), userId);

                return pastBookings;
            }
            catch (Exception ex)
            {
                _logger.Error(ex,"Error occurred while fetching past bookings for user with ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<Booking>> GetUpcomingBookingsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId is required", nameof(userId));

            _logger.Information("Fetching Upcoming bookings for user with ID: {UserId}", userId);
            try
            {
                var bookings = await _unitOfWork.Bookings.GetUserBookingsById(userId);

                var Upcoming = bookings
                    .Where(b => b.CheckOutDate > DateTime.UtcNow && b.Status != BookingStatus.Cancelled)
                    .ToList();

                _logger.Debug("Successfully fetched {Count} Upcoming bookings for user with ID: {UserId}", Upcoming.Count(), userId);

                return Upcoming;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while fetching Upcoming bookings for user with ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<Booking>> GetUserBookingsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId is required", nameof(userId));

            _logger.Information("Fetching user bookings for user with ID: {UserId}", userId);
            try
            {
                var bookings = await _unitOfWork.Bookings.GetUserBookingsById(userId);

                _logger.Debug("Successfully fetched {Count} user bookings for user with ID: {UserId}", bookings.Count(), userId);

                return bookings;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while fetching user bookings for user with ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<ApplicationUser?> GetUserProfileAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId is required", nameof(userId));

            _logger.Information("Fetching User Profile for user with ID: {UserId}", userId);
            try
            {
                var UserProfile = await _userManager.FindByIdAsync(userId);


                _logger.Debug("Successfully fetched User Profile for user with ID: {UserId}",  userId);

                return UserProfile;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while fetching User Profile for user with ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateUserProfileAsync(string userId, string? firstName, string? lastName, DateTime? dateOfBirth, string? address, string? city,string? postalCode, string? country)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            _logger.Information("Updating User Profile for user with ID: {UserId}", userId);

            try
            {
                var userProfile = await _userManager.FindByIdAsync(userId);
                if (userProfile == null)
                {
                    _logger.Warning("User Profile not found for user with ID: {UserId}", userId);
                    return false;
                }

                userProfile.FirstName = firstName ?? userProfile.FirstName;
                userProfile.LastName = lastName ?? userProfile.LastName;

                // DateOfBirth: handle nullable
                if (dateOfBirth.HasValue)
                {
                    userProfile.DateOfBirth = dateOfBirth.Value;
                }

                userProfile.Address = address ?? userProfile.Address;
                userProfile.City = city ?? userProfile.City;
                userProfile.PostalCode = postalCode ?? userProfile.PostalCode;
                userProfile.Country = country ?? userProfile.Country;

                var result = await _userManager.UpdateAsync(userProfile);
                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while updating User Profile for user with ID: {UserId}", userId);
                throw;
            }

        }
    }
}
