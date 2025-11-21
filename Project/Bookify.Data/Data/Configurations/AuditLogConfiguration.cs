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
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    { 
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
            builder.Property(a => a.Description).HasMaxLength(500);
            builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
            builder.Property(a => a.ChangedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            builder.Property(a => a.ChangedByUserId).IsRequired();
            builder.Property(a => a.Changes).HasColumnType("nvarchar(max)");
        }
    }
}
