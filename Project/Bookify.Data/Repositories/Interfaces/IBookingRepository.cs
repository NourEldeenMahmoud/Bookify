using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Repositories.Interfaces
{
    public interface IBookingRepository : IRepository<Booking>
    {
        public Task<IEnumerable<Booking>> GetUserBookingsById(string id);
        public Task<IEnumerable<Booking>> GetBookingsByRoomAsync(int roomId);
        public Task<IEnumerable<Booking>> GetBookingsByStatusAsync(BookingStatus status);
        public Task<Booking?> GetBookingWithDetailsAsync(int id);
        public Task<IEnumerable<Booking>> GetOverlappingBookingsAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null);

    }
}
