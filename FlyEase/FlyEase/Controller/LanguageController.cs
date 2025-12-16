using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http; // Added for CookieOptions
using System; // Added for DateTimeOffset

namespace FlyEase.Controllers
{
    public class LanguageController : Controller
    {
        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            // This creates the standard cookie: ".AspNetCore.Culture"
            // The middleware in Program.cs reads this cookie automatically.
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            // Safety check: If returnUrl is empty, go to Home
            if (string.IsNullOrEmpty(returnUrl))
            {
                return RedirectToAction("Index", "Home");
            }

            return LocalRedirect(returnUrl);
        }
    }
}