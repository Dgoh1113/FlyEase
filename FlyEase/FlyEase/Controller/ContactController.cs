using FlyEase.Data; // Needed for DbContext
using FlyEase.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims; // Needed for User Claims

namespace FlyEase.Controllers
{
    public class ContactController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly FlyEaseDbContext _context; // 1. Add DbContext

        // 2. Inject DbContext in Constructor
        public ContactController(IEmailService emailService, FlyEaseDbContext context)
        {
            _emailService = emailService;
            _context = context;
        }

        // GET: Contact page
        public async Task<IActionResult> Index()
        {
            var model = new ContactViewModel();

            // 3. Check if user is logged in
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // Get User ID from the session claim
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (int.TryParse(userIdClaim, out int userId))
                {
                    // Fetch user details from DB
                    var user = await _context.Users.FindAsync(userId);
                    if (user != null)
                    {
                        // 4. Auto-fill the model
                        model.Name = user.FullName;
                        model.Email = user.Email; // This auto-fills the Gmail/Email
                        model.Phone = user.Phone;
                    }
                }
            }

            return View(model);
        }

        // POST: Handle contact form submission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitContact(ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            try
            {
                // Create contact data
                var contactData = new ContactFormData
                {
                    Name = model.Name,
                    Email = model.Email,
                    Phone = model.Phone,
                    Subject = model.Subject,
                    Message = model.Message,
                    SubmittedAt = DateTime.Now
                };

                // Send email using existing service
                var emailSent = await _emailService.SendContactFormEmailAsync(contactData);

                if (emailSent)
                {
                    TempData["SuccessMessage"] = "Thank you for contacting us! We'll get back to you within 24 hours. Check your email for confirmation.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Your message was saved, but we couldn't send the confirmation email. We'll still contact you soon.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Log error
                TempData["ErrorMessage"] = "Sorry, there was an error sending your message. Please try again.";
                return View("Index", model);
            }
        }
    }

    // Keep existing ContactViewModel
    public class ContactViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        [Required(ErrorMessage = "Subject is required")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message is required")]
        public string Message { get; set; } = string.Empty;
    }
}