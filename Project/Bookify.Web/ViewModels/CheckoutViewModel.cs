using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.ViewModels;

public class CheckoutViewModel
{
    public List<CartItemViewModel> CartItems { get; set; } = new();
    public List<string> IncludedServices { get; set; } = new();
    public List<string> Amenities { get; set; } = new();
    
    [MaxLength(500, ErrorMessage = "Special requests cannot exceed 500 characters")]
    public string? SpecialRequests { get; set; }
    
    // Pricing breakdown (calculated from CartItems)
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; } = 0.14m; // 14% tax
    public decimal TaxAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    
    // Stripe
    public string? ClientSecret { get; set; }
    public string? StripePublishableKey { get; set; }
}

