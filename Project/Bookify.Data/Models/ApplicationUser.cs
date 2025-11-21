using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Models
{
    public class ApplicationUser:IdentityUser
    {
        public string? FirstName { get; set; } 
        public string? LastName { get; set; } 
        public DateTime DateOfBirth { get; set; } 
        public string? Address { get; set; } 
        public string? City { get; set; } 
        public string? PostalCode { get; set; } 
        public string? Country { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation Properties
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public virtual ICollection<BookingStatusHistory> BookingStatusHistory { get; set; } = new List<BookingStatusHistory>();

    }
}
