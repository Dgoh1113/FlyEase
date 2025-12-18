using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100)]
        [Display(Name = "Full Name")]

        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; } = null!;

        // === NEW: OTP PROPERTY ===
        [Required(ErrorMessage = "Verification code is required")]
        [Display(Name = "Verification Code")]
        public string Otp { get; set; } = null!;
        // =========================

        [Required(ErrorMessage = "Phone number is required")]
        public string Phone { get; set; } = null!;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = null!;

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the Terms")]
        public bool AgreeToTerms { get; set; }
    }
}