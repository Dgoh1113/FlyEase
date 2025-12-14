using System;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class PaymentMethodViewModel
    {
        public int BookingID { get; set; }

        public string PackageName { get; set; } = string.Empty;

        public decimal FinalAmount { get; set; }

        // NEW: Deposit Calculation
        public decimal DepositAmount { get; set; }

        // This captures the user's choice (e.g., "Credit Card", "TNG", etc.)
        [Required(ErrorMessage = "Please select a payment method.")]
        public string SelectedMethod { get; set; } = string.Empty;

        // NEW: Capture if user wants Full Payment or Deposit
        // Values: "Full" or "Deposit"
        [Required(ErrorMessage = "Please select a payment type (Full or Deposit).")]
        public string PaymentType { get; set; } = "Full";

        // Add these for back navigation
        public int PackageID { get; set; }
        public int NumberOfPeople { get; set; }
    }
}