using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Email is required")]
        [Display(Name = "Email Address")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@(gmail\.com|yahoo\.com|hotmail\.com)$",
            ErrorMessage = "Only gmail.com, yahoo.com, or hotmail.com email addresses are allowed.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [RegularExpression(@"^\d{9,11}$",
            ErrorMessage = "Please enter the phone number without +60 (e.g., 123456789).")]
        public string Phone { get; set; } = null!;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&/])[A-Za-z\d@$!%*?&/]{6,}$", 
        ErrorMessage = "Password must be at least 6 chars, with 1 uppercase, 1 lowercase, 1 number, and 1 special char.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = null!;

        public string? Address { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the Terms of Service")]
        public bool AgreeToTerms { get; set; }
    }
}