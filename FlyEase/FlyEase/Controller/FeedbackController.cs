using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FlyEase.Controllers
{
    [Authorize] // Only logged-in users can access
    public class FeedbackController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public FeedbackController(FlyEaseDbContext context)
        {
            _context = context;
        }

        // GET: Show the "Write a Review" form
        [HttpGet]
        public async Task<IActionResult> Create(int bookingId)
        {
            // 1. Get the current logged-in User ID
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 2. Validate: Did this user actually make this booking?
            var booking = await _context.Bookings
                .Include(b => b.Package)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.UserID == userId);

            if (booking == null)
            {
                return NotFound("Booking not found or you don't have permission.");
            }

            // 3. Check: Has the user already reviewed this booking?
            var existingFeedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.BookingID == bookingId);

            if (existingFeedback != null)
            {
                TempData["Error"] = "You have already reviewed this trip!";
                return RedirectToAction("Profile", "Auth");
            }

            // 4. Send the Booking object to the view so we can show the Package Name
            return View(booking);
        }

        // POST: Save the data to Database
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int bookingId, int rating, string comment)
        {
            // 1. Basic Validation
            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "Please select a star rating between 1 and 5.";
                return RedirectToAction("Create", new { bookingId });
            }

            // 2. Get User ID again for security
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // 3. Create the Feedback Object
            var feedback = new Feedback
            {
                BookingID = bookingId,
                UserID = userId, // Note: Ensure you didn't delete UserID from the Model yet, or remove this line if you did.
                Rating = rating,
                Comment = comment,
                CreatedDate = DateTime.Now
            };

            // 4. Save to Database
            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thank you for your feedback!";
            return RedirectToAction("Profile", "Auth");
        }

        // ==========================================
        // ADMIN / STAFF VIEW (For your Requirement 6.3)
        // ==========================================
        [Authorize(Roles = "Admin,Staff")] // Restrict to staff only
        public async Task<IActionResult> Index()
        {
            var feedbacks = await _context.Feedbacks
                .Include(f => f.Booking)
                .ThenInclude(b => b.Package) // Link to Package so we know what they are rating
                .Include(f => f.User)        // Link to User so we know who wrote it
                .OrderByDescending(f => f.CreatedDate)
                .ToListAsync();

            return View(feedbacks);
        }
    }
}