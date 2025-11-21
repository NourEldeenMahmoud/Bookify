using Bookify.Data.Data.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Models
{
    public class BookingPayment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string? StripeSessionId { get; set; } = null!;
        public string? PaymentIntentId { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Currency { get; set; } = null!;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;


        // Navigation Properties
        public Booking? Booking { get; set; }

    }
}
