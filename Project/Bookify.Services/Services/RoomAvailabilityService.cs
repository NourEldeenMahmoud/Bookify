using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Bookify.Services.Services
{
    public class RoomAvailabilityService : IRoomAvailabilityService
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly ILogger _logger;

        public RoomAvailabilityService(UnitOfWork unitOfWork,ILogger logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = Log.ForContext<RoomAvailabilityService>();
        }
        public async Task<bool> CheckRoomAvailabilityAsync(int roomId, DateTime checkIn, DateTime checkOut)
        {
            try
            {
                _logger.Information("Checking availability for room ID: {RoomId} from {CheckIn} to {CheckOut}", roomId, checkIn, checkOut);

                if (roomId <= 0)
                {
                    _logger.Warning("Invalid roomId provided: {RoomId}", roomId);
                    throw new ArgumentException("RoomId must be greater than zero", nameof(roomId));
                }

                if (checkIn >= checkOut)
                {
                    _logger.Warning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", checkIn, checkOut);
                    throw new ArgumentException("Check-in date must be before check-out date", nameof(checkIn));
                }

                var isAvailable = await _unitOfWork.Rooms.IsRoomAvailableAsync(roomId, checkIn, checkOut);
                _logger.Debug("Room {RoomId} availability check result: {IsAvailable}", roomId, isAvailable);
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking room availability - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}",
                    roomId, checkIn, checkOut);
                throw;
            }

        }

        public async Task<IEnumerable<Room>> GetAvailableRoomsAsync(DateTime checkIn, DateTime checkOut, int? roomTypeId = null, int? minCapacity = null)
        {
            try
            {
                _logger.Information("Getting available rooms - CheckIn: {CheckIn}, CheckOut: {CheckOut}, RoomTypeId: {RoomTypeId}, MinCapacity: {MinCapacity}", checkIn, checkOut, roomTypeId, minCapacity);
                if (checkIn>checkOut)
                {
                    _logger.Warning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", checkIn, checkOut);
                    throw new ArgumentException("Check-in date must be before check-out date", nameof(checkIn));
                }
                if (roomTypeId.HasValue && roomTypeId.Value <= 0)
                {
                    _logger.Warning("Invalid roomTypeId provided: {RoomTypeId}", roomTypeId);
                    throw new ArgumentException("RoomTypeId must be greater than zero", nameof(roomTypeId));
                }

                if (minCapacity.HasValue && minCapacity.Value <= 0)
                {
                    _logger.Warning("Invalid minCapacity provided: {MinCapacity}", minCapacity);
                    throw new ArgumentException("MinCapacity must be greater than zero", nameof(minCapacity));
                }

                var availableRooms = await _unitOfWork.Rooms.GetAvailableRoomsAsync(checkIn, checkOut);

                if (roomTypeId.HasValue)
                {
                    availableRooms = availableRooms.Where(r => r.RoomTypeId == roomTypeId.Value);
                }

                if (minCapacity.HasValue)
                {
                    availableRooms = availableRooms.Where(r => r.RoomType!.MaxOccupancy >= minCapacity.Value);
                }

                var result = availableRooms.ToList();
                _logger.Debug("Found {Count} available rooms for dates {CheckIn} to {CheckOut}", result.Count, checkIn, checkOut);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting available rooms - CheckIn: {CheckIn}, CheckOut: {CheckOut}", checkIn, checkOut);
                throw;
            }
        }

        public async Task<IEnumerable<RoomType>> GetAvailableRoomTypesAsync(DateTime checkIn, DateTime checkOut, int? minCapacity = null)
        {
            try
            {
                _logger.Information("Getting available room types - CheckIn: {CheckIn}, CheckOut: {CheckOut}, MinCapacity: {MinCapacity}", checkIn, checkOut, minCapacity);
                if (checkIn > checkOut)
                {
                    _logger.Warning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", checkIn, checkOut);
                    throw new ArgumentException("Check-in date must be before check-out date", nameof(checkIn));
                }
                if (minCapacity.HasValue && minCapacity.Value <= 0)
                {
                    _logger.Warning("Invalid minCapacity provided: {MinCapacity}", minCapacity);
                    throw new ArgumentException("MinCapacity must be greater than zero", nameof(minCapacity));
                }
                var availableRooms = await _unitOfWork.Rooms.GetAvailableRoomsAsync(checkIn, checkOut);
                if (minCapacity.HasValue)
                {
                    availableRooms.Where(r => r.RoomType!.MaxOccupancy >= minCapacity.Value);
                }
                var result = availableRooms
                    .Select(r => r.RoomType)
                    .Distinct()
                    .ToList();

                _logger.Debug("Found {Count} available room types for dates {CheckIn} to {CheckOut}", result.Count, checkIn, checkOut);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting available room types - CheckIn: {CheckIn}, CheckOut: {CheckOut}", checkIn, checkOut);
                throw;
            }
        }
    }
}
