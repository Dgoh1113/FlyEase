using Microsoft.AspNetCore.Mvc;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace FlyEase.Controllers
{
    public class AuthController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IMemoryCache _cache; // Inject Cache

        public AuthController(FlyEaseDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
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

            // 5. Verify Password (Robust Check)
            if (VerifyPassword(model.Password, user.PasswordHash))
            {
                // --- SUCCESS ---

                // Clear any fail counts on success
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
                // --- FAILURE (Wrong Password) ---

                // Get current fail count (default 0)
                int currentFails = _cache.GetOrCreate(failCountKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(10); // Reset count after 10 mins of inactivity
                    return 0;
                });

                currentFails++;
                _cache.Set(failCountKey, currentFails); // Update count

                string errorMsg = "Invalid password.";

                // LOCKOUT & COUNTDOWN LOGIC
                if (currentFails < 3)
                {
                    // Show countdown warnings
                    int remaining = 3 - currentFails;
                    errorMsg = $"Invalid password. {remaining} attempt{(remaining > 1 ? "s" : "")} remaining before temporary lockout.";
                }
                else if (currentFails == 3)
                {
                    // Lock for 30 seconds
                    DateTime lockEnd = DateTime.Now.AddSeconds(30);
                    _cache.Set(lockoutKey, lockEnd, TimeSpan.FromSeconds(30));
                    errorMsg = "3 failed attempts. Account locked for 30 seconds.";
                }
                else if (currentFails >= 4)
                {
                    // Lock for 5 minutes (if they fail again after the 30s lock)
                    DateTime lockEnd = DateTime.Now.AddMinutes(5);
                    _cache.Set(lockoutKey, lockEnd, TimeSpan.FromMinutes(5));
                    errorMsg = "Too many failed attempts. Account locked for 5 minutes.";
                }

                ViewData["ErrorMessage"] = errorMsg;
                return View(model);
            }
        }

        // ==========================================
        // OTHER ACTIONS (Register, Logout, etc.)
        // ==========================================
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

                // Auto-login after register
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

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword() { return View(); }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
            {
                ModelState.AddModelError("", "Invalid password reset token");
            }
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        // --- HELPERS ---

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // FIXED: Safe verification that handles legacy plain text passwords without crashing
        private bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                // If storedHash is empty or likely plain text (doesn't start with BCrypt identifier), compare directly
                if (string.IsNullOrEmpty(storedHash) || !storedHash.StartsWith("$2"))
                {
                    return password == storedHash;
                }

                // Attempt standard BCrypt verify
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }
            catch (Exception)
            {
                // If BCrypt crashes (e.g., SaltParseException), assume stored value is plain text
                return password == storedHash;
            }
        }
    }
}