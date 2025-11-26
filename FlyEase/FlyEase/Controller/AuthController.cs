using FlyEase.Data;
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

        public AuthController(FlyEaseDbContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
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
                var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

                if (user != null && VerifyPassword(model.Password, user.PasswordHash))
                {
                    // Create Claims with Role
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.FullName),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Role, user.Role) // Important!
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal,
                        new AuthenticationProperties { IsPersistent = model.RememberMe });

                    // --- ROLE BASED REDIRECT LOGIC ---
                    if (user.Role == "Admin")
                    {
                        // Redirect to Admin Controller
                        return RedirectToAction("Dashboard", "Admin");
                    }
                    else if (user.Role == "Staff")
                    {
                        // Redirect to Staff Dashboard
                        return RedirectToAction("StaffDashboard", "StaffDashboard");
                    }
                    else
                    {
                        // Regular User -> Go to Profile
                        return RedirectToAction("Profile", "Auth");
                    }
                    // ---------------------------------
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

            // Strip +60 for display
            var displayPhone = user.Phone?.StartsWith("+60") == true ? user.Phone.Substring(3) : user.Phone;

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                Phone = displayPhone ?? "",
                Address = user.Address,

                // --- FETCH REAL DATA ---
                MyBookings = _context.Bookings
                    .Include(b => b.Package)
                    .Where(b => b.UserID == user.UserID)
                    .OrderByDescending(b => b.BookingDate)
                    .Select(b => new BookingDisplayModel
                    {
                        BookingID = b.BookingID,
                        PackageTitle = b.Package.PackageName,
                        BookingDate = b.BookingDate,
                        Status = b.BookingStatus,
                        TotalAmount = b.FinalAmount
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

        // ==========================================
        // 2. UPDATE DETAILS (POST)
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            // 1. IGNORE Password fields (We are not updating them here)
            ModelState.Remove("CurrentPassword");
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmNewPassword");

            if (ModelState.IsValid)
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

                if (user != null)
                {
                    // 2. Update Basic Info
                    user.FullName = model.FullName;
                    user.Address = model.Address;

                    // 3. Handle Phone (+60 logic)
                    // If user typed "123456789", we save "+60123456789"
                    if (!string.IsNullOrEmpty(model.Phone))
                    {
                        user.Phone = model.Phone.StartsWith("+60") ? model.Phone : "+60" + model.Phone;
                    }

                    _context.SaveChanges();

                    // 4. Refresh Session (So name updates in header)
                    await RefreshSignIn(user);

                    TempData["SuccessMessage"] = "Profile updated successfully!";
                    return RedirectToAction("Profile");
                }
            }

            // Error handling: Return view with error message
            TempData["ErrorMessage"] = "Update failed. Please check your inputs.";
            return RedirectToAction("Profile");
        }

        // ==========================================
        // 3. CHANGE PASSWORD (POST)
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ProfileViewModel model)
        {
            // 1. IGNORE Profile fields (We are not updating them here)
            ModelState.Remove("FullName");
            ModelState.Remove("Phone");
            ModelState.Remove("Address");

            if (ModelState.IsValid)
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

                if (user != null)
                {
                    // 2. Check Old Password (Hash comparison)
                    if (!VerifyPassword(model.CurrentPassword, user.PasswordHash))
                    {
                        TempData["ErrorMessage"] = "Current password is incorrect.";
                        return RedirectToAction("Profile");
                    }

                    // 3. Save New Password
                    user.PasswordHash = HashPassword(model.NewPassword);
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = "Password changed successfully!";
                    return RedirectToAction("Profile");
                }
            }

            TempData["ErrorMessage"] = "Password update failed. Check requirements.";
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