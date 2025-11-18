using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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








