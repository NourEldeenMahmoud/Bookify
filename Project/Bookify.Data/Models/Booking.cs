using Bookify.Data.Data.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public int RoomId { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public int NumberOfGuests { get; set; }
        public decimal TotalAmount { get; set; }
        public BookingStatus Status { get; set; } 
        public string? SpecialRequests { get; set; }
        public DateTime CreatedAt { get; set; }= DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; } 
        public byte[]? RowVersion { get; set; }


        // Navigation Properties
        public ApplicationUser? User { get; set; } 
        public Room? Room { get; set; } 
        public ICollection<BookingStatusHistory> StatusHistories { get; set; } = new List<BookingStatusHistory>();
        public ICollection<BookingPayment> Payments { get; set; } = new List<BookingPayment>();

    }
}
