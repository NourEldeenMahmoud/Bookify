using Bookify.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Data.Configurations
{
    public class BookingPaymentConfiguration: IEntityTypeConfiguration<BookingPayment>
    {
        public void Configure(EntityTypeBuilder<BookingPayment> builder)
        {
            builder.ToTable("BookingPayments");
            builder.HasKey(bp => bp.Id);
            builder.Property(bp => bp.BookingId).IsRequired();
            builder.Property(bp => bp.StripeSessionId).HasMaxLength(100);
            builder.Property(bp => bp.PaymentIntentId).HasMaxLength(100);
            builder.Property(bp => bp.Amount).IsRequired().HasPrecision(18, 2);
            builder.Property(bp => bp.TransactionDate).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            builder.Property(bp => bp.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("EGP");
            builder.Property(bp => bp.PaymentStatus).IsRequired().HasConversion<string>().HasMaxLength(50);


            builder.HasOne(bp => bp.Booking)
                .WithMany()
                .HasForeignKey(bp => bp.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(e => e.StripeSessionId).IsUnique();

        }
    }
}
