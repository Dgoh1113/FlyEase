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
        public async Task<IActionResult> Index(string searchTerm, string destination, int? categoryId, decimal? minPrice, decimal? maxPrice)
        {
            // 1. Start the query with basic inclusions and availability check
            var query = _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Where(p => p.AvailableSlots > 0);

            // 2. Apply Filters (Logically based on your Db.cs)

            // Search by Name or Description
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.PackageName.Contains(searchTerm) ||
                                       (p.Description != null && p.Description.Contains(searchTerm)));
            }

            // Search by Destination
            if (!string.IsNullOrEmpty(destination))
            {
                query = query.Where(p => p.Destination.Contains(destination));
            }

            // Filter by Category
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryID == categoryId);
            }

            // Filter by Price Range
            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            // 3. Load Categories for the View's Dropdown
            ViewBag.Categories = await _context.PackageCategories.ToListAsync();

            // 4. Persist search values so they don't disappear from the inputs
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Destination = destination;
            ViewBag.CategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            // 5. Execute Query
            // Note: I removed .Take(6) so the user can see all results when searching
            var packages = await query.OrderByDescending(p => p.PackageID).ToListAsync();

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
    }
}
