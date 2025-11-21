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
    public class GalleryImageConfiguration : IEntityTypeConfiguration<GalleryImage>
    {
        public void Configure(EntityTypeBuilder<GalleryImage> builder)
        {
            builder.ToTable("GalleryImages");
            builder.HasKey(g => g.Id);
            builder.Property(g => g.ImageUrl)
                .IsRequired();
            builder.Property(g => g.RoomId)
                .IsRequired();
            builder.Property(g => g.Description)
                .HasMaxLength(500);
            builder.Property(g => g.AltText)
                .HasMaxLength(250);

            builder.HasOne(g => g.Room)
                .WithMany(r => r.GalleryImages)
                .HasForeignKey(g => g.RoomId)
                .OnDelete(DeleteBehavior.Cascade);


        }
    }
}
