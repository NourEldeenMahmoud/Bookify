using System;
using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.ViewModels;

public class CheckoutViewModel
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public string? RoomImageUrl { get; set; }
    public string? RoomDescription { get; set; }
    public int MaxOccupancy { get; set; }
    public List<string> IncludedServices { get; set; } = new();
    public List<string> Amenities { get; set; } = new();
    
    [Required(ErrorMessage = "Check-in date is required")]
    [DataType(DataType.Date)]
    public DateTime CheckIn { get; set; }
    
    [Required(ErrorMessage = "Check-out date is required")]
    [DataType(DataType.Date)]
    public DateTime CheckOut { get; set; }
    
    [Required(ErrorMessage = "Number of guests is required")]
    [Range(1, 20, ErrorMessage = "Number of guests must be between 1 and 20")]
    public int NumberOfGuests { get; set; }
    
    [MaxLength(500, ErrorMessage = "Special requests cannot exceed 500 characters")]
    public string? SpecialRequests { get; set; }
    
    // Pricing breakdown
    public decimal PricePerNight { get; set; }
    public int NumberOfNights { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; } = 0.14m; // 14% tax
    public decimal TaxAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    
    // Stripe
    public string? ClientSecret { get; set; }
    public string? StripePublishableKey { get; set; }
}

