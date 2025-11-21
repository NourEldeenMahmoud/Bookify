using Bookify.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Interfaces
{
    public interface IReservationService
    {
        Task<Booking> CreateReservationAsync(string userId, int roomId, DateTime checkIn, DateTime checkOut, int numberOfGuests, string? specialRequests = null);
        Task<bool> CancelReservationAsync(int bookingId, string userId);
        Task<decimal> CalculateTotalAmountAsync(int roomId, DateTime checkIn, DateTime checkOut);
    }
}
