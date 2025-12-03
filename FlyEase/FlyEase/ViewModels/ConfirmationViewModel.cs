namespace FlyEase.ViewModels
{
    public class ConfirmationViewModel
    {
        // Customer Information
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

        // Payment Information
        public string PaymentMethod { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public string CVV { get; set; } = string.Empty;
    }
}