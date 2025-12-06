using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace FlyEase.Controllers
{
    public class LanguageController : Controller
    {
        // Allow both GET (for links) and POST (for forms)
        [AllowAnonymous]
        [HttpGet, HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            // 1. VALIDATION: Check if the requested culture is actually supported
            // This prevents errors if someone types ?culture=invalid
            var supportedCultures = new[] { "en", "ms", "zh-CN" };
            if (!supportedCultures.Contains(culture))
            {
                culture = "en"; // Default fallback
            }

            // 2. SET COOKIE
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,  // Required for GDPR compliance usually
                    SameSite = SameSiteMode.Strict // Security improvement
                }
            );

            // 3. SAFE REDIRECT
            // Check if returnUrl is valid and belongs to THIS website
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            // Fallback: If no return URL, go to Home Page
            return RedirectToAction("Index", "Home");
        }
    }
}