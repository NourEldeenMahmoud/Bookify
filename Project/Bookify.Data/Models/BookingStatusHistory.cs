using Bookify.Data.Data.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Models
{
    public class BookingStatusHistory
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public BookingStatus PreviousStatus { get; set; }
        public BookingStatus NewStatus { get; set; }
        public string? Notes { get; set; }
        public string ChangedByUserId { get; set; }= string.Empty;
        public DateTime ChangedAt { get; set; }


        // Navigation Properties
        public Booking? Booking { get; set; } 
        public ApplicationUser? ChangedByUser { get; set; } 

    }
}
