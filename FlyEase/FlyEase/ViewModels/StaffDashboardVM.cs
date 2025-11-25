using System.ComponentModel.DataAnnotations;
using FlyEase.Data;

namespace FlyEase.ViewModels
{
    // --- 1. Dashboard ---
    public class StaffDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal TotalRevenue { get; set; }

        // Lists for the dashboard tables
        public List<Booking> RecentBookings { get; set; } = new();
        public List<Package> LowStockPackages { get; set; } = new();
    }

    // --- 2. Package Management (List & Forms) ---
    public class PackageManagementVM
    {
        // For the main list view
        public List<Package> Packages { get; set; } = new();
        public List<PackageCategory> Categories { get; set; } = new();

        // For Feedback messages
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }

        // For Editing
        public int? EditingPackageId { get; set; }
    }

    // Input model for Creating/Updating
    public class PackageInputModel
    {
        public int? PackageID { get; set; }

        [Required]
        public string PackageName { get; set; } = null!;

        [Required]
        public string Destination { get; set; } = null!;

        public int? CategoryID { get; set; }
        public string? NewCategoryName { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [Required]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(1);

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "At least 1 slot required")]
        public int AvailableSlots { get; set; }

        public string? Description { get; set; }

        // File Uploads
        public List<IFormFile> ImageFiles { get; set; } = new();

        // Dynamic Inclusions (comma separated or list bound)
        public List<string> Inclusions { get; set; } = new();

        // For deletions during Edit
        public List<string> DeleteImagePaths { get; set; } = new();
    }
    public class UsersManagementVM
    {
        // Holds the list of users for the table
        public List<User> Users { get; set; } = new();

        // Holds the data for the user currently being edited in the modal
        public UserEditVM CurrentUser { get; set; } = new();
    }

    // The data structure for editing
    public class UserEditVM
    {
        public int UserID { get; set; }

        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Role { get; set; } = null!; // "User", "Staff", "Admin"

        public string? Phone { get; set; }
    }
}