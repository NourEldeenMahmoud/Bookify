using System;
using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.ViewModels;

public class CartItemViewModel
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int MaxOccupancy { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    
    [Required(ErrorMessage = "Check-in date is required")]
    [DataType(DataType.Date)]
    public DateTime CheckIn { get; set; }
    
    [Required(ErrorMessage = "Check-out date is required")]
    [DataType(DataType.Date)]
    public DateTime CheckOut { get; set; }
    
    [Required(ErrorMessage = "Number of guests is required")]
    [Range(1, 20, ErrorMessage = "Number of guests must be between 1 and 20")]
    public int NumberOfGuests { get; set; }
    
    // Calculated properties
    public int NumberOfNights => (CheckOut - CheckIn).Days;
    public decimal Subtotal => PricePerNight * NumberOfNights;
}

