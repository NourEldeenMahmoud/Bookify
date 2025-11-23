using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Bookify.Data.Repositories;
using Bookify.Services.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Bookify.Services.Services
{
    public class ReservationService : IReservationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger _logger;
        private readonly IRoomAvailabilityService _roomAvailabilityService;

        public ReservationService(IUnitOfWork unitOfWork , IRoomAvailabilityService roomAvailabilityService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = Log.ForContext<ReservationService>();
            _roomAvailabilityService = roomAvailabilityService ?? throw new ArgumentNullException(nameof(roomAvailabilityService));
        }
        public async Task<decimal> CalculateTotalAmountAsync(int roomId, DateTime checkIn, DateTime checkOut)
        {
            try
            {
                _logger.Information("Calculating total amount for Room ID: {RoomId} from {CheckIn} to {CheckOut}", roomId, checkIn, checkOut);
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

                var room = await _unitOfWork.Rooms.GetRoomDetailsAsync(roomId);

                if (room == null)
                {
                    _logger.Warning("Room with ID {RoomId} not found", roomId);
                    throw new ArgumentException("Room not found", nameof(roomId));
                }
                var totalNights = (checkOut - checkIn).Days;
                var totalAmount = room.RoomType.PricePerNight * totalNights;
                _logger.Debug("Calculated total amount: {TotalAmount} for {Nights} nights at {PricePerNight} per night",totalAmount, totalNights, room.RoomType.PricePerNight);

                return totalAmount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error calculating total amount for Room ID: {RoomId} from {CheckIn} to {CheckOut}", roomId, checkIn, checkOut);
                throw;
            }
        }

        public async Task<bool> CancelReservationAsync(int bookingId, string userId)
        {
            try
            {
                _logger.Information("Cancelling reservation - BookingId: {BookingId}, UserId: {UserId}", bookingId, userId);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    _logger.Warning("Invalid userId provided: {UserId}", userId);
                    throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
                }

                if (bookingId<=0)
                {
                    _logger.Warning("Invalid bookingId provided: {BookingId}", bookingId);
                    throw new ArgumentException("BookingId must be greater than zero", nameof(bookingId));
                }

                var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingId);
                if (booking == null || booking.UserId != userId)
                {
                    _logger.Warning("Booking not found or does not belong to user - BookingId: {BookingId}, UserId: {UserId}", bookingId, userId);
                    throw new ArgumentException("Booking not found or does not belong to user");
                }
                if (booking.Status == BookingStatus.Cancelled)
                {
                    _logger.Warning("Booking {BookingId} is already cancelled", bookingId);
                    return false;
                }

                if (booking.Status == BookingStatus.Completed)
                {
                    _logger.Warning("Cannot cancel completed booking {BookingId}", bookingId);
                    return false;
                }

                // Begin transaction for atomic operation
                await _unitOfWork.BeginTransactionAsync();
                
                try
                {
                    var statusHistory = new BookingStatusHistory
                    {
                        BookingId = booking.Id,
                        PreviousStatus = booking.Status,
                        NewStatus = BookingStatus.Cancelled,
                        ChangedAt = DateTime.UtcNow,
                        ChangedByUserId = userId,
                        Notes = "Booking cancelled by the user"
                    };
                    
                    booking.Status = BookingStatus.Cancelled;
                    booking.UpdatedAt = DateTime.UtcNow;
                    
                    await _unitOfWork.BookingStatusHistory.AddAsync(statusHistory);
                    _unitOfWork.Bookings.Update(booking);
                    await _unitOfWork.CommitTransactionAsync();
                    
                    _logger.Debug("Successfully cancelled reservation - BookingId: {BookingId}, UserId: {UserId}", bookingId, userId);
                    return true;
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error cancelling reservation - BookingId: {BookingId}, UserId: {UserId}", bookingId, userId);
                throw;
            }
        }

        public async Task<Booking> CreateReservationAsync(string userId, int roomId, DateTime checkIn, DateTime checkOut, int numberOfGuests, string? specialRequests = null)
        {
            try
            {
                //input validation
                if (string.IsNullOrWhiteSpace(userId))
                {
                    _logger.Warning("Invalid userId provided: {UserId}", userId);
                    throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
                }
                if (roomId<=0)
                {
                    _logger.Warning("Invalid roomId provided: {RoomId}", roomId);
                    throw new ArgumentException("RoomId must be greater than zero", nameof(roomId));
                }
                if (numberOfGuests <= 0)
                {
                    _logger.Warning("Invalid numberOfGuests provided: {NumberOfGuests}", numberOfGuests);
                    throw new ArgumentException("Number of guests must be greater than zero", nameof(numberOfGuests));
                }
                if (checkIn >= checkOut)
                {
                    _logger.Warning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", checkIn, checkOut);
                    throw new ArgumentException("Check-in date must be before check-out date.", nameof(checkIn));
                }

                if (checkIn < DateTime.UtcNow)
                {
                    _logger.Warning("Check-in date is in the past: {CheckIn}", checkIn);
                    throw new ArgumentException("Check-in date cannot be in the past.", nameof(checkIn));
                }

                //check room availability
                _logger.Debug("Checking room availability for RoomId: {RoomId}", roomId);
                
                var isAvailable = await _roomAvailabilityService.CheckRoomAvailabilityAsync(roomId, checkIn, checkOut);
                if (!isAvailable)
                {
                    _logger.Warning("Room {RoomId} is not available for dates {CheckIn} to {CheckOut}", roomId, checkIn, checkOut);
                    throw new InvalidOperationException("Room is not available for the selected dates.");
                }
                //check cappacity
                var room =  await _unitOfWork.Rooms.GetRoomDetailsAsync(roomId);
                if (room == null)
                {
                    _logger.Warning("Room with ID {RoomId} not found", roomId);
                    throw new ArgumentException("Room not found", nameof(roomId));
                }
                if (numberOfGuests > room.RoomType.MaxOccupancy)
                {
                    _logger.Warning("Number of guests {NumberOfGuests} exceeds room capacity {MaxOccupancy} for RoomId: {RoomId}", numberOfGuests, room.RoomType.MaxOccupancy, roomId);
                    throw new InvalidOperationException("Number of guests exceeds room capacity.");
                }
                var totalAmount = await CalculateTotalAmountAsync(roomId, checkIn, checkOut);
                
                // Begin transaction for atomic operation
                await _unitOfWork.BeginTransactionAsync();
                
                try
                {
                    var newBooking = new Booking
                    {
                        UserId = userId,
                        RoomId = roomId,
                        CheckInDate = checkIn,
                        CheckOutDate = checkOut,
                        NumberOfGuests = numberOfGuests,
                        SpecialRequests = specialRequests,
                        TotalAmount = totalAmount,
                        Status = BookingStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                    };

                    await _unitOfWork.Bookings.AddAsync(newBooking);
                    await _unitOfWork.SaveChangesAsync(); // Save to get the booking ID

                    var statusHistory = new BookingStatusHistory
                    {
                        BookingId = newBooking.Id,
                        PreviousStatus = BookingStatus.Pending,
                        NewStatus = BookingStatus.Pending,
                        ChangedAt = DateTime.UtcNow,
                        ChangedByUserId = userId,
                        Notes = "Booking created"
                    };

                    await _unitOfWork.BookingStatusHistory.AddAsync(statusHistory);
                    await _unitOfWork.CommitTransactionAsync();

                    _logger.Debug("Successfully created reservation - UserId: {UserId}, RoomId: {RoomId}, BookingId: {BookingId}", userId, roomId, newBooking.Id);
                    return newBooking;
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating reservation - UserId: {UserId}, RoomId: {RoomId}", userId, roomId);
                throw;
            }
        }
    }
}
