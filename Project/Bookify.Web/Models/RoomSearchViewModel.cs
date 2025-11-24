using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.ViewModels;

public class RoomSearchViewModel
{
    [DataType(DataType.Date)]
    [Display(Name = "Check-in Date")]
    [CustomValidation(typeof(RoomSearchViewModel), "ValidateCheckInDate")]
    public DateTime? CheckInDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Check-out Date")]
    [CustomValidation(typeof(RoomSearchViewModel), "ValidateCheckOutDate")]
    public DateTime? CheckOutDate { get; set; }

    [Display(Name = "Room Type")]
    public int? RoomTypeId { get; set; }

    [Range(1, 20, ErrorMessage = "Minimum capacity must be between {1} and {2}.")]
    [Display(Name = "Minimum Capacity")]
    public int? MinCapacity { get; set; }

    [Range(0, 10000, ErrorMessage = "Maximum price must be between {1} and {2}.")]
    [Display(Name = "Maximum Price (per night)")]
    public decimal? MaxPrice { get; set; }

    public static ValidationResult? ValidateCheckInDate(DateTime? checkInDate, ValidationContext context)
    {
        var instance = context.ObjectInstance as RoomSearchViewModel;

        if (checkInDate.HasValue && checkInDate.Value < DateTime.Today)
        {
            return new ValidationResult("Check-in date cannot be in the past.");
        }

        if (checkInDate.HasValue && instance?.CheckOutDate.HasValue == true)
        {
            if (checkInDate.Value >= instance.CheckOutDate.Value)
            {
                return new ValidationResult("Check-in date must be before check-out date.");
            }
        }

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateCheckOutDate(DateTime? checkOutDate, ValidationContext context)
    {
        var instance = context.ObjectInstance as RoomSearchViewModel;

        if (checkOutDate.HasValue && instance?.CheckInDate.HasValue == true)
        {
            if (instance.CheckInDate.Value >= checkOutDate.Value)
            {
                return new ValidationResult("Check-out date must be after check-in date.");
            }

            var nights = (checkOutDate.Value - instance.CheckInDate.Value).Days;
            if (nights > 30)
            {
                return new ValidationResult("Maximum stay duration is 30 nights.");
            }
        }

        return ValidationResult.Success;
    }
}

