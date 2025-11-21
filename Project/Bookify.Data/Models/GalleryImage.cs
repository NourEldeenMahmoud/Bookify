using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Models
{
    public class GalleryImage
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public string Description { get; set; } = string.Empty;


        // Navigation Property
        public Room? Room { get; set; }
    }
}
