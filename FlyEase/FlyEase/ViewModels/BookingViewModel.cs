// [file name]: BookingViewModel.cs (in ViewModels folder)


using System.ComponentModel.DataAnnotations;
using System;

namespace FlyEase.ViewModels
{
    public class BookingViewModel
    {
        // Package Information
        public int PackageID { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal PackagePrice { get; set; }

        // Customer Information
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\d{9,11}$", ErrorMessage = "Please enter a valid phone number (9-11 digits)")]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Travel date is required")]
        [Display(Name = "Travel Date")]
        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Travel date must be in the future")]
        public DateTime TravelDate { get; set; } = DateTime.Now.AddDays(14);

        [Required(ErrorMessage = "Number of people is required")]
        [Range(1, 10, ErrorMessage = "Number of people must be between 1 and 10")]
        [Display(Name = "Number of People")]
        public int NumberOfPeople { get; set; } = 1;

        [Display(Name = "Special Requests")]
        [StringLength(500, ErrorMessage = "Special requests cannot exceed 500 characters")]
        public string? SpecialRequests { get; set; }

        // Payment Information
        [Required(ErrorMessage = "Payment method is required")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Credit Card";

        [Required(ErrorMessage = "Card number is required")]
        [CreditCard(ErrorMessage = "Please enter a valid card number")]
        [Display(Name = "Card Number")]
        public string CardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Card holder name is required")]
        [Display(Name = "Card Holder Name")]
        public string CardHolderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Expiry date is required")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/?([0-9]{2})$", ErrorMessage = "Please enter a valid expiry date (MM/YY)")]
        [Display(Name = "Expiry Date")]
        public string ExpiryDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "CVV is required")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "Please enter a valid CVV")]
        [Display(Name = "CVV")]
        public string CVV { get; set; } = string.Empty;

        // Price Information
        public decimal BasePrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }

        // Progress Tracking
        public int CurrentStep { get; set; } = 1;
    }

    // Custom validation attribute for future dates
    public class FutureDateAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value is DateTime date)
            {
                return date > DateTime.Today;
            }
            return false;
        }
    }
}
