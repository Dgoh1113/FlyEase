using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace FlyEase.Controllers
{
    public class PaymentController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public PaymentController(FlyEaseDbContext context)
        {
            _context = context;
        }

        // STEP 1: Customer Information
        [HttpGet]
        public async Task<IActionResult> CustomerInfo(int packageId, int? people = 1)
        {
            var package = await _context.Packages.FindAsync(packageId);
            if (package == null)
            {
                return NotFound();
            }

            var vm = new BookingViewModel
            {
                PackageID = packageId,
                PackageName = package.PackageName,
                PackagePrice = package.Price,
                NumberOfPeople = people ?? 1,
                TravelDate = DateTime.Now.AddDays(14),
                BasePrice = package.Price * (people ?? 1)
            };

            // Calculate initial discounts
            CalculateDiscounts(vm);

            // Store in session for multi-step process
            HttpContext.Session.SetObject("BookingData", vm);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CustomerInfo(BookingViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Update session data
                var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData") ?? model;
                bookingData.FullName = model.FullName;
                bookingData.Email = model.Email;
                bookingData.Phone = model.Phone;
                bookingData.SpecialRequests = model.SpecialRequests;

                HttpContext.Session.SetObject("BookingData", bookingData);

                return RedirectToAction("PaymentDetails");
            }

            // Reload package data if validation fails
            var package = _context.Packages.Find(model.PackageID);
            if (package != null)
            {
                model.PackageName = package.PackageName;
                model.PackagePrice = package.Price;
            }

            return View(model);
        }

        // STEP 2: Payment Details
        [HttpGet]
        public IActionResult PaymentDetails()
        {
            var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
            if (bookingData == null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(bookingData);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PaymentDetails(BookingViewModel model)
        {
            var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
            if (bookingData == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                // Update payment details in session
                bookingData.PaymentMethod = model.PaymentMethod;
                bookingData.CardNumber = model.CardNumber;
                bookingData.CardHolderName = model.CardHolderName;
                bookingData.ExpiryDate = model.ExpiryDate;
                bookingData.CVV = model.CVV;

                HttpContext.Session.SetObject("BookingData", bookingData);

                return RedirectToAction("Confirmation");
            }

            return View(bookingData);
        }

        // STEP 3: Confirmation
        [HttpGet]
        public IActionResult Confirmation()
        {
            var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
            if (bookingData == null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(bookingData);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessBooking()
        {
            var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
            if (bookingData == null)
            {
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    // For demo, create a temporary user or redirect to login
                    userId = 1; // Demo user ID
                }

                // Create booking
                var booking = new Booking
                {
                    UserID = userId.Value,
                    PackageID = bookingData.PackageID,
                    BookingDate = DateTime.Now,
                    TravelDate = bookingData.TravelDate,
                    NumberOfPeople = bookingData.NumberOfPeople,
                    TotalBeforeDiscount = bookingData.BasePrice,
                    TotalDiscountAmount = bookingData.DiscountAmount,
                    FinalAmount = bookingData.FinalAmount,
                    BookingStatus = "Confirmed"
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Create payment
                var payment = new Payment
                {
                    BookingID = booking.BookingID,
                    PaymentMethod = bookingData.PaymentMethod,
                    AmountPaid = bookingData.FinalAmount,
                    IsDeposit = false,
                    PaymentDate = DateTime.Now,
                    PaymentStatus = "Completed"
                };

                _context.Payments.Add(payment);

                // Update package availability
                var package = await _context.Packages.FindAsync(bookingData.PackageID);
                if (package != null)
                {
                    package.AvailableSlots -= bookingData.NumberOfPeople;
                }

                await _context.SaveChangesAsync();

                // Clear session
                HttpContext.Session.Remove("BookingData");

                return RedirectToAction("Success", new
                {
                    bookingId = booking.BookingID,
                    paymentId = payment.PaymentID
                });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error processing booking: {ex.Message}";
                return View("Confirmation", bookingData);
            }
        }

        // STEP 4: Success Page
        [HttpGet]
        public async Task<IActionResult> Success(int bookingId, int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Package)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId && p.BookingID == bookingId);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // =========================================================
        // === TEMPORARY DEVELOPER BYPASS (FOR TESTING REVIEWS) ===
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> DevQuickBook(int packageId)
        {
            // 1. Get Logged In User (Must be logged in!)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Content("Error: You must be logged in to use the bypass.");
            int userId = int.Parse(userIdClaim);

            // 2. Get Package Details
            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return Content("Error: Package not found.");

            // 3. Create a Mock Booking
            var booking = new Booking
            {
                UserID = userId,
                PackageID = packageId,
                BookingDate = DateTime.Now,
                TravelDate = DateTime.Now.AddDays(7), // Default to next week
                NumberOfPeople = 1,
                TotalBeforeDiscount = package.Price,
                TotalDiscountAmount = 0,
                FinalAmount = package.Price,
                BookingStatus = "Confirmed" // Auto-confirm so it shows in dashboard
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // 4. Create a Mock Payment
            var payment = new Payment
            {
                BookingID = booking.BookingID,
                PaymentMethod = "DevBypass",
                AmountPaid = package.Price,
                IsDeposit = false,
                PaymentDate = DateTime.Now,
                PaymentStatus = "Completed"
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // 5. Redirect to Success Page
            return RedirectToAction("Success", new { bookingId = booking.BookingID, paymentId = payment.PaymentID });
        }
        // =========================================================

        // Helper Methods
        private void CalculateDiscounts(BookingViewModel model)
        {
            decimal discount = 0;

            // Early Bird Discount (30 days in advance)
            if ((model.TravelDate - DateTime.Now).TotalDays >= 30)
            {
                discount += model.BasePrice * 0.10m; // 10% discount
            }

            // Bulk Discount (5+ people)
            if (model.NumberOfPeople >= 5)
            {
                discount += model.BasePrice * 0.15m; // 15% discount
            }

            model.DiscountAmount = discount;
            model.FinalAmount = model.BasePrice - discount;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }
    }

    // Session extension methods
    public static class SessionExtensions
    {
        public static void SetObject(this ISession session, string key, object value)
        {
            session.SetString(key, System.Text.Json.JsonSerializer.Serialize(value));
        }

        public static T GetObject<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : System.Text.Json.JsonSerializer.Deserialize<T>(value);
        }
    }
}