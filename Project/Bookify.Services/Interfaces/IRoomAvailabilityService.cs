using Bookify.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Interfaces
{
    public interface IRoomAvailabilityService
    {
        public Task<IEnumerable<Room>> GetAvailableRoomsAsync(DateTime checkIn, DateTime checkOut, int? roomTypeId = null, int? minCapacity = null);
        public Task<bool> CheckRoomAvailabilityAsync(int roomId, DateTime checkIn, DateTime checkOut);
        public Task<IEnumerable<RoomType>> GetAvailableRoomTypesAsync(DateTime checkIn, DateTime checkOut, int? minCapacity = null);
    }
}
