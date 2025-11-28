using FlyEase.Data;
using FlyEase.Migrations;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
        public BookingController(FlyEaseDbContext db) {
            _db = db;
        }

        public async Task<IActionResult> Booking(int id)
        {
            // 1. Safety Check: If ID is missing, go back to Home
            if (id <= 0) return RedirectToAction("Index", "Home");

            // 2. Fetch Data from Database
            var package = await _db.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .FirstOrDefaultAsync(p => p.PackageID == id);

            // 3. Safety Check: If package not found in DB
            if (package == null) return NotFound();

            // 4. Process Images
            var images = package.ImageURL?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

            // 5. Build ViewModel
            var viewModel = new BookingVM
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

