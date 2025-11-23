using System.ComponentModel.DataAnnotations;

namespace Bookify.Web.Models
{
    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password),ErrorMessage ="Password Doesn't match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        public string FirstName {  get; set; }

        [Required]
        public string LastName { get; set; }

       
    }
}
