using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
    }

  
}