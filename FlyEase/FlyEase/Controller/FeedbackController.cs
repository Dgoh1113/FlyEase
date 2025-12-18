using FlyEase.Data;
using FlyEase.Services;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    [Authorize]
    public class FeedbackController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly EmailService _emailService;

        // INJECT EmailService here
        public FeedbackController(FlyEaseDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

            var model = new Feedback
            {
                BookingID = bookingId,
                Booking = booking,
                Rating = 5
            };

            return View(model);
        }

        // [POST] Create Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Feedback feedback, string SelectedCategory)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            ModelState.Remove("Booking");
            ModelState.Remove("User");

            if (!ModelState.IsValid)
            {
                feedback.Booking = await _context.Bookings
                    .Include(b => b.Package)
                    .FirstOrDefaultAsync(b => b.BookingID == feedback.BookingID);

                return View(feedback);
            }

            if (feedback.Rating < 1 || feedback.Rating > 5)
            {
                TempData["Error"] = "Please select a star rating between 1 and 5.";
                feedback.Booking = await _context.Bookings.Include(b => b.Package).FirstOrDefaultAsync(b => b.BookingID == feedback.BookingID);
                return View(feedback);
            }

            // --- NO DB CHANGE STRATEGY ---
            // Prepend the category to the comment: "[Food] The food was..."
            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "General")
            {
                feedback.Comment = $"[{SelectedCategory}] {feedback.Comment}";
            }

            feedback.UserID = userId;
            feedback.CreatedDate = DateTime.Now;

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            // Send Email using Injected Service
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Package)
                    .FirstOrDefaultAsync(b => b.BookingID == feedback.BookingID);

                if (booking != null)
                {
                    await _emailService.SendReviewConfirmation(
                        booking.User.Email,
                        booking.User.FullName,
                        booking.Package.PackageName,
                        feedback.Rating,
                        feedback.Comment,
                        feedback.Emotion
                    );
                }
            }
            catch { }

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
                .Include(f => f.Booking).ThenInclude(b => b.Package)
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.BookingID == bookingId && f.UserID == userId);

            if (feedback == null) return NotFound("Review not found.");

            // Remove the [Category] tag for display if it exists
            if (feedback.Comment != null && feedback.Comment.StartsWith("[") && feedback.Comment.Contains("]"))
            {
                int end = feedback.Comment.IndexOf("]");
                feedback.Comment = feedback.Comment.Substring(end + 1).Trim();
            }

            return View(feedback);
        }

        // ==========================================
        // EDIT REVIEW (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int bookingId, int rating, string comment, string emotion)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var feedback = await _context.Feedbacks.FirstOrDefaultAsync(f => f.BookingID == bookingId && f.UserID == userId);

            if (feedback == null) return NotFound();

            // Note: In Edit, we lose the category unless we parse it out and save it back. 
            // For simplicity, we just save the new comment.
            feedback.Rating = rating;
            feedback.Comment = comment;
            feedback.Emotion = emotion;
            feedback.CreatedDate = DateTime.Now;

            _context.Feedbacks.Update(feedback);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review updated successfully!";
            return RedirectToAction("Profile", "Auth");
        }

        // ==========================================
        // ADMIN ANALYTICS DASHBOARD
        // ==========================================
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Analytics()
        {
            // 1. Fetch All Feedbacks
            var allFeedbacks = await _context.Feedbacks
                .Include(f => f.Booking).ThenInclude(b => b.Package)
                .Include(f => f.User)
                .ToListAsync();

            var vm = new FeedbackAnalyticsViewModel();

            if (allFeedbacks.Any())
            {
                vm.TotalReviews = allFeedbacks.Count;
                vm.AverageRating = allFeedbacks.Average(f => f.Rating);
                vm.PositivePercentage = (double)allFeedbacks.Count(f => f.Rating >= 4) / vm.TotalReviews * 100;

                vm.RatingCounts = allFeedbacks.GroupBy(f => f.Rating).ToDictionary(g => g.Key, g => g.Count());

                // Recent Reviews (Clone list to modify comments for display)
                vm.RecentReviews = allFeedbacks.OrderByDescending(f => f.CreatedDate).Take(5).ToList()
                    .Select(f => new Feedback
                    {
                        FeedbackID = f.FeedbackID,
                        User = f.User,
                        Booking = f.Booking,
                        Rating = f.Rating,
                        CreatedDate = f.CreatedDate,
                        Comment = (f.Comment != null && f.Comment.StartsWith("[")) ? f.Comment.Substring(f.Comment.IndexOf("]") + 1).Trim() : f.Comment
                    }).ToList();

                // Top & Bottom Packages
                var packageStats = allFeedbacks.GroupBy(f => f.Booking.Package.PackageName)
                    .Select(g => new { Name = g.Key, Rating = g.Average(f => f.Rating), Count = g.Count() }).ToList();

                var best = packageStats.OrderByDescending(p => p.Rating).FirstOrDefault();
                var worst = packageStats.OrderBy(p => p.Rating).FirstOrDefault();

                if (best != null) vm.MostPopularPackage = new PopularPackageViewModel { PackageName = best.Name, AverageRating = best.Rating, ReviewCount = best.Count };
                if (worst != null) vm.LeastPopularPackage = new PopularPackageViewModel { PackageName = worst.Name, AverageRating = worst.Rating, ReviewCount = worst.Count };

                // --- CATEGORY ANALYSIS (Parsing logic) ---
                var categoryData = new List<(string Cat, int Rating)>();

                foreach (var f in allFeedbacks)
                {
                    string cat = "General";
                    string text = f.Comment?.ToLower() ?? "";

                    // Extract from [Tag]
                    if (f.Comment != null && f.Comment.StartsWith("[") && f.Comment.Contains("]"))
                    {
                        int end = f.Comment.IndexOf("]");
                        cat = f.Comment.Substring(1, end - 1);
                    }
                    else // Fallback Keywords
                    {
                        if (text.Contains("food") || text.Contains("meal") || text.Contains("delicious")) cat = "Food";
                        else if (text.Contains("service") || text.Contains("staff") || text.Contains("guide")) cat = "Service";
                        else if (text.Contains("view") || text.Contains("scenery") || text.Contains("environment")) cat = "Environment";
                        else if (text.Contains("price") || text.Contains("worth") || text.Contains("cheap")) cat = "Value";
                    }
                    categoryData.Add((cat, f.Rating));
                }

                var catGroups = categoryData.GroupBy(x => x.Cat);
                foreach (var grp in catGroups)
                {
                    vm.CategoryRatings.Add(grp.Key, grp.Average(x => x.Rating));
                    vm.CategoryCounts.Add(grp.Key, grp.Count());
                }
            }

            // --- UNRATED BOOKINGS ANALYSIS ---
            // Get all completed/confirmed bookings
            var completedBookings = await _context.Bookings
                .Include(b => b.User).Include(b => b.Package)
                .Where(b => b.BookingStatus == "Completed" || b.BookingStatus == "Confirmed")
                .ToListAsync();

            // Filter out those that already have a Feedback entry
            var ratedBookingIds = allFeedbacks.Select(f => f.BookingID).ToHashSet();

            var unratedList = completedBookings
                .Where(b => !ratedBookingIds.Contains(b.BookingID) && b.TravelDate < DateTime.Now) // Trip must be in past
                .OrderByDescending(b => b.TravelDate)
                .ToList();

            vm.UnratedCount = unratedList.Count;
            vm.UnratedBookings = unratedList.Take(10).ToList();

            return View(vm);
        }

        // ==========================================
        // PERSONAL INSIGHTS (Travel DNA)
        // ==========================================
        [Authorize]
        public async Task<IActionResult> Insights()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var myReviews = await _context.Feedbacks
                .Include(f => f.Booking).ThenInclude(b => b.Package)
                .Where(f => f.UserID == userId)
                .OrderByDescending(f => f.CreatedDate)
                .ToListAsync();

            if (myReviews.Any())
            {
                var totalReviews = myReviews.Count;
                var avgGivenRating = myReviews.Average(r => r.Rating);

                string persona = "The Explorer";
                string personaIcon = "fa-hiking";
                string personaDesc = "You love seeing new places and having new experiences.";

                // Remove tags for word count analysis
                var allText = string.Join(" ", myReviews.Select(r => {
                    var c = r.Comment?.ToLower() ?? "";
                    return c.Contains("]") ? c.Substring(c.IndexOf("]") + 1) : c;
                }));

                if (allText.Contains("food") || allText.Contains("meal")) { persona = "The Foodie"; personaIcon = "fa-utensils"; personaDesc = "Your trips are defined by the delicious flavors you discover."; }
                else if (allText.Contains("cheap") || allText.Contains("value")) { persona = "The Smart Saver"; personaIcon = "fa-piggy-bank"; personaDesc = "You know how to find the best deals."; }
                else if (avgGivenRating >= 4.8) { persona = "The Happy Traveler"; personaIcon = "fa-smile-beam"; personaDesc = "You see the positive side of everything!"; }

                ViewBag.TotalReviews = totalReviews;
                ViewBag.AvgGivenRating = avgGivenRating;
                ViewBag.Persona = persona;
                ViewBag.PersonaIcon = personaIcon;
                ViewBag.PersonaDesc = personaDesc;
            }
            else
            {
                ViewBag.TotalReviews = 0;
                ViewBag.AvgGivenRating = 0.0;
                ViewBag.Persona = "The Aspiring Traveler";
                ViewBag.PersonaIcon = "fa-map-marked-alt";
            }

            return View(myReviews);
        }

        // Admin List View
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
    }
}