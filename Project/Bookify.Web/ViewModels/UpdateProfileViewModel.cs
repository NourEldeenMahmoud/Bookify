using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.ViewModels;

public class UpdateProfileViewModel
{
    [StringLength(100, ErrorMessage = "First name must be at most {1} characters long.")]
    [Display(Name = "First Name")]
    public string? FirstName { get; set; }

    [StringLength(100, ErrorMessage = "Last name must be at most {1} characters long.")]
    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    [CustomValidation(typeof(UpdateProfileViewModel), "ValidateDateOfBirth")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(500, ErrorMessage = "Address must be at most {1} characters long.")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(100, ErrorMessage = "City must be at most {1} characters long.")]
    [Display(Name = "City")]
    public string? City { get; set; }

    [StringLength(50, ErrorMessage = "State must be at most {1} characters long.")]
    [Display(Name = "State/Province")]
    public string? State { get; set; }

    [StringLength(20, ErrorMessage = "Postal code must be at most {1} characters long.")]
    [Display(Name = "Postal Code")]
    public string? PostalCode { get; set; }

    [StringLength(100, ErrorMessage = "Country must be at most {1} characters long.")]
    [Display(Name = "Country")]
    public string? Country { get; set; }


    //custom validation method for DateOfBirth
    public static ValidationResult? ValidateDateOfBirth(DateTime? dateOfBirth, ValidationContext context)
    {
        if (dateOfBirth.HasValue && dateOfBirth.Value > DateTime.Today)
        {
            return new ValidationResult("Date of birth cannot be in the future.");
        }

        if (dateOfBirth.HasValue && dateOfBirth.Value < DateTime.Today.AddYears(-120))
        {
            return new ValidationResult("Date of birth is not valid.");
        }

        return ValidationResult.Success;
    }
}

