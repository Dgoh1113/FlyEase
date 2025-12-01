using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    public class BookingController : Controller
    {
        private readonly FlyEaseDbContext _db;
        public BookingController(FlyEaseDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Booking(int id)
        {
            // 1. Safety Check
            if (id <= 0) return RedirectToAction("Index", "Home");

            // 2. Fetch Data (Added .Include for Itinerary)
            var package = await _db.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Include(p => p.Itinerary) // <--- CRITICAL UPDATE
                .FirstOrDefaultAsync(p => p.PackageID == id);

            // 3. Safety Check
            if (package == null) return NotFound();

            // 4. Process Images
            var images = package.ImageURL?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

            // 5. Build ViewModel
            var viewModel = new PackageDetailsVM
            {
                PackageID = package.PackageID,
                PackageName = package.PackageName,
                Destination = package.Destination,
                Description = package.Description,
                Price = package.Price,
                StartDate = package.StartDate,
                EndDate = package.EndDate,
                AvailableSlots = package.AvailableSlots,
                CategoryName = package.Category?.CategoryName ?? "General",
                Inclusions = package.PackageInclusions.Select(pi => pi.InclusionItem).ToList(),

                // Map Itinerary (Ordered by Day)
                Itinerary = package.Itinerary.OrderBy(i => i.DayNumber).ToList(),

                // Image Logic
                AllImages = images,
                MainImage = images.FirstOrDefault() ?? "/img/default-package.jpg",
                GalleryImages = images.Skip(1).Take(4).ToList()
            };

            // 6. Send to View
            return View(viewModel);
        }
    }
}