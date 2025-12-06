using System;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class PaymentMethodViewModel
    {
        public int BookingID { get; set; }

        public string PackageName { get; set; } = string.Empty;

        public decimal FinalAmount { get; set; }

        // This captures the user's choice (e.g., "Credit Card", "TNG", etc.)
        [Required(ErrorMessage = "Please select a payment method.")]
        public string SelectedMethod { get; set; } = string.Empty;
    }
}