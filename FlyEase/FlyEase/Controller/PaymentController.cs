using FlyEase.Data;
using FlyEase.Services;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FlyEase.Controllers
{
    public class PaymentController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly StripeService _stripeService;
        private readonly ILogger<PaymentController> _logger; // Added Logger

        public PaymentController(FlyEaseDbContext context, IConfiguration configuration, ILogger<PaymentController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            // Initialize StripeService manually to ensure it works
            _stripeService = new StripeService(configuration);
        }

        // =========================================================
        // STEP 1: CUSTOMER INFO
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> CustomerInfo(int packageId, int people = 1)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) { TempData["Error"] = "Please login first"; return RedirectToAction("Login", "Auth"); }

            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return NotFound();

            string displayPhone = user.Phone;
            if (!string.IsNullOrEmpty(displayPhone) && displayPhone.StartsWith("+60")) displayPhone = displayPhone.Substring(3);

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
                Phone = displayPhone,
                Address = user.Address
            };

            CalculateDiscounts(vm);

            HttpContext.Session.SetString("OriginalPackageId", packageId.ToString());
            HttpContext.Session.SetString("OriginalPeople", people.ToString());
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

        // =========================================================
        // STEP 2: PAYMENT METHOD
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> PaymentMethod()
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();

            if (customerInfo == null)
            {
                TempData["Error"] = "Please complete customer info";
                return RedirectToAction("CustomerInfo", new { packageId = 1 });
            }

            // POLICY CHECK: DEPOSIT RESTRICTION
            var package = await _context.Packages.FindAsync(customerInfo.PackageID);
            bool allowDeposit = true;
            string depositMessage = "";

            if (package != null)
            {
                if ((package.StartDate - DateTime.Now).TotalDays <= 14)
                {
                    allowDeposit = false;
                    depositMessage = "Deposits are not available for trips starting within 14 days.";
                }
            }

            ViewBag.AllowDeposit = allowDeposit;
            ViewBag.DepositMessage = depositMessage;

            decimal depositAmount = customerInfo.FinalAmount * 0.30m;

            var model = new PaymentMethodViewModel
            {
                PackageName = customerInfo.PackageName,
                FinalAmount = customerInfo.FinalAmount,
                DepositAmount = depositAmount,
                SelectedMethod = "Credit Card",
                PaymentType = "Full"
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> PaymentMethod(PaymentMethodViewModel model)
        {
            if (string.IsNullOrEmpty(model.SelectedMethod))
            {
                TempData["Error"] = "Please select a payment method";
                return RedirectToAction("PaymentMethod");
            }

            // SERVER-SIDE POLICY VALIDATION
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo != null)
            {
                var package = await _context.Packages.FindAsync(customerInfo.PackageID);
                if (package != null && (package.StartDate - DateTime.Now).TotalDays <= 14)
                {
                    if (model.PaymentType == "Deposit")
                    {
                        TempData["Error"] = "Deposit option is not available due to late booking (within 14 days). Full payment required.";
                        return RedirectToAction("PaymentMethod");
                    }
                }
            }

            HttpContext.Session.SetPaymentMethod(model.SelectedMethod);
            HttpContext.Session.SetString("PaymentType", model.PaymentType ?? "Full");

            return model.SelectedMethod switch
            {
                "Credit Card" => RedirectToAction("CardPayment"),
                "TouchNGo" => RedirectToAction("TNGPayment"),
                "Cash" => RedirectToAction("ManualPayment", new { method = "Cash" }),
                _ => RedirectToAction("ManualPayment", new { method = model.SelectedMethod })
            };
        }

        // =========================================================
        // STEP 3A: CARD PAYMENT (STRIPE)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> CardPayment()
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null) return RedirectToAction("Index", "Home");

            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            decimal amountToPay = (paymentType == "Deposit")
                ? customerInfo.FinalAmount * 0.30m
                : customerInfo.FinalAmount;

            var domain = $"{Request.Scheme}://{Request.Host}";

            try
            {
                var session = await _stripeService.CreateCheckoutSessionAsync(
                    amountToPay,
                    "myr",
                    $"{domain}/Payment/StripeCallback?session_id={{CHECKOUT_SESSION_ID}}",
                    $"{domain}/Payment/PaymentMethod",
                    $"BK-{DateTime.Now.Ticks}"
                );

                return Redirect(session.Url);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Stripe Connection Error: {ex.Message}";
                return RedirectToAction("PaymentMethod");
            }
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

            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            decimal amountToPay = (paymentType == "Deposit")
                ? customerInfo.FinalAmount * 0.30m
                : customerInfo.FinalAmount;

            try
            {
                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(amountToPay * 100),
                    Currency = "myr",
                    PaymentMethodTypes = new List<string> { "card" },
                    Description = $"FlyEase Booking: {customerInfo.PackageName} ({paymentType})",
                    Metadata = new Dictionary<string, string>
                    {
                        { "customer_name", customerInfo.FullName },
                        { "customer_email", customerInfo.Email },
                        { "package_id", customerInfo.PackageID.ToString() },
                        { "payment_type", paymentType }
                    }
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                return await ProcessBooking("Credit Card", paymentIntent.Id);
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

        [HttpGet]
        public async Task<IActionResult> StripeCallback(string session_id)
        {
            var service = new Stripe.Checkout.SessionService();
            var session = await service.GetAsync(session_id);

            if (session.PaymentStatus == "paid")
            {
                var customerInfo = HttpContext.Session.GetCustomerInfo();
                if (customerInfo != null)
                {
                    return await ProcessBooking("Stripe Payment", session.PaymentIntentId);
                }
            }

            TempData["Error"] = "Payment verification failed or was cancelled.";
            return RedirectToAction("PaymentMethod");
        }

        // =========================================================
        // STEP 3B: MANUAL PAYMENT (CASH & OTHERS)
        // =========================================================
        [HttpGet]
        public IActionResult ManualPayment(string method)
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null)
            {
                TempData["Error"] = "Session expired";
                return RedirectToAction("Index", "Home");
            }

            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            decimal amountToPay = (paymentType == "Deposit")
                ? customerInfo.FinalAmount * 0.30m
                : customerInfo.FinalAmount;

            ViewBag.PaymentMethod = method;
            ViewBag.FinalAmount = amountToPay;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ManualPaymentPost(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                TempData["Error"] = "Please enter reference number";
                var storedMethod = HttpContext.Session.GetPaymentMethod() ?? "Cash";
                return RedirectToAction("ManualPayment", new { method = storedMethod });
            }

            var method = HttpContext.Session.GetPaymentMethod();

            if (method == "TouchNGo")
            {
                return RedirectToAction("TNGPayment");
            }
            else
            {
                return await ProcessBooking(method, reference);
            }
        }

        // =========================================================
        // TOUCH 'N GO PAYMENT
        // =========================================================
        [HttpGet]
        public IActionResult TNGPayment()
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null)
            {
                TempData["Error"] = "Session expired";
                return RedirectToAction("Index", "Home");
            }

            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            decimal amountToPay = (paymentType == "Deposit")
                ? customerInfo.FinalAmount * 0.30m
                : customerInfo.FinalAmount;

            ViewBag.FinalAmount = amountToPay;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessTNGPayment(string transactionId, string phoneNumber,
            DateTime paymentTime, string paymentMethod, string senderTngId = "")
        {
            if (!IsValidTNG(transactionId))
            {
                TempData["Error"] = "Invalid TNG transaction ID. Format: TNG-ABC123XYZ";
                return RedirectToAction("TNGPayment");
            }

            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(paymentMethod))
            {
                TempData["Error"] = "Please fill all required fields";
                return RedirectToAction("TNGPayment");
            }

            return await ProcessVerifiedManualPayment("TouchNGo", transactionId);
        }

        // =========================================================
        // NEW: BOOKING HISTORY & CANCELLATION
        // =========================================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BookingHistoryDetails(int id)
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (userEmail == null) return RedirectToAction("Login", "Auth");

            var booking = await _context.Bookings
                .Include(b => b.Package)
                .Include(b => b.Payments)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingID == id && b.User.Email == userEmail);

            if (booking == null) return NotFound();

            // Cancellation Rule: Must be > 14 days before start
            ViewBag.CanCancel = (booking.Package.StartDate - DateTime.Now).TotalDays > 14
                                && booking.BookingStatus != "Cancelled";

            // Calculate Balance
            decimal totalPaid = booking.Payments
                .Where(p => p.PaymentStatus == "Completed" || p.PaymentStatus == "Deposit")
                .Sum(p => p.AmountPaid);

            ViewBag.BalanceDue = booking.FinalAmount - totalPaid;

            return View(booking);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return RedirectToAction("Login", "Auth");

            var booking = await _context.Bookings
                .Include(b => b.Package)
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.UserID == user.UserID);

            if (booking == null) return NotFound();

            // 1. POLICY CHECK (14 Days)
            var daysUntilTrip = (booking.Package.StartDate - DateTime.Now).TotalDays;
            if (daysUntilTrip <= 14)
            {
                TempData["Error"] = "Cancellation is not allowed within 14 days of the trip.";
                return RedirectToAction("BookingHistoryDetails", new { id = bookingId });
            }

            try
            {
                // 2. PROCESS REFUNDS
                bool refundInitiated = false;
                foreach (var payment in booking.Payments)
                {
                    if (payment.PaymentStatus == "Completed" || payment.PaymentStatus == "Deposit")
                    {
                        if (!string.IsNullOrEmpty(payment.TransactionID) && payment.TransactionID.StartsWith("pi_"))
                        {
                            try
                            {
                                await _stripeService.RefundPaymentAsync(payment.TransactionID);
                                payment.PaymentStatus = "Refunded";
                                refundInitiated = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Stripe refund failed: {ex.Message}");
                                payment.PaymentStatus = "Refund Failed (Manual Check)";
                            }
                        }
                        else
                        {
                            // Manual payments
                            payment.PaymentStatus = "Refund Pending (Manual)";
                        }
                    }
                }

                // 3. CANCEL BOOKING & RETURN SLOTS
                booking.BookingStatus = "Cancelled";
                booking.Package.AvailableSlots += booking.NumberOfPeople;

                await _context.SaveChangesAsync();

                string msg = "Booking cancelled successfully.";
                if (refundInitiated) msg += " Refund processed via Stripe.";

                TempData["Success"] = msg;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling booking");
                TempData["Error"] = "An error occurred while cancelling.";
            }

            return RedirectToAction("BookingHistoryDetails", new { id = bookingId });
        }

        // =========================================================
        // CORE PROCESSING LOGIC
        // =========================================================

        private async Task<IActionResult> ProcessVerifiedManualPayment(string paymentMethod, string transactionId)
        {
            return await ProcessBookingCore(paymentMethod, transactionId, isVerified: true);
        }

        private async Task<IActionResult> ProcessBooking(string paymentMethod, string transactionId)
        {
            return await ProcessBookingCore(paymentMethod, transactionId, isVerified: false);
        }

        private async Task<IActionResult> ProcessBookingCore(string paymentMethod, string transactionId, bool isVerified)
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            var userId = HttpContext.Session.GetUserId();
            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            bool isDeposit = (paymentType == "Deposit");

            if (customerInfo == null || userId == 0)
            {
                TempData["Error"] = "Session expired";
                return RedirectToAction("Index", "Home");
            }

            var package = await _context.Packages.FindAsync(customerInfo.PackageID);

            if (package == null || package.AvailableSlots < customerInfo.NumberOfPeople)
            {
                TempData["Error"] = "Package not available (Sold Out)";
                return RedirectToAction("Index", "Home");
            }

            string bookingStatus = "Pending";
            bool isPaymentSuccessful = isVerified || paymentMethod == "Credit Card" || paymentMethod == "Stripe Payment" || paymentMethod == "DevBypass";

            if (isPaymentSuccessful)
            {
                bookingStatus = isDeposit ? "Deposit" : "Completed";
            }
            else
            {
                bookingStatus = "Pending";
            }

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
                BookingStatus = bookingStatus
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            decimal paidAmount = isDeposit ? (customerInfo.FinalAmount * 0.30m) : customerInfo.FinalAmount;

            string paymentMethodText = GetPaymentMethodName(paymentMethod);
            if (isVerified) paymentMethodText += $" (Verified: {transactionId})";
            else if (paymentMethod == "Credit Card" || paymentMethod == "Stripe Payment")
                paymentMethodText += transactionId.StartsWith("pi_") ? " (Stripe)" : "";

            var payment = new Payment
            {
                BookingID = booking.BookingID,
                PaymentMethod = paymentMethodText,
                AmountPaid = paidAmount,
                IsDeposit = isDeposit,
                PaymentDate = DateTime.Now,
                PaymentStatus = (bookingStatus == "Pending") ? "Pending" : "Completed",
                TransactionID = transactionId
            };

            _context.Payments.Add(payment);
            package.AvailableSlots -= customerInfo.NumberOfPeople;

            await _context.SaveChangesAsync();
            HttpContext.Session.ClearPaymentSession();

            return RedirectToAction("Success", new { bookingId = booking.BookingID });
        }

        // =========================================================
        // VIEW ACTIONS
        // =========================================================
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

        [HttpGet]
        public async Task<IActionResult> Receipt(int bookingId, bool print = false)
        {
            var booking = await _context.Bookings
                .Include(b => b.Package)
                .Include(b => b.User)
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null) return RedirectToAction("Index", "Home");

            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    if (booking.UserID != userId && !User.IsInRole("Admin") && !User.IsInRole("Staff"))
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }
            }

            ViewBag.Print = print;
            return View(booking);
        }

        [HttpGet("Payment/DevQuickBook")]
        public async Task<IActionResult> DevQuickBook(int packageId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Content("Error: Must be logged in");
            int userId = int.Parse(userIdClaim);

            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return Content("Error: Package not found");

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
                BookingStatus = "Completed"
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
                PaymentStatus = "Completed",
                TransactionID = "DEV_BYPASS"
            };

            _context.Payments.Add(payment);
            package.AvailableSlots -= 1;
            await _context.SaveChangesAsync();

            return RedirectToAction("Success", new { bookingId = booking.BookingID });
        }

        // =========================================================
        // HELPER METHODS
        // =========================================================

        private async Task<User> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return null;
            if (int.TryParse(userIdClaim, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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
                "Credit Card" => "Credit Card",
                "Stripe Payment" => "Credit Card",
                "TouchNGo" => "Touch 'n Go",
                "Cash" => "Cash Payment",
                _ => method
            };
        }

        private bool IsValidTNG(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return false;
            var pattern = @"^(TNG-|TNG)\w{9,12}$";
            return System.Text.RegularExpressions.Regex.IsMatch(reference, pattern);
        }
    }
}