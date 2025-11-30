using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.ViewModels;

public class RoomViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Room number is required")]
    [StringLength(10, ErrorMessage = "Room number cannot exceed 10 characters")]
    [Display(Name = "Room Number")]
    public string RoomNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Room type is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid room type")]
    [Display(Name = "Room Type")]
    public int RoomTypeId { get; set; }

    [Display(Name = "Available")]
    public bool IsAvailable { get; set; } = true;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}

