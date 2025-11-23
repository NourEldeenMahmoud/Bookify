namespace Bookify.Web.Models
{
    public class RoomSearchViewModel
    {
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public int? RoomTypeId { get; set; }
        public int? MinCapacity { get; set; }
        public decimal? MaxPrice { get; set; }

    }

}