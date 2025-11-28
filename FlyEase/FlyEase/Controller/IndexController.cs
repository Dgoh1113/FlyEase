using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using System.Linq;
using System.Threading.Tasks;

namespace FlyEaseTravel.Controllers
{
    public class HomeController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public HomeController(FlyEaseDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Where(p => p.AvailableSlots > 0 && p.StartDate > System.DateTime.Now)
                .OrderByDescending(p => p.StartDate)
                .Take(3)
                .ToListAsync();

            return View(packages);
        }
    }
}