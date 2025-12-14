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
                .Include(b => b.User)
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

        // [POST] Create Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        // 1. Updated to accept 'emotion'
        public async Task<IActionResult> Create(int bookingId, int rating, string comment, string emotion)
        {
            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "Please select a star rating between 1 and 5.";
                return RedirectToAction("Create", new { bookingId });
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 2. Save Feedback (including Emotion)
            var feedback = new Feedback
            {
                BookingID = bookingId,
                UserID = userId,
                Rating = rating,
                Emotion = emotion, // <--- Added here
                Comment = comment,
                CreatedDate = DateTime.Now
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            // 3. Send "Thank You" Email (Preserved Logic)
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Package)
                    .FirstOrDefaultAsync(b => b.BookingID == bookingId);
                // ... inside [HttpPost] Create method ...

                if (booking != null)
                {
                    var emailService = new FlyEase.Services.EmailService();
                    await emailService.SendReviewConfirmation(
                        booking.User.Email,
                        booking.User.FullName,
                        booking.Package.PackageName,
                        rating,
                        comment,
                        emotion // <--- Add this argument
                    );
                }
            }
            catch
            {
                // Ignore email errors
            }

            TempData["SuccessMessage"] = "Thank you for your feedback! A confirmation email has been sent.";
            return RedirectToAction("Profile", "Auth");
        }

        // ==========================================
        // EDIT REVIEW (GET)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(int bookingId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

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
        // 1. Add 'string emotion' to the parameters
        public async Task<IActionResult> Edit(int bookingId, int rating, string comment, string emotion)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var feedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.BookingID == bookingId && f.UserID == userId);

            if (feedback == null) return NotFound();

            feedback.Rating = rating;
            feedback.Comment = comment;
            feedback.Emotion = emotion; // <--- 2. Update Emotion
            feedback.CreatedDate = DateTime.Now;

            _context.Feedbacks.Update(feedback);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review updated successfully!";
            return RedirectToAction("Profile", "Auth");
        }

        // ==========================================
        // NEW FEATURE: PERSONAL TRAVEL INSIGHTS
        // Analysis of the user's own review habits
        // ==========================================
        [Authorize]
        public async Task<IActionResult> Insights()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 1. Fetch all reviews by this user
            var myReviews = await _context.Feedbacks
                .Include(f => f.Booking)
                .ThenInclude(b => b.Package)
                .Where(f => f.UserID == userId)
                .OrderByDescending(f => f.CreatedDate)
                .ToListAsync();

            // 2. CHECK IF REVIEWS EXIST
            if (myReviews.Any())
            {
                // LOGIC: Calculate Statistics
                var totalReviews = myReviews.Count;
                var avgGivenRating = myReviews.Average(r => r.Rating);
                var totalWords = myReviews.Sum(r => r.Comment?.Split(' ').Length ?? 0);

                // LOGIC: Determine "Travel Persona" based on Keywords & Ratings
                string persona = "The Explorer"; // Default
                string personaIcon = "fa-hiking";
                string personaDesc = "You love seeing new places and having new experiences.";

                // Join all comments to analyze keywords
                var allText = string.Join(" ", myReviews.Select(r => r.Comment?.ToLower() ?? ""));

                if (allText.Contains("food") || allText.Contains("meal") || allText.Contains("delicious"))
                {
                    persona = "The Foodie";
                    personaIcon = "fa-utensils";
                    personaDesc = "Your trips are defined by the delicious flavors you discover.";
                }
                else if (allText.Contains("cheap") || allText.Contains("price") || allText.Contains("value"))
                {
                    persona = "The Smart Saver";
                    personaIcon = "fa-piggy-bank";
                    personaDesc = "You know how to find the best deals and get the most value.";
                }
                else if (allText.Contains("service") || allText.Contains("staff") || allText.Contains("friendly"))
                {
                    persona = "The People Person";
                    personaIcon = "fa-users";
                    personaDesc = "For you, good service and friendly faces make the trip.";
                }
                else if (avgGivenRating >= 4.5)
                {
                    persona = "The Happy Traveler";
                    personaIcon = "fa-smile-beam";
                    personaDesc = "You tend to see the positive side of every journey!";
                }
                else if (avgGivenRating <= 2.5)
                {
                    persona = "The Critical Critic";
                    personaIcon = "fa-gavel";
                    personaDesc = "You have high standards and expect the best quality.";
                }

                // Pass Data to View
                ViewBag.TotalReviews = totalReviews;
                ViewBag.AvgGivenRating = avgGivenRating;
                ViewBag.TotalWords = totalWords;
                ViewBag.Persona = persona;
                ViewBag.PersonaIcon = personaIcon;
                ViewBag.PersonaDesc = personaDesc;
            }
            else
            {
                // DEFAULT DATA FOR NO REVIEWS
                ViewBag.TotalReviews = 0;
                ViewBag.AvgGivenRating = 0.0;
                ViewBag.TotalWords = 0;
                ViewBag.Persona = "The Aspiring Traveler";
                ViewBag.PersonaIcon = "fa-map-marked-alt";
                ViewBag.PersonaDesc = "Your journey is just beginning. Book a trip to see your travel persona!";
            }

            return View(myReviews);
        }

        // ==========================================
        // ADMIN / STAFF VIEW (List of all feedback)
        // ==========================================
        [Authorize(Roles = "Admin,Staff")]
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
        [Authorize(Roles = "Admin,Staff")]
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

    } // <--- END OF CLASS FeedbackController

    // Helper Class
    public class FeedbackAnalyticsVM
    {
        public string PackageName { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int OneStarCount { get; set; }
        public int FiveStarCount { get; set; }
    }

}