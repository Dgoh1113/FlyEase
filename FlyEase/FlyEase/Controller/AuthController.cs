using FlyEase.Data;
using FlyEase.Services;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace FlyEase.Controllers
{
    public class AuthController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly ILogger<AuthController> _logger;
        private readonly IEmailService _emailService;

        // Simple in-memory store for reset sessions
        private static readonly Dictionary<string, ResetTokenInfo> _resetTokens = new();

        private class ResetTokenInfo
        {
            public string Email { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsUsed { get; set; }
        }

        public AuthController(FlyEaseDbContext context, ILogger<AuthController> logger,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        // ==========================================
        // REGISTER
        // ==========================================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Format Phone Number: Prepend +60
                    string formattedPhone = "+60" + model.Phone;

                    // Check if email already exists
                    var existingEmail = _context.Users.FirstOrDefault(u => u.Email == model.Email);
                    if (existingEmail != null)
                    {
                        ModelState.AddModelError("Email", "Email is already registered.");
                        return View(model);
                    }

                    // Check if phone number already exists
                    var existingPhone = _context.Users.FirstOrDefault(u => u.Phone == formattedPhone);
                    if (existingPhone != null)
                    {
                        ModelState.AddModelError("Phone", "Phone number is already registered.");
                        return View(model);
                    }

                    // Additional manual validations
                    if (!IsValidEmailDomain(model.Email))
                    {
                        ModelState.AddModelError("Email", "Invalid email domain.");
                        return View(model);
                    }

                    if (!IsPasswordStrong(model.Password))
                    {
                        ModelState.AddModelError("Password", "Password must contain at least 6 characters with uppercase, lowercase, and number.");
                        return View(model);
                    }

                    // Create User
                    var user = new User
                    {
                        FullName = model.FullName,
                        Email = model.Email,
                        Phone = formattedPhone,
                        PasswordHash = HashPassword(model.Password),
                        Role = "User",
                        CreatedDate = DateTime.UtcNow,
                        Address = model.Address
                    };

                    _context.Users.Add(user);
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = "Registration successful! Please login.";
                    return RedirectToAction("Login");
                }
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                ModelState.AddModelError("", "An error occurred.");
                return View(model);
            }
        }

        // ==========================================
        // LOGIN
        // ==========================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (TempData["SuccessMessage"] != null)
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user != null && VerifyPassword(model.Password, user.PasswordHash))
            {
                var claims = new List<Claim>
                {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                // FIX: Set IsPersistent based on RememberMe checkbox
                var authProperties = new AuthenticationProperties
                {
                    // IsPersistent = true tells the browser to save the cookie to disk
                    // allowing it to survive a browser restart.
                    IsPersistent = model.RememberMe,

                    // Set expiration to 30 days if remembered, otherwise default session
                    ExpiresUtc = model.RememberMe ? DateTime.UtcNow.AddDays(30) : null,

                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // ==========================================
        // PROFILE
        // ==========================================
        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login");

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone?.StartsWith("+60") == true ? user.Phone.Substring(3) : user.Phone ?? "",
                Address = user.Address,
                MyBookings = _context.Bookings
                    .Include(b => b.Package)
                    .Include(b => b.Feedbacks)
                    .Where(b => b.UserID == user.UserID)
                    .OrderByDescending(b => b.BookingDate)
                    .Select(b => new BookingDisplayModel
                    {
                        BookingID = b.BookingID,
                        PackageTitle = b.Package.PackageName,
                        BookingDate = b.BookingDate,
                        Status = b.BookingStatus,
                        TotalAmount = b.FinalAmount,
                        IsReviewed = b.Feedbacks.Any()
                    }).ToList(),
                PaymentHistory = _context.Payments
                    .Include(p => p.Booking)
                    .Where(p => p.Booking.UserID == user.UserID)
                    .OrderByDescending(p => p.PaymentDate)
                    .Select(p => new PaymentDisplayModel
                    {
                        PaymentID = p.PaymentID,
                        PaymentDate = p.PaymentDate,
                        PaymentMethod = p.PaymentMethod,
                        AmountPaid = p.AmountPaid,
                        PaymentStatus = p.PaymentStatus
                    }).ToList(),
                MyReviews = _context.Feedbacks
                    .Include(f => f.Booking).ThenInclude(b => b.Package)
                    .Where(f => f.UserID == user.UserID)
                    .OrderByDescending(f => f.CreatedDate)
                    .Select(f => new ReviewDisplayModel
                    {
                        PackageTitle = f.Booking.Package.PackageName,
                        Rating = f.Rating,
                        Comment = f.Comment,
                        CreatedDate = f.CreatedDate
                    }).ToList(),
                FavoritePackages = _context.Packages
                    .Take(4)
                    .Select(p => new PackageDisplayModel
                    {
                        PackageID = p.PackageID,
                        Title = p.PackageName,
                        ImageUrl = p.ImageURL ?? "https://via.placeholder.com/400x300",
                        Price = p.Price,
                        Duration = ((p.EndDate - p.StartDate).Days + 1) + " Days",
                        Rating = 5.0
                    }).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult RefreshBookings()
        {
            var user = GetCurrentUser();
            if (user == null) return NotFound();

            var myBookings = _context.Bookings
                .Include(b => b.Package)
                .Include(b => b.Feedbacks)
                .Where(b => b.UserID == user.UserID)
                .OrderByDescending(b => b.BookingDate)
                .Select(b => new BookingDisplayModel
                {
                    BookingID = b.BookingID,
                    PackageTitle = b.Package.PackageName,
                    BookingDate = b.BookingDate,
                    Status = b.BookingStatus,
                    TotalAmount = b.FinalAmount,
                    IsReviewed = b.Feedbacks.Any()
                }).ToList();

            return PartialView("_BookingRows", myBookings);
        }

        // ==========================================
        // FORGOT PASSWORD (FIXED VERSION)
        // ==========================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user != null)
                {
                    // Generate a simple token (timestamp + email hash)
                    var token = Guid.NewGuid().ToString();
                    var expiry = DateTime.UtcNow.AddHours(1);

                    // Store in session (alternative: use TempData)
                    HttpContext.Session.SetString("ResetToken", token);
                    HttpContext.Session.SetString("ResetEmail", user.Email);
                    HttpContext.Session.SetString("ResetExpiry", expiry.ToString("O"));

                    // Generate reset link
                    var resetLink = Url.Action("ResetPassword", "Auth",
                        new { token = token, email = user.Email },
                        Request.Scheme);

                    // Send email
                    await _emailService.SendPasswordResetLinkAsync(user.Email, user.FullName, resetLink);

                    _logger.LogInformation($"Reset link sent to {user.Email}");
                }

                // Always show success (security best practice)
                TempData["SuccessMessage"] = "If an account exists, a reset link has been sent.";
                return RedirectToAction("ForgotPassword");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Forgot password error");
                ModelState.AddModelError("", "An error occurred.");
                return View(model);
            }
        }

        // ==========================================
        // RESET PASSWORD (SIMPLIFIED VERSION)
        // ==========================================
        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            // Simple validation
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Invalid reset link.";
                return RedirectToAction("ForgotPassword");
            }

            // Store in ViewData for the form
            ViewData["ResetToken"] = token;
            ViewData["ResetEmail"] = email;

            return View(new ResetPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, string token, string email)
        {
            // Get values from form if not in parameters
            token = token ?? Request.Form["token"];
            email = email ?? Request.Form["email"];

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Invalid reset request.";
                return RedirectToAction("ForgotPassword");
            }

            if (!ModelState.IsValid)
            {
                ViewData["ResetToken"] = token;
                ViewData["ResetEmail"] = email;
                return View(model);
            }

            // Validate passwords match
            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                ViewData["ResetToken"] = token;
                ViewData["ResetEmail"] = email;
                return View(model);
            }

            // Validate password strength
            if (!IsPasswordStrong(model.NewPassword))
            {
                ModelState.AddModelError("NewPassword",
                    "Password must be at least 6 characters with uppercase, lowercase, and number.");
                ViewData["ResetToken"] = token;
                ViewData["ResetEmail"] = email;
                return View(model);
            }

            try
            {
                // Find user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("ForgotPassword");
                }

                // Update password - SIMPLE AND RELIABLE
                var newHash = HashPassword(model.NewPassword);

                // Method 1: Direct SQL (Most reliable)
                var sql = "UPDATE Users SET PasswordHash = @p0 WHERE Email = @p1";
                int rowsUpdated = await _context.Database.ExecuteSqlRawAsync(sql,
                    new Microsoft.Data.SqlClient.SqlParameter("@p0", newHash),
                    new Microsoft.Data.SqlClient.SqlParameter("@p1", email));

                // Method 2: Entity Framework (Backup)
                if (rowsUpdated == 0)
                {
                    user.PasswordHash = newHash;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Password reset successful for {email}");

                TempData["SuccessMessage"] = "Password reset successfully! Please login.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Reset password error for {email}");
                TempData["ErrorMessage"] = "Failed to reset password.";
                return RedirectToAction("ForgotPassword");
            }
        }




        // ==========================================
        // PROFILE UPDATE METHODS
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            ModelState.Remove("CurrentPassword");
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmNewPassword");
            ModelState.Remove("MyBookings");
            ModelState.Remove("MyReviews");
            ModelState.Remove("PaymentHistory");
            ModelState.Remove("FavoritePackages");

            if (ModelState.IsValid)
            {
                var user = GetCurrentUser();
                if (user != null)
                {
                    user.FullName = model.FullName;
                    user.Address = model.Address;

                    if (!string.IsNullOrEmpty(model.Phone))
                    {
                        user.Phone = model.Phone.StartsWith("+60") ? model.Phone : "+60" + model.Phone;
                    }

                    _context.SaveChanges();
                    await RefreshSignIn(user, true);

                    TempData["DetailsSuccessMessage"] = "Profile updated successfully!";
                    return RedirectToAction("Profile", new { tab = "details" });
                }
            }

            TempData["DetailsErrorMessage"] = "Update failed.";
            return RedirectToAction("Profile", new { tab = "details" });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ProfileViewModel model)
        {
            try
            {
                ModelState.Remove("FullName");
                ModelState.Remove("Phone");
                ModelState.Remove("Address");
                ModelState.Remove("MyBookings");
                ModelState.Remove("MyReviews");
                ModelState.Remove("PaymentHistory");
                ModelState.Remove("FavoritePackages");

                if (string.IsNullOrEmpty(model.CurrentPassword) ||
                    string.IsNullOrEmpty(model.NewPassword) ||
                    string.IsNullOrEmpty(model.ConfirmNewPassword))
                {
                    TempData["SecurityErrorMessage"] = "All fields are required.";
                    return RedirectToAction("Profile", new { tab = "security" });
                }

                if (ModelState.IsValid)
                {
                    var user = GetCurrentUser();
                    if (user != null)
                    {
                        if (!VerifyPassword(model.CurrentPassword, user.PasswordHash))
                        {
                            TempData["SecurityErrorMessage"] = "Current password is incorrect.";
                            return RedirectToAction("Profile", new { tab = "security" });
                        }

                        if (!IsPasswordStrong(model.NewPassword))
                        {
                            TempData["SecurityErrorMessage"] = "Password must have uppercase, lowercase, number, and be at least 6 characters.";
                            return RedirectToAction("Profile", new { tab = "security" });
                        }

                        user.PasswordHash = HashPassword(model.NewPassword);
                        _context.SaveChanges();

                        TempData["SecuritySuccessMessage"] = "Password changed successfully!";
                        return RedirectToAction("Profile", new { tab = "security" });
                    }

                    TempData["SecurityErrorMessage"] = "User not found.";
                    return RedirectToAction("Profile", new { tab = "security" });
                }
                else
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    TempData["SecurityErrorMessage"] = string.Join(" ", errors);
                    return RedirectToAction("Profile", new { tab = "security" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                TempData["SecurityErrorMessage"] = "An error occurred.";
                return RedirectToAction("Profile", new { tab = "security" });
            }
        }

        // ==========================================
        // HELPER METHODS
        // ==========================================
        private void CleanupOldTokens()
        {
            var tokensToRemove = _resetTokens
                .Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow || kvp.Value.IsUsed)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var token in tokensToRemove)
            {
                _resetTokens.Remove(token);
            }

            _logger.LogInformation($"Cleaned up {tokensToRemove.Count} old reset tokens");
        }


        private User? GetCurrentUser()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            return userEmail != null ? _context.Users.FirstOrDefault(u => u.Email == userEmail) : null;
        }

        private async Task RefreshSignIn(User user, bool isPersistent = false)
        {
            var claims = new List<Claim> {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties { IsPersistent = isPersistent };
            if (isPersistent) authProperties.ExpiresUtc = DateTime.UtcNow.AddDays(30);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
        }

        private bool IsValidEmailDomain(string email)
        {
            if (string.IsNullOrEmpty(email)) return false;
            var regex = new Regex(@"^[a-zA-Z0-9._%+-]+@(gmail\.com|yahoo\.com|hotmail\.com)$", RegexOptions.IgnoreCase);
            return regex.IsMatch(email);
        }

        private bool IsPasswordStrong(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$");
            return regex.IsMatch(password);
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            return HashPassword(password) == storedHash;
        }

        [AcceptVerbs("GET", "POST")]
        public IActionResult VerifyEmail(string email)
        {
            var existingUser = _context.Users.FirstOrDefault(u => u.Email == email);
            if (existingUser != null)
                return Json($"Email {email} is already registered.");
            return Json(true);
        }

        [AcceptVerbs("GET", "POST")]
        public IActionResult VerifyPhone(string phone)
        {
            string formattedPhone = "+60" + phone;
            var existingUser = _context.Users.FirstOrDefault(u => u.Phone == formattedPhone);
            if (existingUser != null)
                return Json($"Phone number {phone} is already registered.");
            return Json(true);
        }
    }
}