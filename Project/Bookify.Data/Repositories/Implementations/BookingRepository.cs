using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Bookify.Data.Repositories.Implementations;
using Bookify.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bookify.Data.Repositories;

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    private new readonly ILogger _logger;

    public BookingRepository(DbContext context) : base(context)
    {
        _logger = Log.ForContext<BookingRepository>();
    }

    public async Task<IEnumerable<Booking>> GetUserBookingsById(string userId)
    {
        try
        {
            _logger.Information("Getting bookings for user: {UserId}", userId);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.Warning("Invalid userId provided: {UserId}", userId);
                throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
            }

            var result = await _dbSet
                .Include(b => b.Room)
                    .ThenInclude(r => r!.RoomType)
                .Include(b => b.Payments)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            _logger.Debug("Retrieved {Count} bookings for user {UserId}", result.Count, userId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting bookings for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<Booking>> GetBookingsByRoomAsync(int roomId)
    {
        try
        {
            _logger.Information("Getting bookings for room: {RoomId}", roomId);

            if (roomId <= 0)
            {
                _logger.Warning("Invalid roomId provided: {RoomId}", roomId);
                throw new ArgumentException("RoomId must be greater than zero", nameof(roomId));
            }

            var result = await _dbSet
                .Include(b => b.User)
                .Include(b => b.Payments)
                .Where(b => b.RoomId == roomId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            _logger.Debug("Retrieved {Count} bookings for room {RoomId}", result.Count, roomId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting bookings for room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<IEnumerable<Booking>> GetBookingsByStatusAsync(BookingStatus status)
    {
        try
        {
            _logger.Information("Getting bookings with status: {Status}", status);

            var result = await _dbSet
                .Include(b => b.User)
                .Include(b => b.Room)
                    .ThenInclude(r => r!.RoomType)
                .Include(b => b.Payments)
                .Where(b => b.Status == status)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            _logger.Debug("Retrieved {Count} bookings with status {Status}", result.Count, status);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting bookings with status {Status}", status);
            throw;
        }
    }

    public async Task<Booking?> GetBookingWithDetailsAsync(int id)
    {
        try
        {
            _logger.Information("Getting booking with details for ID: {BookingId}", id);

            if (id <= 0)
            {
                _logger.Warning("Invalid booking ID provided: {BookingId}", id);
                throw new ArgumentException("Booking ID must be greater than zero", nameof(id));
            }

            var result = await _dbSet
                .Include(b => b.User)
                .Include(b => b.Room)
                    .ThenInclude(r => r!.RoomType)
                .Include(b => b.Payments)
                .Include(b => b.StatusHistory)
                    .ThenInclude(sh => sh.ChangedByUser)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (result == null)
                _logger.Warning("Booking with ID {BookingId} not found", id);
            else
                _logger.Debug("Successfully retrieved booking {BookingId} with all details", id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting booking with details for ID {BookingId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Booking>> GetOverlappingBookingsAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null)
    {
        try
        {
            _logger.Information("Checking for overlapping bookings - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}, ExcludeBookingId: {ExcludeBookingId}",
                roomId, checkIn, checkOut, excludeBookingId);

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

            var query = _dbSet
                .Where(b => b.RoomId == roomId &&
                       b.Status != BookingStatus.Cancelled &&
                       ((b.CheckInDate <= checkIn && b.CheckOutDate > checkIn) ||
                        (b.CheckInDate < checkOut && b.CheckOutDate >= checkOut) ||
                        (b.CheckInDate >= checkIn && b.CheckOutDate <= checkOut)));

            if (excludeBookingId.HasValue)
            {
                query = query.Where(b => b.Id != excludeBookingId.Value);
            }

            var result = await query.ToListAsync();
            _logger.Debug("Found {Count} overlapping bookings for room {RoomId}", result.Count, roomId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for overlapping bookings - RoomId: {RoomId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}",
                roomId, checkIn, checkOut);
            throw;
        }
    }
}

