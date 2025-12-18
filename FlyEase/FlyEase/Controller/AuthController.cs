using FlyEase.Data;
using FlyEase.Services;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization; // Added for [Authorize]
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq; // Added for LINQ
using System.Security.Claims;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    public class AuthController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;
        private readonly EmailService _otpService;

        public AuthController(
            FlyEaseDbContext context,
            IMemoryCache cache,
            IEmailService emailService,
            EmailService otpService)
        {
            _context = context;
            _cache = cache;
            _emailService = emailService;
            _otpService = otpService;
        }

        // ==========================================
        // LOGIN ACTIONS
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            string failCountKey = $"FailCount_{model.Email}";
            string lockoutKey = $"Lockout_{model.Email}";

            if (_cache.TryGetValue(lockoutKey, out DateTime lockoutEnd))
            {
                if (DateTime.Now < lockoutEnd)
                {
                    var remaining = lockoutEnd - DateTime.Now;
                    string timeMsg = remaining.TotalMinutes >= 1
                        ? $"{(int)remaining.TotalMinutes + 1} minutes"
                        : $"{(int)remaining.TotalSeconds} seconds";

                    ViewData["ErrorMessage"] = $"Account locked due to multiple failed attempts. Please try again in {timeMsg}.";
                    return View(model);
                }
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                ViewData["ErrorMessage"] = "Account does not exist.";
                return View(model);
            }

            if (user.Role == "Ban")
            {
                ViewData["ErrorMessage"] = "Your account has been banned. Please contact support.";
                return View(model);
            }

            if (VerifyPassword(model.Password, user.PasswordHash))
            {
                _cache.Remove(failCountKey);
                _cache.Remove(lockoutKey);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role ?? "User")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTime.UtcNow.AddDays(30) : null,
                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                if (user.Role == "Admin" || user.Role == "Staff")
                {
                    return RedirectToAction("AdminDashboard", "AdminDashboard");
                }

                return RedirectToAction("Index", "Home");
            }
            else
            {
                int currentFails = _cache.GetOrCreate(failCountKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(10);
                    return 0;
                });

                currentFails++;
                _cache.Set(failCountKey, currentFails);

                string errorMsg = "Invalid password.";

                if (currentFails < 3)
                {
                    int remaining = 3 - currentFails;
                    errorMsg = $"Invalid password. {remaining} attempt{(remaining > 1 ? "s" : "")} remaining before temporary lockout.";
                }
                else if (currentFails == 3)
                {
                    DateTime lockEnd = DateTime.Now.AddSeconds(30);
                    _cache.Set(lockoutKey, lockEnd, TimeSpan.FromSeconds(30));
                    errorMsg = "3 failed attempts. Account locked for 30 seconds.";
                }
                else if (currentFails >= 4)
                {
                    DateTime lockEnd = DateTime.Now.AddMinutes(5);
                    _cache.Set(lockoutKey, lockEnd, TimeSpan.FromMinutes(5));
                    errorMsg = "Too many failed attempts. Account locked for 5 minutes.";
                }

                ViewData["ErrorMessage"] = errorMsg;
                return View(model);
            }
        }

        // ==========================================
        // REGISTER ACTIONS
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> SendRegisterOtp([FromBody] string email)
        {
            if (string.IsNullOrEmpty(email))
                return Json(new { success = false, message = "Please enter an email address." });

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return Json(new { success = false, message = "This email is already registered." });
            }

            var otp = new Random().Next(100000, 999999).ToString();
            _cache.Set($"OTP_{email}", otp, TimeSpan.FromMinutes(5));
            await _otpService.SendOtpEmail(email, otp);

            return Json(new { success = true, message = "OTP code sent to your email." });
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                string otpKey = $"OTP_{model.Email}";
                if (!_cache.TryGetValue(otpKey, out string storedOtp) || storedOtp != model.Otp)
                {
                    ModelState.AddModelError("Otp", "Invalid or expired verification code.");
                    return View(model);
                }

                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email is already taken.");
                    return View(model);
                }

                var user = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    PasswordHash = HashPassword(model.Password),
                    Role = "User",
                    CreatedDate = DateTime.Now,
                    ExpiryTime = DateTime.Now.AddDays(1)
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                _cache.Remove(otpKey);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        // ==========================================
        // PROFILE ACTIONS (ADDED BACK)
        // ==========================================

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");
            int userId = int.Parse(userIdStr);

            // Fetch User with all necessary inclusions for the dashboard
            var user = await _context.Users
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Package)
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Payments)
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Feedbacks)
                .Include(u => u.Feedbacks)
                    .ThenInclude(f => f.Booking)
                    .ThenInclude(b => b.Package)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return RedirectToAction("Login");

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,

                // Map Bookings
                MyBookings = user.Bookings.Select(b => new BookingDisplayModel
                {
                    BookingID = b.BookingID,
                    PackageTitle = b.Package?.PackageName ?? "Unknown Package",
                    BookingDate = b.BookingDate,
                    Status = b.BookingStatus,
                    TotalAmount = b.FinalAmount,
                    // Check if this booking has a feedback
                    IsReviewed = b.Feedbacks.Any()
                }).OrderByDescending(x => x.BookingDate).ToList(),

                // Map Reviews
                MyReviews = user.Feedbacks.Select(f => new ReviewDisplayModel
                {
                    PackageTitle = f.Booking?.Package?.PackageName ?? "Unknown Package",
                    Rating = f.Rating,
                    Comment = f.Comment,
                    CreatedDate = f.CreatedDate
                }).OrderByDescending(x => x.CreatedDate).ToList(),

                // Map Payment History (Flattened from Bookings)
                PaymentHistory = user.Bookings
                    .SelectMany(b => b.Payments)
                    .Select(p => new PaymentDisplayModel
                    {
                        PaymentID = p.PaymentID,
                        PaymentDate = p.PaymentDate,
                        PaymentMethod = p.PaymentMethod,
                        AmountPaid = p.AmountPaid,
                        PaymentStatus = p.PaymentStatus
                    }).OrderByDescending(x => x.PaymentDate).ToList()
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");
            int userId = int.Parse(userIdStr);

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Validate Name: Only letters and spaces allowed
            if (!System.Text.RegularExpressions.Regex.IsMatch(model.FullName, @"^[a-zA-Z\s]+$"))
            {
                TempData["ErrorMessage"] = "Update Failed: Name can only contain letters and spaces.";
                // Redirect back to the edit tab so they can try again
                return RedirectToAction("Profile", new { tab = "edit-profile" });
            }

            // Validate Phone: Must be numbers only, 9 to 11 digits
            if (!System.Text.RegularExpressions.Regex.IsMatch(model.Phone, @"^\d{9,11}$"))
            {
                TempData["ErrorMessage"] = "Update Failed: Phone must be 9-11 digits (numbers only).";
                return RedirectToAction("Profile", new { tab = "edit-profile" });
            }

            // Only update allowed fields
            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Refresh cookie if name changed
            if (User.Identity.Name != user.FullName)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            }

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Profile", new { tab = "details" });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ProfileViewModel model)
        {
            // Note: We only validate password fields here, ignoring others
            if (string.IsNullOrEmpty(model.CurrentPassword) ||
                string.IsNullOrEmpty(model.NewPassword) ||
                string.IsNullOrEmpty(model.ConfirmNewPassword))
            {
                TempData["SecurityErrorMessage"] = "All password fields are required.";
                return RedirectToAction("Profile", new { tab = "security" });
            }

            if (model.NewPassword != model.ConfirmNewPassword)
            {
                TempData["SecurityErrorMessage"] = "New password and confirmation do not match.";
                return RedirectToAction("Profile", new { tab = "security" });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = int.Parse(userIdStr);

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login");

            // Verify old password
            if (!VerifyPassword(model.CurrentPassword, user.PasswordHash))
            {
                TempData["SecurityErrorMessage"] = "Current password is incorrect.";
                return RedirectToAction("Profile", new { tab = "security" });
            }

            // Update to new password
            user.PasswordHash = HashPassword(model.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SecuritySuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("Profile", new { tab = "security" });
        }

        // ==========================================
        // FORGOT PASSWORD ACTIONS
        // ==========================================

        [HttpGet]
        public IActionResult ForgotPassword() { return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user != null)
                {
                    var token = Guid.NewGuid().ToString();
                    _cache.Set($"ResetToken_{token}", model.Email, TimeSpan.FromMinutes(15));
                    var resetLink = Url.Action("ResetPassword", "Auth", new { token = token, email = model.Email }, Request.Scheme);
                    await _emailService.SendPasswordResetLinkAsync(user.Email, user.FullName, resetLink);
                }
                TempData["SuccessMessage"] = "If an account exists, a reset link has been sent to your email.";
                return RedirectToAction("ForgotPassword");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Invalid password reset link.";
            }
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Verify Token in Cache
            if (!_cache.TryGetValue($"ResetToken_{model.Token}", out string cachedEmail) || cachedEmail != model.Email)
            {
                TempData["ErrorMessage"] = "This reset link is invalid or has expired.";
                return RedirectToAction("ForgotPassword");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // ============================================================
            // NEW LOGIC: Prevent using the same password
            // ============================================================
            if (VerifyPassword(model.NewPassword, user.PasswordHash))
            {
                // Use TempData to display the error in the alert box defined in your View
                TempData["ErrorMessage"] = "You cannot use your previous password. Please choose a new, more secure password.";
                return View(model);
            }
            // ============================================================

            user.PasswordHash = HashPassword(model.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Clear token after successful reset
            _cache.Remove($"ResetToken_{model.Token}");

            TempData["SuccessMessage"] = "Password has been reset successfully. Please login.";
            return RedirectToAction("Login");
        }

        // ==========================================
        // LOGOUT
        // ==========================================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // --- HELPERS ---

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                if (string.IsNullOrEmpty(storedHash) || !storedHash.StartsWith("$2"))
                {
                    return password == storedHash;
                }
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }
            catch (Exception)
            {
                return password == storedHash;
            }
        }
    }
}