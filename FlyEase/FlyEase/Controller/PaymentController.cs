using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // REQUIRED for accessing Env Variables
using Stripe;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    public class PaymentController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IConfiguration _configuration; // Inject Configuration

        public PaymentController(FlyEaseDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ... [CustomerInfo GET/POST remain exactly the same as your original file] ...
        [HttpGet]
        public async Task<IActionResult> CustomerInfo(int packageId, int? people = 1)
        {
            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return NotFound();

            var vm = new BookingViewModel
            {
                PackageID = packageId,
                PackageName = package.PackageName,
                PackagePrice = package.Price,
                NumberOfPeople = people ?? 1,
                TravelDate = DateTime.Now.AddDays(14),
                BasePrice = package.Price * (people ?? 1)
            };

            CalculateDiscounts(vm);
            HttpContext.Session.SetObject("BookingData", vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerInfo(BookingViewModel model)
        {
            if (ModelState.IsValid)
            {
                var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
                if (bookingData == null)
                {
                    bookingData = model;
                    var package = await _context.Packages.FindAsync(model.PackageID);
                    if (package != null)
                    {
                        bookingData.PackageName = package.PackageName;
                        bookingData.PackagePrice = package.Price;
                        bookingData.BasePrice = package.Price * model.NumberOfPeople;
                    }
                    bookingData.TravelDate = model.TravelDate;
                    CalculateDiscounts(bookingData);
                }

                bookingData.FullName = model.FullName;
                bookingData.Email = model.Email;
                bookingData.Phone = model.Phone;
                bookingData.SpecialRequests = model.SpecialRequests;
                bookingData.TravelDate = model.TravelDate;
                bookingData.NumberOfPeople = model.NumberOfPeople;

                HttpContext.Session.SetObject("BookingData", bookingData);
                return RedirectToAction("PaymentDetails");
            }

            // Reload package data on error
            var packageReload = await _context.Packages.FindAsync(model.PackageID);
            if (packageReload != null)
            {
                model.PackageName = packageReload.PackageName;
                model.PackagePrice = packageReload.Price;
                model.BasePrice = packageReload.Price * model.NumberOfPeople;
                CalculateDiscounts(model);
            }
            return View(model);
        }

        // STEP 2: Payment Details
        [HttpGet]
        public IActionResult PaymentDetails()
        {
            var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
            if (bookingData == null || string.IsNullOrEmpty(bookingData.FullName))
            {
                TempData["Error"] = "Please complete customer information first";
                return RedirectToAction("Index", "Home");
            }
            return View(bookingData);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PaymentDetails(BookingViewModel model)
        {
            var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
            if (bookingData == null) return RedirectToAction("Index", "Home");

            if (string.IsNullOrWhiteSpace(model.CardHolderName))
            {
                ModelState.AddModelError("CardHolderName", "Card Holder Name is required.");
                return View(bookingData);
            }

            // Update session with payment info
            bookingData.PaymentMethod = model.PaymentMethod;
            bookingData.CardHolderName = model.CardHolderName;
            bookingData.StripeToken = model.StripeToken;

            // === ADD THIS LINE ===
            // Save the last 4 digits (passed safely from frontend) to the session
            bookingData.CardNumber = model.CardNumber;

            HttpContext.Session.SetObject("BookingData", bookingData);

            return RedirectToAction("Confirmation");
        }

        // STEP 3: Confirmation
        [HttpGet]
        public IActionResult Confirmation()
        {
            var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
            if (bookingData == null) return RedirectToAction("Index", "Home");
            return View(bookingData);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessBooking()
        {
            var bookingData = HttpContext.Session.GetObject<BookingViewModel>("BookingData");
            if (bookingData == null) return RedirectToAction("Index", "Home");

            try
            {
                if (bookingData.PaymentMethod == "Credit Card")
                {
                    // ---------------------------------------------------------
                    // SECURE IMPLEMENTATION: Read Key from Environment/UserSecrets
                    // ---------------------------------------------------------
                    var secretKey = _configuration["Stripe:SecretKey"];
                    // Safety Check: Ensure key exists and isn't the dummy text
                    if (string.IsNullOrEmpty(secretKey) || secretKey.Contains("SET_IN_ENVIRONMENT"))
                    {
                        throw new Exception("Stripe Secret Key is missing. Please check your Environment Variables or User Secrets.");
                    }

                    StripeConfiguration.ApiKey = secretKey;

                    var options = new ChargeCreateOptions
                    {
                        Amount = (long)(bookingData.FinalAmount * 100),
                        Currency = "myr",
                        Description = $"FlyEase Booking: {bookingData.PackageName}",
                        Source = bookingData.StripeToken,
                        ReceiptEmail = bookingData.Email,
                    };

                    var service = new ChargeService();
                    Charge charge = service.Create(options);

                    if (charge.Status != "succeeded")
                    {
                        throw new Exception($"Payment failed: {charge.FailureMessage}");
                    }
                }

                // Database Saving Logic
                var userId = GetCurrentUserId() ?? 1;

                var booking = new Booking
                {
                    UserID = userId,
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

                // Update Slots
                var package = await _context.Packages.FindAsync(bookingData.PackageID);
                if (package != null)
                {
                    package.AvailableSlots -= bookingData.NumberOfPeople;
                }

                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("BookingData");

                return RedirectToAction("Success", new { bookingId = booking.BookingID, paymentId = payment.PaymentID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error processing booking: {ex.Message}";
                return View("Confirmation", bookingData);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Success(int bookingId, int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking).ThenInclude(b => b.Package)
                .Include(p => p.Booking).ThenInclude(b => b.User)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId && p.BookingID == bookingId);

            if (payment == null) return NotFound();
            return View(payment);
        }

        private void CalculateDiscounts(BookingViewModel model)
        {
            decimal discount = 0;
            if ((model.TravelDate - DateTime.Now).TotalDays >= 30) discount += model.BasePrice * 0.10m;
            if (model.NumberOfPeople >= 5) discount += model.BasePrice * 0.15m;
            model.DiscountAmount = discount;
            model.FinalAmount = model.BasePrice - discount;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }
    }

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