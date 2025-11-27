using Bookify.Data.Data;
using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Data.Configurations
{
    public class BookingConfiguration:IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("Bookings");
            builder.HasKey(b => b.Id);
            builder.Property(p=>p.RoomId).IsRequired();
            builder.Property(p => p.UserId).IsRequired();
            builder.Property(p => p.CheckInDate).IsRequired();
            builder.Property(p => p.CheckOutDate).IsRequired();
            builder.Property(p => p.NumberOfGuests).IsRequired().HasDefaultValue(1);
            builder.Property(p => p.TotalAmount).IsRequired().HasPrecision(18,2);
            builder.Property(p => p.Status).IsRequired().HasConversion<string>().HasDefaultValue(BookingStatus.Pending);
            builder.Property(p => p.SpecialRequests).HasMaxLength(500);
            builder.Property(p => p.CreatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            builder.Property(p => p.RowVersion).IsRowVersion().IsConcurrencyToken();

            builder.HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(b => b.Room)
                .WithMany(r => r.Bookings)
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => new { e.RoomId, e.CheckInDate, e.CheckOutDate });

            // Note: SeedBookings is called separately after users are created
            // Use: await SeedData.SeedBookingsAsync(context);
        }
    }
}
