using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace FlyEase.Controllers
{
    public class PaymentController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IConfiguration _configuration;

        public PaymentController(FlyEaseDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;

            // SET REAL STRIPE KEY
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        // ========== STEP 1: CUSTOMER INFO ==========
        [HttpGet]
        public async Task<IActionResult> CustomerInfo(int packageId, int people = 1)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["Error"] = "Please login first";
                return RedirectToAction("Login", "Auth");
            }

            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return NotFound();

            var vm = new CustomerInfoViewModel
            {
                PackageID = packageId,
                PackageName = package.PackageName,
                PackagePrice = package.Price,
                NumberOfPeople = people,
                TravelDate = DateTime.Now.AddDays(14),
                BasePrice = package.Price * people,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address
            };

            CalculateDiscounts(vm);

            HttpContext.Session.SetCustomerInfo(vm);
            HttpContext.Session.SetUserId(user.UserID);

            return View(vm);
        }

        [HttpPost]
        public IActionResult CustomerInfo(CustomerInfoViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            CalculateDiscounts(model);

            HttpContext.Session.SetCustomerInfo(model);
            return RedirectToAction("PaymentMethod");
        }

        // ========== STEP 2: PAYMENT METHOD ==========
        [HttpGet]
        public IActionResult PaymentMethod()
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null)
            {
                TempData["Error"] = "Please complete customer info";
                return RedirectToAction("CustomerInfo", new { packageId = 1 });
            }

            ViewBag.PackageName = customerInfo.PackageName;
            ViewBag.FinalAmount = customerInfo.FinalAmount;
            return View();
        }

        [HttpPost]
        public IActionResult PaymentMethod(string paymentMethod)
        {
            if (string.IsNullOrEmpty(paymentMethod))
            {
                TempData["Error"] = "Please select payment method";
                return RedirectToAction("PaymentMethod");
            }

            HttpContext.Session.SetPaymentMethod(paymentMethod);

            return paymentMethod == "card"
                ? RedirectToAction("CardPayment")
                : RedirectToAction("ManualPayment", new { method = paymentMethod });
        }

        // ========== STEP 3A: CARD PAYMENT ==========
        [HttpGet]
        public IActionResult CardPayment()
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null)
            {
                TempData["Error"] = "Session expired";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.FinalAmount = customerInfo.FinalAmount;
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CardPayment(string cardNumber, string expiry, string cvv, string cardholder)
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null)
            {
                TempData["Error"] = "Session expired";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // 1. CREATE STRIPE PAYMENT INTENT
                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(customerInfo.FinalAmount * 100),
                    Currency = "myr",
                    PaymentMethodTypes = new List<string> { "card" },
                    Description = $"FlyEase Booking: {customerInfo.PackageName}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "customer_name", customerInfo.FullName },
                        { "customer_email", customerInfo.Email },
                        { "package_id", customerInfo.PackageID.ToString() },
                        { "package_name", customerInfo.PackageName }
                    }
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                // 2. GET INTENT ID
                string realStripeId = paymentIntent.Id;

                // 3. PROCESS PAYMENT
                return await ProcessBooking("card", realStripeId);
            }
            catch (StripeException ex)
            {
                TempData["Error"] = $"Stripe Error: {ex.StripeError.Message}";
                return RedirectToAction("CardPayment");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Payment Error: {ex.Message}";
                return RedirectToAction("CardPayment");
            }
        }

        // ========== STEP 3B: MANUAL PAYMENT ==========
        [HttpGet]
        public IActionResult ManualPayment(string method)
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null)
            {
                TempData["Error"] = "Session expired";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.PaymentMethod = method;
            ViewBag.FinalAmount = customerInfo.FinalAmount;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ManualPaymentPost(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                TempData["Error"] = "Please enter reference number";
                return RedirectToAction("ManualPayment");
            }

            var method = HttpContext.Session.GetPaymentMethod();
            return await ProcessBooking(method, reference);
        }

        // ========== PROCESS BOOKING ==========
        private async Task<IActionResult> ProcessBooking(string paymentMethod, string transactionId)
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            var userId = HttpContext.Session.GetUserId();

            if (customerInfo == null || userId == 0)
            {
                TempData["Error"] = "Session expired";
                return RedirectToAction("Index", "Home");
            }

            var package = await _context.Packages.FindAsync(customerInfo.PackageID);
            if (package == null || package.AvailableSlots < customerInfo.NumberOfPeople)
            {
                TempData["Error"] = "Package not available";
                return RedirectToAction("Index", "Home");
            }

            // Create booking
            var booking = new Booking
            {
                UserID = userId,
                PackageID = customerInfo.PackageID,
                BookingDate = DateTime.Now,
                TravelDate = customerInfo.TravelDate,
                NumberOfPeople = customerInfo.NumberOfPeople,
                TotalBeforeDiscount = customerInfo.BasePrice,
                TotalDiscountAmount = customerInfo.DiscountAmount,
                FinalAmount = customerInfo.FinalAmount,
                // === CHANGE 1: Always Pending ===
                BookingStatus = "Pending"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // Create payment
            string paymentMethodText = GetPaymentMethodName(paymentMethod);

            if (paymentMethod == "card")
            {
                if (transactionId.StartsWith("pi_"))
                {
                    paymentMethodText = $"Credit Card (Stripe: {transactionId})";
                }
                else
                {
                    paymentMethodText = $"Credit Card (Ref: {transactionId})";
                }
            }

            var payment = new Payment
            {
                BookingID = booking.BookingID,
                PaymentMethod = paymentMethodText,
                AmountPaid = customerInfo.FinalAmount,
                IsDeposit = false,
                PaymentDate = DateTime.Now,
                PaymentStatus = paymentMethod == "card" ? "Completed" : "Pending"
            };

            _context.Payments.Add(payment);

            // Only reduce slots for confirmed/paid payments
            if (paymentMethod == "card")
            {
                package.AvailableSlots -= customerInfo.NumberOfPeople;
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.ClearPaymentSession();

            return RedirectToAction("Success", new { bookingId = booking.BookingID });
        }

        // ========== SUCCESS PAGE ==========
        [HttpGet]
        public async Task<IActionResult> Success(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Package)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null) return NotFound();
            return View(booking);
        }

        // =========================================================
        // === DEVELOPER BYPASS (UPDATED TO PENDING) ===
        // =========================================================
        [HttpGet("Payment/DevQuickBook")]
        public async Task<IActionResult> DevQuickBook(int packageId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Content("Error: You must be logged in.");
            int userId = int.Parse(userIdClaim);

            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return Content("Error: Package not found.");

            var booking = new Booking
            {
                UserID = userId,
                PackageID = packageId,
                BookingDate = DateTime.Now,
                TravelDate = DateTime.Now.AddDays(7),
                NumberOfPeople = 1,
                TotalBeforeDiscount = package.Price,
                TotalDiscountAmount = 0,
                FinalAmount = package.Price,
                // === CHANGE 2: Always Pending ===
                BookingStatus = "Pending"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

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

            // Decrease slots for dev bypass since payment is "completed"
            package.AvailableSlots -= 1;

            await _context.SaveChangesAsync();

            return RedirectToAction("Success", new { bookingId = booking.BookingID });
        }

        // ========== HELPER METHODS ==========
        // [file]: Controllers/PaymentController.cs

        private async Task<User> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim)) return null;

            if (int.TryParse(userIdClaim, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);

                // === FIX: If Cookie exists but User is gone (DB Reset), force Logout ===
                if (user == null)
                {
                    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(HttpContext, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                }

                return user;
            }

            return null;
        }

        private void CalculateDiscounts(CustomerInfoViewModel model)
        {
            decimal discount = 0;
            if ((model.TravelDate - DateTime.Now).TotalDays >= 30) discount += model.BasePrice * 0.10m;
            if (model.NumberOfPeople >= 5) discount += model.BasePrice * 0.15m;
            model.DiscountAmount = discount;
            model.FinalAmount = model.BasePrice - discount;
        }

        private string GetPaymentMethodName(string method)
        {
            return method switch
            {
                "card" => "Credit Card",
                "bank_transfer" => "Bank Transfer",
                "tng_ewallet" => "Touch 'n Go",
                "grabpay" => "GrabPay",
                "cash" => "Cash Payment",
                _ => method
            };
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

        // Typed helpers for PaymentController
        public static void SetCustomerInfo(this ISession session, CustomerInfoViewModel vm) => session.SetObject("CustomerInfo", vm);
        public static CustomerInfoViewModel GetCustomerInfo(this ISession session) => session.GetObject<CustomerInfoViewModel>("CustomerInfo");

        public static void SetUserId(this ISession session, int id) => session.SetInt32("UserId", id);
        public static int GetUserId(this ISession session) => session.GetInt32("UserId") ?? 0;

        public static void SetPaymentMethod(this ISession session, string method) => session.SetString("PaymentMethod", method);
        public static string GetPaymentMethod(this ISession session) => session.GetString("PaymentMethod");

        public static void ClearPaymentSession(this ISession session)
        {
            session.Remove("CustomerInfo");
            session.Remove("UserId");
            session.Remove("PaymentMethod");
        }
    }
}