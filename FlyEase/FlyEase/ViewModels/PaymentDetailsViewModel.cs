using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class PaymentDetailsViewModel
    {
        // Customer Information (from previous step)
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime TravelDate { get; set; }
        public int NumberOfPeople { get; set; }
        public string? SpecialRequests { get; set; }

        // Package Information
        public int PackageID { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal PackagePrice { get; set; }

        // Price Information
        public decimal BasePrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }

        // Payment Details
        [Required(ErrorMessage = "Payment method is required")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Credit Card";

        [Required(ErrorMessage = "Card holder name is required")]
        [Display(Name = "Card Holder Name")]
        public string CardHolderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Card number is required")]
        [RegularExpression(@"^[0-9\s]{13,19}$", ErrorMessage = "Please enter a valid card number (13-19 digits)")]
        [Display(Name = "Card Number")]
        public string CardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Expiry date is required")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/?([0-9]{2})$", ErrorMessage = "Please enter valid expiry date (MM/YY)")]
        [Display(Name = "Expiry Date")]
        public string ExpiryDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "CVV is required")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "Please enter valid CVV")]
        [Display(Name = "CVV")]
        public string CVV { get; set; } = string.Empty;

        // Stripe Payment Intent ID (for server-side confirmation)
        public string PaymentIntentId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        // Payment Method Selection
        [Required(ErrorMessage = "Please select a payment method")]
        [Display(Name = "Payment Method")]
        public string SelectedPaymentMethod { get; set; } = "card";

        // List of available payment methods
        public List<PaymentMethodOption> AvailablePaymentMethods { get; set; } = new();
        // Additional fields for non-Stripe methods
        public string? ReferenceNumber { get; set; }
        public string? TransactionId { get; set; }
        public DateTime? PaymentDate { get; set; }
    }
    public class PaymentMethodOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPopular { get; set; }
        public bool UsesStripe { get; set; } = false; // Only card uses Stripe
        public string Instructions { get; set; } = string.Empty; // For non-Stripe methods
    }
}