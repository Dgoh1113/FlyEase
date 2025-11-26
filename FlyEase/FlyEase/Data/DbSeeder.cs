using FlyEase.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FlyEase.Data
{
    public static class DbSeeder
    {
        public static void Seed(IServiceProvider serviceProvider)
        {
            using (var context = new FlyEaseDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<FlyEaseDbContext>>()))
            {
                // Look for any users.
                if (context.Users.Any())
                {
                    return;   // DB has been seeded
                }

                // 1. Create Default Admin
                var admin = new User
                {
                    FullName = "System Administrator",
                    Email = "admin@gmail.com",
                    Phone = "+60123456789",
                    PasswordHash = HashPassword("Admin@123"), // Default Password
                    Role = "Admin",
                    CreatedDate = DateTime.UtcNow,
                    Address = "HQ Office"
                };
                context.Users.Add(admin);

                // 2. Create 5 Staff Members
                for (int i = 1; i <= 5; i++)
                {
                    context.Users.Add(new User
                    {
                        FullName = $"Staff Member {i}",
                        Email = $"staff{i}@gmail.com",
                        Phone = $"+6011{i}222333",
                        PasswordHash = HashPassword($"Staff@{i}23"), // Passwords: Staff@123, Staff@223...
                        Role = "Staff",
                        CreatedDate = DateTime.UtcNow,
                        Address = $"Branch Office {i}"
                    });
                }

                context.SaveChanges();
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}