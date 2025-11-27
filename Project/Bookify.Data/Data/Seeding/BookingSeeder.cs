using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Data.Seeding
{
    public static class BookingSeeder
    {
        private static readonly ILogger _logger = Log.ForContext(typeof(BookingSeeder));

        public static async Task SeedBookingsAsync(AppDbContext context, string customerEmail = "customer@bookify.com")
        {
            if (await context.Bookings.AnyAsync())
                return;

            var customerUser = await context.Users.FirstOrDefaultAsync(u => u.Email == customerEmail);
            if (customerUser == null)
            {
                _logger.Warning("User with email '{CustomerEmail}' not found. Skipping booking seeding.", customerEmail);
                return;
            }

            // Optional: load existing room ids to ensure FKs are valid
            var existingRoomIds = await context.Rooms.Select(r => r.Id).ToListAsync();

            var now = DateTime.UtcNow;
            var desiredBookings = new List<Booking>
        {
            new Booking
            {
                // Id can be omitted if DB generates it; keep it only if you need fixed Ids
                UserId = customerUser.Id,
                RoomId = 6,
                CheckInDate = now.AddDays(5),
                CheckOutDate = now.AddDays(8),
                NumberOfGuests = 2,
                TotalAmount = 2400.00m,
                Status = BookingStatus.Pending,
                SpecialRequests = "Late check-in requested",
                CreatedAt = now.AddDays(-2),
                UpdatedAt = null
            },
            new Booking
            {
                UserId = customerUser.Id,
                RoomId = 11,
                CheckInDate = now.AddDays(10),
                CheckOutDate = now.AddDays(13),
                NumberOfGuests = 2,
                TotalAmount = 4500.00m,
                Status = BookingStatus.Paid,
                SpecialRequests = "Anniversary celebration",
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-4)
            },
            new Booking
            {
                UserId = customerUser.Id,
                RoomId = 15,
                CheckInDate = now.AddDays(-10),
                CheckOutDate = now.AddDays(-7),
                NumberOfGuests = 4,
                TotalAmount = 3600.00m,
                Status = BookingStatus.Completed,
                SpecialRequests = "Extra beds needed",
                CreatedAt = now.AddDays(-15),
                UpdatedAt = now.AddDays(-7)
            },
            new Booking
            {
                UserId = customerUser.Id,
                RoomId = 8,
                CheckInDate = now.AddDays(15),
                CheckOutDate = now.AddDays(18),
                NumberOfGuests = 2,
                TotalAmount = 2400.00m,
                Status = BookingStatus.Pending,
                SpecialRequests = null,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = null
            },
            new Booking
            {
                UserId = customerUser.Id,
                RoomId = 18,
                CheckInDate = now.AddDays(20),
                CheckOutDate = now.AddDays(23),
                NumberOfGuests = 2,
                TotalAmount = 9000.00m,
                Status = BookingStatus.Cancelled,
                SpecialRequests = "Cancelled due to change of plans",
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddDays(-3)
            }
        };

            // Filter out bookings that reference non-existing rooms (prevents FK failures)
            var bookingsToAdd = desiredBookings
                .Where(b =>
                {
                    if (!existingRoomIds.Contains(b.RoomId))
                    {
                        _logger.Warning("Room with Id {RoomId} not found — skipping booking Id {BookingId}.", b.RoomId, b.Id);
                        return false;
                    }
                    return true;
                })
                .ToList();

            if (!bookingsToAdd.Any())
            {
                _logger.Warning("No valid bookings to add after validation.");
                return;
            }

            await context.Bookings.AddRangeAsync(bookingsToAdd);
            await context.SaveChangesAsync();

            _logger.Information("Seeded {Count} bookings.", bookingsToAdd.Count);
        }
    }

}
