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
    public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.ToTable("ApplicationUsers");

            builder.Property(p => p.FirstName).HasMaxLength(50);
            builder.Property(p => p.LastName).HasMaxLength(50);
            builder.Property(p=> p.Address).HasMaxLength(250);
            builder.Property(p => p.City).HasMaxLength(50);
            builder.Property(p => p.PostalCode).HasMaxLength(10);
            builder.Property(p => p.Country).HasMaxLength(50);

            builder.Property(p => p.CreatedAt)
                   .HasDefaultValueSql("GETDATE()")
                   .IsRequired();       
        }
    
    }
}
