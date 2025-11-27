using Bookify.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Repositories.Interfaces
{
    public interface IRoomRepository : IRepository<Room>
    {
        public Task<IEnumerable<Room>> GetRoomsByTypeAsync(int RoomTypeId);
        public Task<IEnumerable<Room>> GetAvailableRoomsAsync(DateTime CheckIn,DateTime CheckOut);
        public Task<Room?> GetRoomDetailsAsync(int RoomId);
        public Task<bool> IsRoomAvailableAsync(int roomId, DateTime checkIn, DateTime checkOut);
        public Task<IEnumerable<Room>> GetAllRoomsWithRoomTypeAsync();

    }
}
