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
                .Include(b => b.User) // <--- ADD THIS LINE!
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.UserID == userId);

            if (booking == null)
            {
                return NotFound("Booking not found or you don't have permission.");
            }

            var existingFeedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.BookingID == bookingId);

            if (existingFeedback != null)
            {
                TempData["ErrorMessage"] = "You have already reviewed this trip!";
                return RedirectToAction("Profile", "Auth");
            }

            return View(booking);
        }

        // ==========================================
        // EDIT REVIEW (GET)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(int bookingId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 1. Find the existing feedback
            var feedback = await _context.Feedbacks
                .Include(f => f.Booking)
                    .ThenInclude(b => b.Package)
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.BookingID == bookingId && f.UserID == userId);

            if (feedback == null)
            {
                return NotFound("Review not found or you don't have permission.");
            }

            return View(feedback);
        }

        // ==========================================
        // EDIT REVIEW (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int bookingId, int rating, string comment)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var feedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.BookingID == bookingId && f.UserID == userId);

            if (feedback == null) return NotFound();

            // Update fields
            feedback.Rating = rating;
            feedback.Comment = comment;
            feedback.CreatedDate = DateTime.Now; // Optional: Update date to now

            _context.Feedbacks.Update(feedback);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review updated successfully!";
            return RedirectToAction("Profile", "Auth");
        }

        // [file]: Controllers/FeedbackController.cs

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

            // 1. Save Feedback
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

            // =========================================================================
            // 2. NEW: Send "Thank You" Email
            // =========================================================================
            try
            {
                // We need to fetch the User and Package details to personalize the email
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Package)
                    .FirstOrDefaultAsync(b => b.BookingID == bookingId);

                if (booking != null)
                {
                    var emailService = new FlyEase.Services.EmailService();
                    await emailService.SendReviewConfirmation(
                        booking.User.Email,      // To: User's Email
                        booking.User.FullName,   // Name
                        booking.Package.PackageName, // Package
                        rating,                  // Rating
                        comment                  // What they wrote
                    );
                }
            }
            catch
            {
                // Ignore email errors so the user still sees the "Success" screen
            }
            // =========================================================================

            TempData["SuccessMessage"] = "Thank you for your feedback! A confirmation email has been sent.";
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