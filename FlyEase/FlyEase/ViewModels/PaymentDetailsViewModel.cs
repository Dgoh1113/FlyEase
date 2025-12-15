using System.ComponentModel.DataAnnotations;
using System;

namespace FlyEase.ViewModels
{
    [Serializable]
    public class BookingViewModel
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
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime TravelDate { get; set; }

        [Required]
        [Range(1, 10)]
        public int NumberOfPeople { get; set; } = 1;

        public string? SpecialRequests { get; set; }

        // Price Information
        public decimal BasePrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }

        // Payment Details
        [Required]
        public string PaymentMethod { get; set; } = "Credit Card";

        // === STRIPE TOKEN (Important) ===
        public string? StripeToken { get; set; }

        // Optional Card Fields (Not used for Stripe processing)
        public string? CardNumber { get; set; }
        public string? CardHolderName { get; set; }
        public string? ExpiryDate { get; set; }
        public string? CVV { get; set; }

        // Progress tracking
        public int CurrentStep { get; set; } = 1;
        public string CurrentStepName { get; set; } = "Customer Information";
    }
    public class PriceRequest
    {
        public int PackageId { get; set; }
        public int People { get; set; }
        public int Seniors { get; set; } // New
        public int Juniors { get; set; } // New
        public DateTime TravelDate { get; set; }
    }
}