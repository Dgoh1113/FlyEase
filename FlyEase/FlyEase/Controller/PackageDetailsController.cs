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
    public class PackageDetailsController : Controller
    {
        private readonly FlyEaseDbContext _db;

        public PackageDetailsController(FlyEaseDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> PackageDetails(int id)
        {
            // 1. Safety Check
            if (id <= 0) return RedirectToAction("Index", "Home");

            // 2. Fetch Data
            var package = await _db.Packages
                .Include(p => p.Itinerary)
                .Include(p => p.PackageInclusions) // Fetch Inclusions from DB
                .Include(p => p.Bookings)
                    .ThenInclude(b => b.Feedbacks)
                .FirstOrDefaultAsync(p => p.PackageID == id);

            if (package == null) return NotFound();

            // 3. Process Images
            var images = package.ImageURL?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

            // 4. Calculate Ratings
            var allFeedbacks = package.Bookings.SelectMany(b => b.Feedbacks).ToList();
            double avgRating = allFeedbacks.Any() ? allFeedbacks.Average(f => f.Rating) : 0;

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
                Latitude = package.Latitude,
                Longitude = package.Longitude,

                // === POPULATE INCLUSIONS FROM DATABASE ===
                Inclusions = package.PackageInclusions
                    .Select(pi => new InclusionItemVM
                    {
                        Id = pi.InclusionID,      // Taking the ID
                        Name = pi.InclusionItem   // Taking the Name
                    })
                    .ToList(),

                // Itinerary Mapping
                Itinerary = package.Itinerary
                    .OrderBy(i => i.DayNumber)
                    .Select(i => new ItineraryViewModel
                    {
                        DayNumber = i.DayNumber,
                        Title = i.Title,
                        ActivityDescription = i.ActivityDescription
                    })
                    .ToList(),

                // Other Properties
                AverageRating = avgRating,
                TotalReviews = allFeedbacks.Count,
                AllImages = images,
                MainImage = images.FirstOrDefault() ?? "/img/default-package.jpg",
                GalleryImages = images.Skip(1).Take(4).ToList()
            };

            return View(viewModel);
        }
    }
}