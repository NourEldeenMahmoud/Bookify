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
    public class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
    {
        public void Configure(EntityTypeBuilder<RoomType> builder)
        {
            builder.ToTable("RoomTypes");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);
            builder.Property(p => p.Description).HasMaxLength(500);
            builder.Property(p => p.PricePerNight)
                .IsRequired()
                .HasPrecision(18, 2);
            builder.Property(p=>p.MaxOccupancy).IsRequired();
            builder.Property(p => p.RowVersion).IsRowVersion().IsConcurrencyToken();

            builder.HasIndex(p => p.Name).IsUnique();


        }
    }
}
