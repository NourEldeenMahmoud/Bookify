using System.Collections.Generic;
using System.Linq;

namespace Bookify.Web.ViewModels;

public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public decimal TaxRate { get; set; } = 0.14m; // 14% tax
    
    // Calculated properties
    public decimal Subtotal => Items.Sum(item => item.Subtotal);
    public decimal TaxAmount => Subtotal * TaxRate;
    public decimal TotalAmount => Subtotal + TaxAmount;
    
    public bool IsEmpty => !Items.Any();
    public int ItemCount => Items.Count;
}

