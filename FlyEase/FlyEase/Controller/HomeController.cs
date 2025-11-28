// [file name]: HomeController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using System.Linq;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    public class HomeController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public HomeController(FlyEaseDbContext context)
        {
            _context = context;
        }

        // GET: /
        // GET: /Home
        // GET: /Home/Index
        public async Task<IActionResult> Index()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Where(p => p.AvailableSlots > 0)
                .OrderByDescending(p => p.PackageID)
                .Take(6)
                .ToListAsync();

            return View(packages);
        }

        // GET: /Home/Packages
        public async Task<IActionResult> Packages()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Where(p => p.AvailableSlots > 0)
                .OrderByDescending(p => p.PackageID)
                .ToListAsync();

            return View(packages);
        }

        // GET: /Home/Contact
        public IActionResult Contact()
        {
            return View();
        }

        // GET: /Home/Discounts
        public IActionResult Discounts()
        {
            return RedirectToAction("Discounts", "Discount");
        }

        // In HomeController.cs
        public IActionResult DebugPayment()
        {
            return View();
        }
    }
}
