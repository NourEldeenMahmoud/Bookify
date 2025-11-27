using Bookify.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace Bookify.Data.Data.Seeding
{
    public static class IdentitySeeder
    {
        public static async Task SeedUsersAndRolesAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // Seed Roles
            await EnsureRoleAsync(roleManager, "Admin");
            await EnsureRoleAsync(roleManager, "Customer");

            // Seed Users
            await SeedAdminAsync(userManager);
            await SeedCustomerAsync(userManager);
        }

        private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        private static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager)
        {
            var email = "admin@bookify.com";
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = "NourEldeen",
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User",
                    City = "Cairo",
                    Country = "Egypt",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, "Admin@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, "Admin");
            }
        }

        private static async Task SeedCustomerAsync(UserManager<ApplicationUser> userManager)
        {
            var email = "customer@bookify.com";
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = "NourEldeen",
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = "Nour",
                    LastName = "Eldeen",
                    City = "Cairo",
                    Country = "Egypt",
                    Address = "123 Main Street",
                    PostalCode = "12345",
                    DateOfBirth = new DateTime(1990, 5, 15),
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, "Customer@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, "Customer");
            }
        }
    }
}
