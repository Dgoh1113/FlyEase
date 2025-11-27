using Microsoft.AspNetCore.Mvc;

namespace FlyEase.Controllers
{
    public class BookingController : Controller
    {
        public IActionResult Booking()
        {
            return View();
        }
    }
}
