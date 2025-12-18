using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class CustomerInfoViewModel
    {
        // Package Information
        public int PackageID { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal PackagePrice { get; set; }

        // Customer Information
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [RegularExpression(@"^\d{9,11}$", ErrorMessage = "Please enter valid phone number")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Travel date is required")]
        [Display(Name = "Travel Date")]
        [DataType(DataType.Date)]
        public DateTime TravelDate { get; set; } = DateTime.Now.AddDays(14);

        [Required(ErrorMessage = "Number of people is required")]
        [Range(1, 20, ErrorMessage = "Number of people must be between 1 and 20")]
        [Display(Name = "Total People")]
        public int NumberOfPeople { get; set; } = 1;

        // === AGE DISCOUNTS ===
        [Range(0, 10, ErrorMessage = "Invalid number")]
        public int NumberOfSeniors { get; set; } = 0;

        [Range(0, 10, ErrorMessage = "Invalid number")]
        public int NumberOfJuniors { get; set; } = 0;

        // === VOUCHER (New Field to Persist Selection) ===
        public string? VoucherCode { get; set; }

        [Display(Name = "Special Requests")]
        public string? SpecialRequests { get; set; }

        // Price Information (calculated)
        public decimal BasePrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }

        public string? Address { get; set; }
    }
}