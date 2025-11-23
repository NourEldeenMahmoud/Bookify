using Bookify.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Interfaces
{
    public interface IUserProfileService
    {
        Task<ApplicationUser?> GetUserProfileAsync(string userId);
        Task<bool> UpdateUserProfileAsync(string userId, string? firstName, string? lastName, DateTime? dateOfBirth, string? address, string? city,  string? postalCode, string? country);
        Task<IEnumerable<Booking>> GetUserBookingsAsync(string userId);
        Task<IEnumerable<Booking>> GetUpcomingBookingsAsync(string userId);
        Task<IEnumerable<Booking>> GetPastBookingsAsync(string userId);
    }
}
