using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.ViewModels;

public class ReservationCartItem : IValidatableObject
{
    [Required(ErrorMessage = "Room ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Room ID must be greater than zero.")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Room number is required.")]
    [StringLength(50, ErrorMessage = "Room number must be at most {1} characters long.")]
    public string RoomNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Room type name is required.")]
    [StringLength(100, ErrorMessage = "Room type name must be at most {1} characters long.")]
    public string RoomTypeName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Check-in date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Check-in Date")]
    public DateTime CheckInDate { get; set; }

    [Required(ErrorMessage = "Check-out date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Check-out Date")]
    public DateTime CheckOutDate { get; set; }

    [Required(ErrorMessage = "Number of guests is required.")]
    [Range(1, 20, ErrorMessage = "Number of guests must be between {1} and {2}.")]
    [Display(Name = "Number of Guests")]
    public int NumberOfGuests { get; set; }

    [Required(ErrorMessage = "Total amount is required.")]
    [Range(0, 100000, ErrorMessage = "Total amount must be between {1} and {2}.")]
    [Display(Name = "Total Amount")]
    [DataType(DataType.Currency)]
    public decimal TotalAmount { get; set; }

    [Required(ErrorMessage = "Added date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Added At")]
    public DateTime AddedAt { get; set; }

    // Custom validation method
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validate date range
        if (CheckInDate >= CheckOutDate)
        {
            results.Add(new ValidationResult(
                "Check-in date must be before check-out date.",
                new[] { nameof(CheckInDate), nameof(CheckOutDate) }));
        }

        // validate check-in date is not in the past (with -1 days for timezone)
        if (CheckInDate < DateTime.Today.AddDays(-1))
        {
            results.Add(new ValidationResult(
                "Check-in date cannot be more than 1 day in the past.",
                new[] { nameof(CheckInDate) }));
        }

        // validate stay duration (max 30 nights)
        var nights = (CheckOutDate - CheckInDate).Days;
        if (nights > 30)
        {
            results.Add(new ValidationResult(
                "Maximum stay duration is 30 nights.",
                new[] { nameof(CheckOutDate) }));
        }

        // Validate that AddedAt is not in the future
        if (AddedAt > DateTime.UtcNow.AddMinutes(5))
        {
            results.Add(new ValidationResult(
                "Added date cannot be in the future.",
                new[] { nameof(AddedAt) }));
        }

        return results;
    }
}

