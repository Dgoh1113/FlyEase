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

            // 2. Fetch Data (Includes Itinerary + Reviews)
            var package = await _db.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .Include(p => p.Itinerary) // Friend's Itinerary
                .Include(p => p.Bookings)  // Your Reviews/Feedbacks
                    .ThenInclude(b => b.Feedbacks)
                        .ThenInclude(f => f.User)
                .FirstOrDefaultAsync(p => p.PackageID == id);

            // 3. Safety Check
            if (package == null) return NotFound();

            // 4. Calculate Ratings & Fetch Lists
            var allFeedbacks = package.Bookings
                .SelectMany(b => b.Feedbacks)
                .OrderByDescending(f => f.CreatedDate)
                .ToList();

            double avgRating = allFeedbacks.Any() ? allFeedbacks.Average(f => f.Rating) : 0;
            int totalReviews = allFeedbacks.Count;

            var topReview = allFeedbacks
                .OrderByDescending(f => f.Rating)
                .FirstOrDefault();

            // 5. Process Images
            var images = package.ImageURL?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

            // 6. Build ViewModel
            var viewModel = new BookingVM
            {
                // Basic Details
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

                // === 1. NEW: Map Coordinates (From Database) ===
                Latitude = package.Latitude,
                Longitude = package.Longitude,

                // === 2. NEW: Dummy Data for Accommodation & AddOns ===
                // (Since these tables don't exist in your DB yet, we hardcode them for now so the UI shows up)
                AccommodationList = new List<string> { "Standard Room", "Deluxe Suite", "Family Studio" },
                AddOnList = new List<string> { "Airport Pickup", "Breakfast Buffet", "Travel Insurance" },

                // Itinerary Logic
                Itinerary = package.Itinerary.OrderBy(i => i.DayNumber).ToList(),

                // Review Stats
                AverageRating = avgRating,
                TotalReviews = totalReviews,

                // Top Review
                TopReviewComment = topReview?.Comment,
                TopReviewUser = topReview?.User?.FullName ?? "Anonymous",
                TopReviewRating = topReview?.Rating,

                // All Reviews List
                AllReviews = allFeedbacks,

                // Image Logic
                AllImages = images,
                MainImage = images.FirstOrDefault() ?? "/img/default-package.jpg",
                GalleryImages = images.Skip(1).Take(4).ToList()
            };

            // 7. Send to View
            return View(viewModel);
        }
    }
}