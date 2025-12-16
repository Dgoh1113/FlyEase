using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using X.PagedList;

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
        // 1. LANDING PAGE
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var destinations = await _context.Packages
                                             .AsNoTracking()
                                             .Where(p => !string.IsNullOrEmpty(p.Destination))
                                             .Select(p => p.Destination)
                                             .Distinct()
                                             .OrderBy(d => d)
                                             .ToListAsync();

            var sliderPackages = await _context.Packages
                                               .AsNoTracking()
                                               .Include(p => p.Category)
                                               .Where(p => p.AvailableSlots > 0 && !string.IsNullOrEmpty(p.ImageURL))
                                               .OrderByDescending(p => p.PackageID)
                                               .Take(5)
                                               .ToListAsync();

            var allPackages = await _context.Packages
                                            .AsNoTracking()
                                            .Include(p => p.Category)
                                            .Include(p => p.Bookings).ThenInclude(b => b.Feedbacks)
                                            .Where(p => p.AvailableSlots > 0)
                                            .ToListAsync();

            foreach (var p in allPackages)
            {
                var ratings = p.Bookings.SelectMany(b => b.Feedbacks).Select(f => f.Rating);
                p.AverageRating = ratings.Any() ? ratings.Average() : 0;
                p.ReviewCount = ratings.Count();
            }

            var categorizedPackages = allPackages.GroupBy(p => p.Category?.CategoryName ?? "General")
                                                 .OrderBy(g => g.Key);

            var viewModel = new HomeViewModel
            {
                Destinations = destinations,
                SliderPackages = sliderPackages,
                CategorizedPackages = categorizedPackages
            };

            return View(viewModel);
        }

        // ==========================================
        // 2. PACKAGES LISTING (UPDATED)
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
                .Include(p => p.Itinerary) // <--- FIX: Added this line to load Itinerary data
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
                .Include(p => p.Itinerary) // <--- Added here too just in case you use this action
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