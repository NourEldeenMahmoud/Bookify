using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Bookify.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Bookify.Data.Data;

namespace Bookify.Data.Repositories.Implementations
{
    public class RoomRepository : Repository<Room>, IRoomRepository
    {
        private new readonly ILogger _logger;
        public RoomRepository(AppDbContext Context) : base(Context)
        {
            _logger = Log.ForContext<RoomRepository>();
        }

        public async Task<IEnumerable<Room>> GetAllRoomsWithRoomTypeAsync()
        {
            try
            {
                _logger.Information("Getting all rooms with room type");
                var result = await _dbSet
                .Include(r => r.RoomType)
                .ToListAsync();
                _logger.Debug("Retrieved {Count} rooms with room type", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting all rooms with room type");
                throw;
            }
        }

        public async Task<IEnumerable<Room>> GetAvailableRoomsAsync(DateTime CheckIn, DateTime CheckOut)
        {
            try
            {
                _logger.Information("Getting available rooms - CheckIn: {CheckIn}, CheckOut: {CheckOut}", CheckIn, CheckOut);

                if (CheckIn >= CheckOut)
                {
                    _logger.Warning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", CheckIn, CheckOut);
                    throw new ArgumentException("CheckIn date must be before CheckOut date", nameof(CheckIn));
                }
                var result= await _dbSet
            .Include(r => r.RoomType)
            .Include(r => r.GalleryImages)
            .Where(r => r.IsAvailable &&!r.Bookings
            .Any(b => b.Status != BookingStatus.Cancelled &&
                       ((b.CheckInDate <= CheckIn && b.CheckOutDate > CheckIn) ||
                        (b.CheckInDate < CheckOut && b.CheckOutDate >= CheckOut) ||
                        (b.CheckInDate >= CheckIn && b.CheckOutDate <= CheckOut))))
            .ToListAsync();
                _logger.Debug("Found {Count} available rooms for dates {CheckIn} to {CheckOut}", result.Count, CheckIn, CheckOut);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting available rooms - CheckIn: {CheckIn}, CheckOut: {CheckOut}", CheckIn, CheckOut);
                throw;
            }
        }

        public async Task<Room?> GetRoomDetailsAsync(int id)
        {
            try
            {
                _logger.Information("Getting room with details for ID: {RoomId}", id);

                if (id <= 0)
                {
                    _logger.Warning("Invalid room ID provided: {RoomId}", id);
                    throw new ArgumentException("Room ID must be greater than zero", nameof(id));
                }

                var result = await _dbSet
                .Include(r => r.RoomType)
                .Include(r => r.GalleryImages)
                .FirstOrDefaultAsync(r => r.Id == id);
                if (result == null)
                    _logger.Warning("Room with ID {RoomId} not found", id);
                else
                    _logger.Debug("Successfully retrieved room {RoomId} with all details", id);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting room with details for ID {RoomId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Room>> GetRoomsByTypeAsync(int roomTypeId)
        {
            try
            {
                _logger.Information("Getting rooms by type: {RoomTypeId}", roomTypeId);

                if (roomTypeId <= 0)
                {
                    _logger.Warning("Invalid roomTypeId provided: {RoomTypeId}", roomTypeId);
                    throw new ArgumentException("RoomTypeId must be greater than zero", nameof(roomTypeId));
                }

                var result = await _dbSet.Where(rt=> rt.RoomTypeId == roomTypeId)
               .Include(r => r.RoomType)
               .Include(r => r.GalleryImages)
               .ToListAsync();
                _logger.Debug("Retrieved {Count} rooms for type {RoomTypeId}", result.Count, roomTypeId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting rooms by type {RoomTypeId}", roomTypeId);
                throw;
            }
        }

        public async Task<bool> IsRoomAvailableAsync(int roomId, DateTime checkIn, DateTime checkOut)
        {
            try
            {
                _logger.Information("Checking room availability - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}",
                    roomId, checkIn, checkOut);

                if (roomId <= 0)
                {
                    _logger.Warning("Invalid roomId provided: {RoomId}", roomId);
                    throw new ArgumentException("RoomId must be greater than zero", nameof(roomId));
                }

                if (checkIn >= checkOut)
                {
                    _logger.Warning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", checkIn, checkOut);
                    throw new ArgumentException("CheckIn date must be before CheckOut date", nameof(checkIn));
                }

                var room = await _dbSet
                    .Include(r => r.Bookings)
                    .FirstOrDefaultAsync(r => r.Id == roomId);

                if (room == null)
                {
                    _logger.Warning("Room with ID {RoomId} not found", roomId);
                    return false;
                }

                if (!room.IsAvailable)
                {
                    _logger.Debug("Room {RoomId} is marked as unavailable", roomId);
                    return false;
                }

                var hasOverlappingBookings = room.Bookings.Any(b =>
                    b.Status != BookingStatus.Cancelled &&
                    ((b.CheckInDate <= checkIn && b.CheckOutDate > checkIn) ||
                     (b.CheckInDate < checkOut && b.CheckOutDate >= checkOut) ||
                     (b.CheckInDate >= checkIn && b.CheckOutDate <= checkOut)));

                var isAvailable = !hasOverlappingBookings;
                _logger.Debug("Room {RoomId} availability check result: {IsAvailable}", roomId, isAvailable);

                return isAvailable;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking room availability - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}",
                    roomId, checkIn, checkOut);
                throw;
            }
        }

    }
}
