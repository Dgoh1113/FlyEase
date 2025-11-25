using System.ComponentModel.DataAnnotations;
using FlyEase.Data;

namespace FlyEase.ViewModels
{
    // --- 1. DASHBOARD ---
    public class StaffDashboardVM
    {
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<Booking> RecentBookings { get; set; } = new();
        public List<Package> LowStockPackages { get; set; } = new();
    }

    // --- 2. USERS PAGE ---
    public class UsersPageVM
    {
        public List<User> Users { get; set; } = new();
        public UserEditVM CurrentUser { get; set; } = new();
    }

    public class UserEditVM
    {
        public int UserID { get; set; }
        [Required] public string FullName { get; set; } = null!;
        [Required, EmailAddress] public string Email { get; set; } = null!;
        [Required] public string Role { get; set; } = "User";
        public string? Phone { get; set; }
    }

    // --- 3. PACKAGES PAGE (Updated for Images) ---
    public class PackagesPageVM
    {
        public List<Package> Packages { get; set; } = new();
        public List<PackageCategory> Categories { get; set; } = new();
        public PackageInputModel CurrentPackage { get; set; } = new();
    }

    public class PackageInputModel
    {
        public int? PackageID { get; set; }
        [Required] public string PackageName { get; set; } = null!;
        [Required] public string Destination { get; set; } = null!;
        public int? CategoryID { get; set; }
        public string? NewCategoryName { get; set; }
        [Required, Range(0.01, double.MaxValue)] public decimal Price { get; set; }
        [Required] public DateTime StartDate { get; set; } = DateTime.Today;
        [Required] public DateTime EndDate { get; set; } = DateTime.Today.AddDays(1);
        [Required, Range(1, int.MaxValue)] public int AvailableSlots { get; set; }
        public string? Description { get; set; }

        // --- IMAGE HANDLING ---
        public List<IFormFile> ImageFiles { get; set; } = new(); // For new uploads
        public List<string> DeleteImagePaths { get; set; } = new(); // For images removed in Edit
        // ---------------------

        public List<string> Inclusions { get; set; } = new();
    }

    // --- 4. BOOKINGS PAGE ---
    public class BookingsPageVM
    {
        public List<Booking> Bookings { get; set; } = new();
        public BookingEditVM CurrentBooking { get; set; } = new();
    }

    public class BookingEditVM
    {
        public int BookingID { get; set; }
        [Required] public string Status { get; set; } = null!;
        [Required] public DateTime TravelDate { get; set; }
    }
}