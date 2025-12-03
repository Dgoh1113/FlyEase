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

        // GET: /Home/Index
        public async Task<IActionResult> Index(string searchTerm, string destination, int? categoryId, decimal? minPrice, decimal? maxPrice)
        {
            // 1. Base Query
            var query = _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Include(p => p.Itinerary)
                .Include(p => p.Bookings)
                .ThenInclude(b => b.Feedbacks)
                .Where(p => p.AvailableSlots > 0);

            // 2. Apply Filters
            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(p => p.PackageName.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm)));

            if (!string.IsNullOrEmpty(destination))
                query = query.Where(p => p.Destination.Contains(destination));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId);

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            // 3. Load Data for View (Categories for dropdown)
            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Destination = destination;
            ViewBag.CategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            var packages = await query.OrderByDescending(p => p.PackageID).ToListAsync();

            // 4. Calculate Ratings
            foreach (var p in packages)
            {
                var ratings = p.Bookings.SelectMany(b => b.Feedbacks).Select(f => f.Rating);
                p.AverageRating = ratings.Any() ? ratings.Average() : 0;
            }

            return View(packages);
        }

        public async Task<IActionResult> Packages()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Include(p => p.Itinerary)
                .Where(p => p.AvailableSlots > 0)
                .OrderByDescending(p => p.PackageID)
                .ToListAsync();

            return View(packages);
        }

        public async Task<IActionResult> PackageDetails(int id)
        {
            var package = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Include(p => p.Itinerary)
                .FirstOrDefaultAsync(m => m.PackageID == id);

            if (package == null) return NotFound();

            return View(package);
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Discounts()
        {
            return RedirectToAction("Discounts", "Discount");
        }
    }
}