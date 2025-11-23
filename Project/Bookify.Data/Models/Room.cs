using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Models
{
    public class Room
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int RoomTypeId { get; set; } 
        public bool IsAvailable { get; set; }
        public string Notes { get; set; } = string.Empty;
        public byte[]? RowVersion { get; set; }


        // Navigation Properties
        public RoomType RoomType { get; set; } = null!;
        public ICollection<GalleryImage> GalleryImages { get; set; } = new List<GalleryImage>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
