using System;

namespace Bookify.Web.ViewModels
{
    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = "Customer";
        public int TotalBookings { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}

