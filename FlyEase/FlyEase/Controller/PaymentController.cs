using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Security.Claims;
using Microsoft.AspNetCore.Http; // For ISession

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


            // SET REAL STRIPE TEST KEY
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

            // USE EXTENSION METHODS HERE:
            HttpContext.Session.SetCustomerInfo(vm);
            HttpContext.Session.SetUserId(user.UserID);

            return View(vm);
        }

        [HttpPost]
        public IActionResult CustomerInfo(CustomerInfoViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            CalculateDiscounts(model);

            // USE EXTENSION METHODS HERE:
            HttpContext.Session.SetCustomerInfo(model);
            return RedirectToAction("PaymentMethod");
        }

        // ========== STEP 2: PAYMENT METHOD ==========
        [HttpGet]
        public IActionResult PaymentMethod()
        {
            // USE EXTENSION METHODS HERE:
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

            // USE EXTENSION METHODS HERE:
            HttpContext.Session.SetPaymentMethod(paymentMethod);

            return paymentMethod == "card"
                ? RedirectToAction("CardPayment")
                : RedirectToAction("ManualPayment", new { method = paymentMethod });
        }

        // ========== STEP 3A: CARD PAYMENT ==========
        [HttpGet]
        public IActionResult CardPayment()
        {
            // USE EXTENSION METHODS HERE:
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
            // USE EXTENSION METHODS HERE:
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null)
            {
                TempData["Error"] = "Session expired";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // 1. CREATE REAL STRIPE PAYMENT INTENT
                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(customerInfo.FinalAmount * 100), // Convert RM to cents
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

                // 2. GET REAL STRIPE INTENT ID
                string realStripeId = paymentIntent.Id; // REAL: pi_xxxxxxxxxxxxx

                // 3. PROCESS PAYMENT WITH REAL STRIPE ID
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
            // USE EXTENSION METHODS HERE:
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

            // USE EXTENSION METHODS HERE:
            var method = HttpContext.Session.GetPaymentMethod();
            return await ProcessBooking(method, reference);
        }

        // ========== PROCESS BOOKING ==========
        private async Task<IActionResult> ProcessBooking(string paymentMethod, string transactionId)
        {
            // USE EXTENSION METHODS HERE:
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
                BookingStatus = paymentMethod == "card" ? "Confirmed" : "Pending Payment"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // Create payment with REAL Stripe ID
            string paymentMethodText = GetPaymentMethodName(paymentMethod);

            // Store REAL Stripe ID if it's a card payment
            if (paymentMethod == "card")
            {
                // Check if this looks like a real Stripe ID
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

            // Only reduce slots for confirmed payments
            if (paymentMethod == "card")
            {
                package.AvailableSlots -= customerInfo.NumberOfPeople;
            }

            await _context.SaveChangesAsync();

            // USE EXTENSION METHODS HERE: Clear session
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

        // ========== HELPER METHODS ==========
        private async Task<User> GetCurrentUserAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return null;
            return await _context.Users.FindAsync(int.Parse(userId));
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
}