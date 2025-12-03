using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class ProfileViewModel
    {
        // --- PROFILE INFO ---
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = "";

        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Phone is required")]
        [Display(Name = "Phone")]
        [RegularExpression(@"^\d{9,11}$", ErrorMessage = "Enter 9-11 digits (e.g. 123456789)")]
        public string Phone { get; set; } = "";

        public string? Address { get; set; }
        public string? ProfilePictureUrl { get; set; }

        // --- PASSWORD FIELDS ---
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$", ErrorMessage = "Weak password (Need Upper, Lower, Number).")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string? ConfirmNewPassword { get; set; }

        // --- HISTORY LISTS ---
        public List<BookingDisplayModel> MyBookings { get; set; } = new();
        public List<ReviewDisplayModel> MyReviews { get; set; } = new();
        public List<PaymentDisplayModel> PaymentHistory { get; set; } = new();
        public List<PackageDisplayModel> FavoritePackages { get; set; } = new();
    }

    // Helper Models
    public class BookingDisplayModel
    {
        public int BookingID { get; set; }
        public string PackageTitle { get; set; } = "";
        public DateTime BookingDate { get; set; }
        public string Status { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public bool IsReviewed { get; set; }
    }

    public class ReviewDisplayModel
    {
        public string PackageTitle { get; set; } = "";
        public int Rating { get; set; }
        public string Comment { get; set; } = "";
        public DateTime CreatedDate { get; set; }
    }

    public class PaymentDisplayModel
    {
        public int PaymentID { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = "";
        public decimal AmountPaid { get; set; }
        public string PaymentStatus { get; set; } = "";
    }

    public class PackageDisplayModel
    {
        public int PackageID { get; set; }
        public string Title { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Duration { get; set; } = "";
        public decimal Price { get; set; }
        public double Rating { get; set; }
    }
}