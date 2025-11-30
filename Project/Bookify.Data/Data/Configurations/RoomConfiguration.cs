using Bookify.Data.Data.Seeding;
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
    public class RoomConfiguration : IEntityTypeConfiguration<Room>
    {
        public void Configure(EntityTypeBuilder<Room> builder)
        {
            builder.ToTable("Rooms");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.RoomNumber)
                .IsRequired()
                .HasMaxLength(10);
            builder.Property(r => r.RoomTypeId)
                .IsRequired();
            builder.Property(r => r.Notes)
                .HasMaxLength(500);
            builder.Property(r => r.IsAvailable)
                .IsRequired()
                .HasDefaultValue(true);
            builder.Property(r => r.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            builder.HasOne(r => r.RoomType)
                .WithMany(rt => rt.Rooms)
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => r.RoomNumber).IsUnique();

            // Seed Rooms (Mock Data)
            builder.HasData(SeedData.SeedRooms());
        }
    }
}
