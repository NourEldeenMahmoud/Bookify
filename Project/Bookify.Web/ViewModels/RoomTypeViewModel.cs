using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.ViewModels;

public class RoomTypeViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    [Display(Name = "Room Type Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Price per night is required")]
    [Range(100, double.MaxValue, ErrorMessage = "Price per night must be at least 100")]
    [Display(Name = "Price Per Night")]
    [DataType(DataType.Currency)]
    public decimal PricePerNight { get; set; }

    [Required(ErrorMessage = "Max occupancy is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Max occupancy must be at least 1")]
    [Display(Name = "Max Occupancy")]
    public int MaxOccupancy { get; set; }

    [Display(Name = "Current Image")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Upload New Image")]
    public IFormFile? ImageFile { get; set; }
}
