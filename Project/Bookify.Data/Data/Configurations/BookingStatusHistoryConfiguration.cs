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
    public class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
    {
        public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
        {
            builder.ToTable("BookingStatusHistory");
            builder.HasKey(bsh => bsh.Id);
            builder.Property(bsh => bsh.BookingId).IsRequired();
            builder.Property(bsh => bsh.PreviousStatus).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Property(bsh => bsh.NewStatus).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Property(bsh => bsh.ChangedByUserId).IsRequired();
            builder.Property(bsh => bsh.ChangedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");

            builder.HasOne(bsh => bsh.Booking)
                .WithMany()
                .HasForeignKey(bsh => bsh.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(bsh => bsh.ChangedByUser)
                .WithMany()
                .HasForeignKey(bsh => bsh.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

        }
    
    }
}
