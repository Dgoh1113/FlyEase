using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels; // Ensure you have the updated HomeViewModel
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using X.PagedList; // Ensure X.PagedList.Mvc.Core is installed

namespace FlyEase.Controllers
{
    public class HomeController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public HomeController(FlyEaseDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. LANDING PAGE (Sidebar + Slider + Cards)
        // ==========================================
        public async Task<IActionResult> Index()
        {
            // A. Fetch Distinct Destinations for the Left Sidebar
            var destinations = await _context.Packages
                                             .AsNoTracking()
                                             .Where(p => !string.IsNullOrEmpty(p.Destination))
                                             .Select(p => p.Destination)
                                             .Distinct()
                                             .OrderBy(d => d)
                                             .ToListAsync();

            // B. Fetch Recent/Featured Packages for the Top Slider
            // logic: Available slots > 0 AND ImageURL is not empty
            var sliderPackages = await _context.Packages
                                               .AsNoTracking()
                                               .Include(p => p.Category)
                                               // ImageURL is a string column, so we don't .Include() it, just check it
                                               .Where(p => p.AvailableSlots > 0 && !string.IsNullOrEmpty(p.ImageURL))
                                               .OrderByDescending(p => p.PackageID)
                                               .Take(5)
                                               .ToListAsync();

            // C. Fetch All Packages for the Bottom Categorized Rows
            // We fetch them all and then group them in memory or logic
            var allPackages = await _context.Packages
                                            .AsNoTracking()
                                            .Include(p => p.Category)
                                            .Include(p => p.Bookings).ThenInclude(b => b.Feedbacks) // For Rating calculation
                                            .Where(p => p.AvailableSlots > 0)
                                            .ToListAsync();

            // Calculate Ratings for the cards
            foreach (var p in allPackages)
            {
                var ratings = p.Bookings.SelectMany(b => b.Feedbacks).Select(f => f.Rating);
                p.AverageRating = ratings.Any() ? ratings.Average() : 0;
                p.ReviewCount = ratings.Count();
            }

            // Group by Category Name
            var categorizedPackages = allPackages.GroupBy(p => p.Category?.CategoryName ?? "General")
                                                 .OrderBy(g => g.Key);

            // D. Populate ViewModel
            var viewModel = new HomeViewModel
            {
                Destinations = destinations,
                SliderPackages = sliderPackages,
                CategorizedPackages = categorizedPackages
            };

            return View(viewModel);
        }

        // ==========================================
        // 2. PACKAGES LISTING (View All / Search)
        // ==========================================
        public async Task<IActionResult> Packages(string searchTerm, string destination, int? categoryId, decimal? maxPrice, DateTime? startDate, DateTime? endDate, int? page)
        {
            int pageSize = 12;
            int pageNumber = page ?? 1;

            // 1. Base Query
            var query = _context.Packages
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
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

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            if (startDate.HasValue)
                query = query.Where(p => p.StartDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(p => p.StartDate <= endDate.Value);

            // 3. Load View Data for Dropdowns
            ViewBag.Categories = await _context.PackageCategories.ToListAsync();

            ViewBag.Destinations = await _context.Packages
                .Where(p => !string.IsNullOrEmpty(p.Destination))
                .Select(p => p.Destination)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            // Pass filters back to View to keep inputs filled
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Destination = destination;
            ViewBag.CategoryId = categoryId;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            // 4. Pagination & Ratings
            var totalItems = await query.CountAsync();

            var pagedData = await query
                .OrderByDescending(p => p.PackageID)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            foreach (var p in pagedData)
            {
                var ratings = p.Bookings.SelectMany(b => b.Feedbacks).Select(f => f.Rating);
                p.AverageRating = ratings.Any() ? ratings.Average() : 0;
                p.ReviewCount = ratings.Count();
            }

            var pagedList = new StaticPagedList<Package>(pagedData, pageNumber, pageSize, totalItems);

            return View(pagedList);
        }

        // ==========================================
        // 3. PACKAGE DETAILS
        // ==========================================
        public async Task<IActionResult> PackageDetails(int id)
        {
            var package = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Include(p => p.Bookings)
                    .ThenInclude(b => b.Feedbacks)
                .ThenInclude(f => f.User)
                .FirstOrDefaultAsync(m => m.PackageID == id);

            if (package == null) return NotFound();

            var ratings = package.Bookings.SelectMany(b => b.Feedbacks).Select(f => f.Rating);
            package.AverageRating = ratings.Any() ? ratings.Average() : 0;
            package.ReviewCount = ratings.Count();

            return View(package);
        }

        public IActionResult Contact() { return View(); }

        public IActionResult Discounts() { return RedirectToAction("Discounts", "Discount"); }
    }
}