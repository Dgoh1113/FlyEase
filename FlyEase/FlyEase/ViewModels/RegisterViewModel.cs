using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

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
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@(gmail\.com|yahoo\.com|hotmail\.com)$",
            ErrorMessage = "Only gmail.com, yahoo.com, or hotmail.com email addresses are allowed.")]
        [Remote(action: "VerifyEmail", controller: "Auth", ErrorMessage = "Email is already registered")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [RegularExpression(@"^\d{9,11}$",
            ErrorMessage = "Please enter the phone number without +60 (e.g., 123456789).")]
        [Remote(action: "VerifyPhone", controller: "Auth", ErrorMessage = "Phone number is already registered")]
        public string Phone { get; set; } = null!;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&/])[A-Za-z\d@$!%*?&/]{6,}$",
            ErrorMessage = "Password must be at least 6 characters with uppercase, lowercase, number, and special character.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = null!;

        public string? Address { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the Terms of Service")]
        [Display(Name = "I agree to Terms & Conditions")]
        public bool AgreeToTerms { get; set; }
    }
}