using FlyEase.Data;
using FlyEase.Services;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    public class AuthController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService; // For Password Reset (Interface)
        private readonly EmailService _otpService;    // For OTP (Concrete Class)

        // Constructor Injection
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

            // 1. CHECK: Is Account Currently Locked?
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

            // 2. Find User
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            // 3. CHECK: Account does not exist
            if (user == null)
            {
                ViewData["ErrorMessage"] = "Account does not exist.";
                return View(model);
            }

            // 4. CHECK: Is User Banned?
            if (user.Role == "Ban")
            {
                ViewData["ErrorMessage"] = "Your account has been banned. Please contact support.";
                return View(model);
            }

            // 5. Verify Password
            if (VerifyPassword(model.Password, user.PasswordHash))
            {
                // --- SUCCESS ---
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
                    return RedirectToAction("Index", "AdminDashboard");
                }

                return RedirectToAction("Index", "Home");
            }
            else
            {
                // --- FAILURE ---
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
        // REGISTER ACTIONS (With OTP)
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

            // Generate OTP
            var otp = new Random().Next(100000, 999999).ToString();

            // Store in Cache (5 minutes)
            _cache.Set($"OTP_{email}", otp, TimeSpan.FromMinutes(5));

            // Send Email using the OTP Service
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
                // 1. Verify OTP
                string otpKey = $"OTP_{model.Email}";
                if (!_cache.TryGetValue(otpKey, out string storedOtp) || storedOtp != model.Otp)
                {
                    ModelState.AddModelError("Otp", "Invalid or expired verification code.");
                    return View(model);
                }

                // 2. Check Email Uniqueness
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email is already taken.");
                    return View(model);
                }

                // 3. Create User
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

                // 4. Clear OTP
                _cache.Remove(otpKey);

                // 5. Auto Login
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
                    // Generate a unique token
                    var token = Guid.NewGuid().ToString();

                    // Store token in cache for 15 mins (Key: ResetToken_GUID -> Email)
                    _cache.Set($"ResetToken_{token}", model.Email, TimeSpan.FromMinutes(15));

                    // Generate Link
                    var resetLink = Url.Action("ResetPassword", "Auth", new { token = token, email = model.Email }, Request.Scheme);

                    // Send Email using IEmailService (Interface for Forgot Password)
                    await _emailService.SendPasswordResetLinkAsync(user.Email, user.FullName, resetLink);
                }

                // Show success message regardless to prevent email enumeration
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

            // 1. Verify Token from Cache
            if (!_cache.TryGetValue($"ResetToken_{model.Token}", out string cachedEmail) || cachedEmail != model.Email)
            {
                TempData["ErrorMessage"] = "This reset link is invalid or has expired.";
                return RedirectToAction("ForgotPassword");
            }

            // 2. Find User
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // 3. Update Password
            user.PasswordHash = HashPassword(model.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // 4. Invalidate Token
            _cache.Remove($"ResetToken_{model.Token}");

            TempData["SuccessMessage"] = "Password has been reset successfully. Please login.";
            return RedirectToAction("Login");
        }

        // ==========================================
        // OTHER ACTIONS
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