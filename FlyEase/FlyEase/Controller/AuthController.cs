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

        public AuthController(FlyEaseDbContext context, ILogger<AuthController> logger, IEmailService emailService)
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
                    // 1. Format Phone Number: Prepend +60
                    string formattedPhone = "+60" + model.Phone;

                    // 2. Check if email exists
                    var existingUser = _context.Users.FirstOrDefault(u => u.Email == model.Email);
                    if (existingUser != null)
                    {
                        ModelState.AddModelError("Email", "Email is already registered.");
                        return View(model);
                    }

                    // 3. Manual validations
                    if (!IsValidEmailDomain(model.Email))
                    {
                        ModelState.AddModelError("Email", "Invalid email domain.");
                        return View(model);
                    }

                    if (!IsPasswordStrong(model.Password))
                    {
                        ModelState.AddModelError("Password", "Password is not strong enough.");
                        return View(model);
                    }

                    // 4. Create User (Only basic fields)
                    var user = new User
                    {
                        FullName = model.FullName,
                        Email = model.Email,
                        Phone = formattedPhone,
                        PasswordHash = HashPassword(model.Password),
                        Role = "User",
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.Users.Add(user);
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = "Registration successful! Please login.";
                    return RedirectToAction("Login");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                ModelState.AddModelError("", "An unexpected error occurred.");
            }

            return View(model);
        }

        // ==========================================
        // LOGIN
        // ==========================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Check for success message from registration
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                // 1. Find User
                var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

                // 2. Verify Password (using Hash)
                if (user != null && VerifyPassword(model.Password, user.PasswordHash))
                {
                    // 3. Create Claims
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier,user.UserID.ToString()),
                        new Claim(ClaimTypes.Name, user.FullName),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Role, user.Role ?? "User")
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    // --- FIX START: Handle Remember Me ---
                    var authProperties = new AuthenticationProperties
                    {
                        // This tells the browser: "Save this cookie even after closing"
                        IsPersistent = model.RememberMe
                    };

                    if (model.RememberMe)
                    {
                        // If checked: Keep them logged in for 30 days
                        authProperties.ExpiresUtc = DateTime.UtcNow.AddDays(30);
                    }
                    // --- FIX END ---

                    // 4. Sign In with Properties
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal,
                        authProperties); // Pass the properties here!

                    // 5. Redirect (FIXED THIS PART)
                    if (user.Role == "Admin" || user.Role == "Staff")
                    {
                        // Redirects to AdminDashboardController, Action: AdminDashboard
                        return RedirectToAction("AdminDashboard", "AdminDashboard");
                    }

                    return RedirectToAction("Profile", "Auth");
                }
                ModelState.AddModelError("", "Invalid email or password.");
            }
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
        // PROFILE (Fixed)
        // ==========================================
        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            if (user == null) return RedirectToAction("Login");

            var displayPhone = user.Phone?.StartsWith("+60") == true ? user.Phone.Substring(3) : user.Phone;

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                Phone = displayPhone ?? "",
                Address = user.Address,

                // === FIXED QUERY ===
                MyBookings = _context.Bookings
                    .Include(b => b.Package)
                    .Include(b => b.Feedbacks) // <--- CRITICAL ADDITION: Ensures we know if it's reviewed
                    .Where(b => b.UserID == user.UserID)
                    .OrderByDescending(b => b.BookingDate)
                    .Select(b => new BookingDisplayModel
                    {
                        BookingID = b.BookingID,
                        PackageTitle = b.Package.PackageName,
                        BookingDate = b.BookingDate,
                        Status = b.BookingStatus,
                        TotalAmount = b.FinalAmount,
                        IsReviewed = b.Feedbacks.Any() // Now this will definitely work
                    }).ToList(),
                // ===================

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

        // 2. ADD THIS MISSING METHOD (Fixes the Refresh Button)
        [HttpGet]
        public IActionResult RefreshBookings()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            if (user == null) return NotFound();

            // Re-fetch only the bookings
            var myBookings = _context.Bookings
                .Include(b => b.Package)
                .Include(b => b.Feedbacks) // <--- Don't forget it here too!
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

            // Return ONLY the rows, not the whole page
            return PartialView("_BookingRows", myBookings);
        }



        // ==========================================
        // FORGOT PASSWORD
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
            if (ModelState.IsValid)
            {
                try
                {
                    var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

                    if (user != null)
                    {
                        // Generate random password
                        var newPassword = GenerateRandomPassword();

                        // Update user password in database
                        user.PasswordHash = HashPassword(newPassword);
                        await _context.SaveChangesAsync();

                        // Send email with new password
                        var emailSent = await _emailService.SendPasswordResetEmailAsync(
                            user.Email,
                            user.FullName,
                            newPassword
                        );

                        if (emailSent)
                        {
                            TempData["SuccessMessage"] = "A new password has been sent to your email address!";
                            return RedirectToAction("Login");
                        }
                        else
                        {
                            ModelState.AddModelError("", "Failed to send email. Please try again later.");
                        }
                    }
                    else
                    {
                        // Don't reveal whether user exists (security best practice)
                        TempData["SuccessMessage"] = "If an account exists with that email, a new password has been sent.";
                        return RedirectToAction("Login");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during password reset for email: {Email}", model.Email);
                    ModelState.AddModelError("", "An error occurred while processing your request.");
                }
            }

            return View(model);
        }

        private string GenerateRandomPassword()
        {
            const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lowercase = "abcdefghjkmnpqrstuvwxyz";
            const string numbers = "23456789";
            const string special = "!@#$%^&*";

            var random = new Random();
            var password = new char[10];

            // Ensure at least one of each type
            password[0] = uppercase[random.Next(uppercase.Length)];
            password[1] = lowercase[random.Next(lowercase.Length)];
            password[2] = numbers[random.Next(numbers.Length)];
            password[3] = special[random.Next(special.Length)];

            // Fill the rest
            var allChars = uppercase + lowercase + numbers + special;
            for (int i = 4; i < 10; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Shuffle the password
            return new string(password.OrderBy(x => random.Next()).ToArray());
        }

        // ==========================================
        // PROFILE & UPDATE LOGIC
        // ==========================================

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            // --- FIX: Ignore Password errors when updating profile ---
            ModelState.Remove("CurrentPassword");
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmNewPassword");
            // Ignore Lists
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

                    // Re-add +60
                    if (!string.IsNullOrEmpty(model.Phone))
                    {
                        user.Phone = model.Phone.StartsWith("+60") ? model.Phone : "+60" + model.Phone;
                    }

                    _context.SaveChanges();
                    await RefreshSignIn(user, true); // Keep session persistent if it was before

                    TempData["SuccessMessage"] = "Profile updated successfully!";
                    return RedirectToAction("Profile");
                }
            }
            TempData["ErrorMessage"] = "Update failed. Please check inputs.";
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ProfileViewModel model)
        {
            // --- FIX: Ignore Profile errors when updating password ---
            ModelState.Remove("FullName");
            ModelState.Remove("Phone");
            ModelState.Remove("Address");
            // Ignore Lists
            ModelState.Remove("MyBookings");
            ModelState.Remove("MyReviews");
            ModelState.Remove("PaymentHistory");
            ModelState.Remove("FavoritePackages");

            // --- FIX: Check for NULL/Empty Password Fields ---
            if (string.IsNullOrEmpty(model.CurrentPassword) ||
                string.IsNullOrEmpty(model.NewPassword) ||
                string.IsNullOrEmpty(model.ConfirmNewPassword))
            {
                TempData["ErrorMessage"] = "All password fields are required.";
                return RedirectToAction("Profile");
            }

            if (ModelState.IsValid)
            {
                var user = GetCurrentUser();
                if (user != null)
                {
                    // Verify Old Password
                    if (!VerifyPassword(model.CurrentPassword, user.PasswordHash))
                    {
                        TempData["ErrorMessage"] = "Current password is incorrect.";
                        return RedirectToAction("Profile");
                    }

                    // Update Password
                    user.PasswordHash = HashPassword(model.NewPassword);
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = "Password changed successfully!";
                    return RedirectToAction("Profile");
                }
            }
            TempData["ErrorMessage"] = "Password check failed.";
            return RedirectToAction("Profile");
        }

        private async Task RefreshSignIn(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }
        private User? GetCurrentUser()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail)) return null;
            return _context.Users.FirstOrDefault(u => u.Email == userEmail);
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
        // Helper Methods
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
    }
}