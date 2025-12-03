// [file name]: BookingViewModel.cs

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

        // Step 1: Customer Information
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
        public DateTime TravelDate { get; set; }

        [Required(ErrorMessage = "Number of people is required")]
        [Range(1, 10, ErrorMessage = "Number of people must be between 1 and 10")]
        [Display(Name = "Number of People")]
        public int NumberOfPeople { get; set; } = 1;

        [Display(Name = "Special Requests")]
        public string? SpecialRequests { get; set; }

        // Price Information
        public decimal BasePrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }

        // Step 2: Payment Details
        [Required(ErrorMessage = "Payment method is required")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Credit Card";

        [Required(ErrorMessage = "Card number is required")]
        [RegularExpression(@"^[0-9\s]{13,19}$", ErrorMessage = "Please enter a valid card number (13-19 digits)")]
        [Display(Name = "Card Number")]
        public string CardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Card holder name is required")]
        [Display(Name = "Card Holder Name")]
        public string CardHolderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Expiry date is required")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/?([0-9]{2})$", ErrorMessage = "Please enter valid expiry date (MM/YY)")]
        [Display(Name = "Expiry Date")]
        public string ExpiryDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "CVV is required")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "Please enter valid CVV")]
        [Display(Name = "CVV")]
        public string CVV { get; set; } = string.Empty;

        // Progress tracking
        public int CurrentStep { get; set; } = 1;
        public string CurrentStepName { get; set; } = "Customer Information";
    }
}
