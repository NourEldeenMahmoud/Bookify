using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Models
{
    public class RoomType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g., Single, Double, Suite
        public string? Description { get; set; }
        public decimal PricePerNight { get; set; }
        public int MaxOccupancy { get; set; }
        public string? ImageUrl { get; set; } = string.Empty;
        public byte[]? RowVersion { get; set; }


        // Navigation property
        public ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
