using Microsoft.AspNetCore.Mvc;

namespace FlyEaseTravel.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Packages()
        {
            return View();
        }

        public IActionResult Discounts()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }
    }
}