using System;

namespace Bookify.Services.DTOs;

public class CartItemDto
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int MaxOccupancy { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int NumberOfGuests { get; set; }
    
    // Calculated properties
    public int NumberOfNights => (CheckOut - CheckIn).Days;
    public decimal Subtotal => PricePerNight * NumberOfNights;
}

