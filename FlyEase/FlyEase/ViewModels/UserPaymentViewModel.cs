// [file name]: UserPaymentViewModel.cs

using System.ComponentModel.DataAnnotations;
using System;

namespace FlyEase.ViewModels
{
    public class UserPaymentViewModel
    {
        // Package Selection
        [Required(ErrorMessage = "Please select a package")]
        [Display(Name = "Select Travel Package")]
        public int? SelectedPackageId { get; set; }

        public string PackageName { get; set; } = string.Empty;

        // Travel Details
        [Required(ErrorMessage = "Please select travel date")]
        [Display(Name = "Travel Date")]
        [DataType(DataType.Date)]
        public DateTime TravelDate { get; set; } = DateTime.Now.AddDays(14);

        [Required(ErrorMessage = "Please specify number of people")]
        [Range(1, 10, ErrorMessage = "Number of people must be between 1 and 10")]
        [Display(Name = "Number of People")]
        public int NumberOfPeople { get; set; } = 1;

        // Price Information (calculated)
        [Display(Name = "Base Price")]
        public decimal BasePrice { get; set; }

        [Display(Name = "Discount Amount")]
        public decimal DiscountAmount { get; set; }

        [Display(Name = "Final Amount")]
        public decimal FinalAmount { get; set; }

        // Payment Method
        [Required(ErrorMessage = "Please select payment method")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Credit Card";

        // Card Details
        [Required(ErrorMessage = "Card number is required")]
        [CreditCard(ErrorMessage = "Please enter a valid card number")]
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

        // Terms acceptance
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms and conditions")]
        [Display(Name = "I agree to the terms and conditions")]
        public bool AcceptTerms { get; set; }
    }
}
