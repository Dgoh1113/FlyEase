using FlyEase.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    [Authorize] // 1. Global Rule: Must be logged in to do ANYTHING here
    public class FeedbackController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public FeedbackController(FlyEaseDbContext context)
        {
            _context = context;
        }

        // GET: Create Review
        [HttpGet]
        public async Task<IActionResult> Create(int bookingId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var booking = await _context.Bookings
                .Include(b => b.Package)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.UserID == userId);

            if (booking == null)
            {
                return NotFound("Booking not found or you don't have permission.");
            }

            var existingFeedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.BookingID == bookingId);

            if (existingFeedback != null)
            {
                TempData["Error"] = "You have already reviewed this trip!";
                return RedirectToAction("Profile", "Auth");
            }

            return View(booking);
        }

        // POST: Submit Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int bookingId, int rating, string comment)
        {
            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "Please select a star rating between 1 and 5.";
                return RedirectToAction("Create", new { bookingId });
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var feedback = new Feedback
            {
                BookingID = bookingId,
                UserID = userId,
                Rating = rating,
                Comment = comment,
                CreatedDate = DateTime.Now
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thank you for your feedback!";
            return RedirectToAction("Profile", "Auth");
        }

        // ==========================================
        // ADMIN / STAFF VIEW (List of all feedback)
        // ==========================================
        [Authorize(Roles = "Admin,Staff")] // 2. SECURITY: Only Admin/Staff can see this
        public async Task<IActionResult> Index()
        {
            var feedbacks = await _context.Feedbacks
                .Include(f => f.Booking).ThenInclude(b => b.Package)
                .Include(f => f.User)
                .OrderByDescending(f => f.CreatedDate)
                .ToListAsync();

            return View(feedbacks);
        }

        // ==========================================
        // ANALYTICS DASHBOARD 
        // ==========================================
        [Authorize(Roles = "Admin,Staff")] // 3. SECURITY: Only Admin/Staff can see this
        public async Task<IActionResult> Analytics()
        {
            var data = await _context.Feedbacks
                .Include(f => f.Booking).ThenInclude(b => b.Package)
                .GroupBy(f => f.Booking.Package.PackageName)
                .Select(g => new FeedbackAnalyticsVM
                {
                    PackageName = g.Key,
                    AverageRating = g.Average(f => (double)f.Rating),
                    TotalReviews = g.Count(),
                    OneStarCount = g.Count(f => f.Rating == 1),
                    FiveStarCount = g.Count(f => f.Rating == 5)
                })
                .OrderByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.TotalReviews)
                .ToListAsync();

            return View(data);
        }
    }

    public class FeedbackAnalyticsVM
    {
        public string PackageName { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int OneStarCount { get; set; }
        public int FiveStarCount { get; set; }
    }
}