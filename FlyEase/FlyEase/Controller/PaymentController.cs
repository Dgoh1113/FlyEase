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

// Data Transfer Object
public class PriceRequest
{
    public int PackageId { get; set; }
    public int People { get; set; }
    public int Seniors { get; set; }
    public int Juniors { get; set; }
    public string VoucherCode { get; set; }
    public DateTime? TravelDate { get; set; }
    public DateTime? MainPassengerDOB { get; set; }
}

// Helper DTO for internal calculation
public class CalculationResult
{
    public decimal BasePrice { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal FinalAmount { get; set; }
    public List<string> Breakdown { get; set; } = new List<string>();
    public bool IsVoucherValid { get; set; }
    public string VoucherMessage { get; set; }
}

namespace FlyEase.Controllers
{
    public class PaymentController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly StripeService _stripeService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            FlyEaseDbContext context,
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            StripeService stripeService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _stripeService = stripeService;
        }

        // =========================================================
        // STEP 1: CUSTOMER INFO (GET)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> CustomerInfo(int packageId, int people = 1)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) { TempData["Error"] = "Please login first"; return RedirectToAction("Login", "Auth"); }

            if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                TempData["Error"] = "Administrator and Staff accounts are not allowed to make bookings.";
                return RedirectToAction("Index", "Home");
            }

            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return NotFound();

            // --- NEW: CHECK AVAILABILITY IN DB ---
            // We check if "Senior" or "Junior" discounts exist in the DB to decide whether to show the dropdowns
            var activeDiscounts = await _context.DiscountTypes
                .Where(d => d.IsActive)
                .Select(d => d.DiscountName.ToUpper())
                .ToListAsync();

            ViewBag.HasSeniorParams = activeDiscounts.Any(n => n.Contains("SENIOR"));
            ViewBag.HasJuniorParams = activeDiscounts.Any(n => n.Contains("JUNIOR"));
            // -------------------------------------

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
                Address = user.Address,
                NumberOfSeniors = 0,
                NumberOfJuniors = 0
            };

            // Calculate initial price (0 seniors/juniors)
            var calc = await CalculateBookingDetailsAsync(packageId, people, 0, 0, null);
            vm.DiscountAmount = calc.TotalDiscount;
            vm.FinalAmount = calc.FinalAmount;

            // Save Initial Session
            HttpContext.Session.SetString("OriginalPackageId", packageId.ToString());
            HttpContext.Session.SetString("OriginalPeople", people.ToString());
            HttpContext.Session.SetCustomerInfo(vm);
            HttpContext.Session.SetUserId(user.UserID);

            return View(vm);
        }

        // =========================================================
        // STEP 1: CUSTOMER INFO (POST)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> CustomerInfo(CustomerInfoViewModel model)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                TempData["Error"] = "Administrator and Staff accounts are not allowed to make bookings.";
                return RedirectToAction("Index", "Home");
            }

            // --- NEW: STRICT VALIDATION ---
            // Verify DB again to prevent tampering
            var activeDiscounts = await _context.DiscountTypes
                .Where(d => d.IsActive)
                .Select(d => d.DiscountName.ToUpper())
                .ToListAsync();

            bool seniorAllowed = activeDiscounts.Any(n => n.Contains("SENIOR"));
            bool juniorAllowed = activeDiscounts.Any(n => n.Contains("JUNIOR"));

            if (model.NumberOfSeniors > 0 && !seniorAllowed)
            {
                ModelState.AddModelError("NumberOfSeniors", "Senior discount is not currently available.");
            }
            if (model.NumberOfJuniors > 0 && !juniorAllowed)
            {
                ModelState.AddModelError("NumberOfJuniors", "Junior discount is not currently available.");
            }
            // ------------------------------

            if ((model.NumberOfSeniors + model.NumberOfJuniors) > model.NumberOfPeople)
            {
                ModelState.AddModelError("NumberOfPeople", "Total people count must include Seniors and Juniors.");
            }

            if (!ModelState.IsValid)
            {
                // Reload ViewBags if returning with error
                ViewBag.HasSeniorParams = seniorAllowed;
                ViewBag.HasJuniorParams = juniorAllowed;
                return View(model);
            }

            // RECALCULATE EVERYTHING SERVER SIDE
            var calc = await CalculateBookingDetailsAsync(
                model.PackageID,
                model.NumberOfPeople,
                model.NumberOfSeniors,
                model.NumberOfJuniors,
                null // Voucher usually applied via API later
            );

            // Update model with the calculated truths
            model.BasePrice = calc.BasePrice;
            model.DiscountAmount = calc.TotalDiscount;
            model.FinalAmount = calc.FinalAmount;

            // Update Session
            HttpContext.Session.SetCustomerInfo(model);

            return RedirectToAction("PaymentMethod");
        }

        // =========================================================
        // STEP 2: VALIDATE VOUCHER (API)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> ApplyVoucher([FromBody] PriceRequest request)
        {
            if (string.IsNullOrEmpty(request.VoucherCode)) return Json(new { success = false, message = "Code is empty." });

            var discount = await _context.DiscountTypes
                .FirstOrDefaultAsync(d => d.DiscountName == request.VoucherCode);

            if (discount == null) return Json(new { success = false, message = "Invalid Code." });

            var validationResult = ValidateDiscountRules(discount, request);

            if (!validationResult.IsValid)
            {
                return Json(new { success = false, message = validationResult.ErrorMessage });
            }

            return Json(new
            {
                success = true,
                message = "Voucher Applied Successfully!",
                discountAmount = discount.DiscountAmount ?? 0,
                discountRate = discount.DiscountRate ?? 0
            });
        }

        // =========================================================
        // STEP 3: CALCULATE PRICE API (THE BRAIN)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> CalculatePrice([FromBody] PriceRequest request)
        {
            var result = await CalculateBookingDetailsAsync(
                request.PackageId,
                request.People,
                request.Seniors,
                request.Juniors,
                request.VoucherCode,
                request
            );

            if (result.BasePrice == 0) return Json(new { success = false, message = "Package not found" });

            return Json(new
            {
                success = true,
                basePrice = result.BasePrice,
                discountAmount = result.TotalDiscount,
                finalAmount = result.FinalAmount,
                breakdown = result.Breakdown
            });
        }

        // =========================================================
        // CENTRALIZED CALCULATION ENGINE (STACKING + VALIDATION)
        // =========================================================
        private async Task<CalculationResult> CalculateBookingDetailsAsync(
            int packageId, int people, int seniors, int juniors, string voucherCode, PriceRequest fullRequest = null)
        {
            var result = new CalculationResult();

            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return result;

            result.BasePrice = package.Price * people;

            decimal accumulatedDiscount = 0;
            var allDiscounts = await _context.DiscountTypes.Where(d => d.IsActive).ToListAsync();

            // 1. Calculate Standard SENIOR Discount
            // Logic: Only calculate if the DB actually has a "Senior" record
            if (seniors > 0)
            {
                var snrConfig = allDiscounts.FirstOrDefault(x => x.DiscountName.ToUpper().Contains("SENIOR"));

                if (snrConfig != null) // Only proceed if configured in DB
                {
                    decimal snrAmount = 0;
                    if (snrConfig.DiscountRate.HasValue) snrAmount = (package.Price * snrConfig.DiscountRate.Value) * seniors;
                    else if (snrConfig.DiscountAmount.HasValue) snrAmount = snrConfig.DiscountAmount.Value * seniors;

                    accumulatedDiscount += snrAmount; // Stack it
                    result.Breakdown.Add($"Senior x{seniors}: -RM{snrAmount:N2}");
                }
            }

            // 2. Calculate Standard JUNIOR Discount
            if (juniors > 0)
            {
                var jnrConfig = allDiscounts.FirstOrDefault(x => x.DiscountName.ToUpper().Contains("JUNIOR"));

                if (jnrConfig != null) // Only proceed if configured in DB
                {
                    decimal jnrAmount = 0;
                    if (jnrConfig.DiscountRate.HasValue) jnrAmount = (package.Price * jnrConfig.DiscountRate.Value) * juniors;
                    else if (jnrConfig.DiscountAmount.HasValue) jnrAmount = jnrConfig.DiscountAmount.Value * juniors;

                    accumulatedDiscount += jnrAmount; // Stack it
                    result.Breakdown.Add($"Junior x{juniors}: -RM{jnrAmount:N2}");
                }
            }

            // 3. Auto-Apply System/Bulk Discounts
            foreach (var d in allDiscounts.Where(x => string.IsNullOrEmpty(x.AgeCriteria) && (x.MinPax.HasValue || x.MinSpend.HasValue)))
            {
                if (d.DiscountName.Contains("Bulk") && people >= (d.MinPax ?? 3))
                {
                    decimal bulkAmt = 0;
                    if (d.DiscountRate.HasValue) bulkAmt = result.BasePrice * d.DiscountRate.Value;
                    else if (d.DiscountAmount.HasValue) bulkAmt = d.DiscountAmount.Value;

                    if (bulkAmt > 0)
                    {
                        accumulatedDiscount += bulkAmt;
                        result.Breakdown.Add($"{d.DiscountName}: -RM{bulkAmt:N2}");
                    }
                }
            }

            // 4. Apply Voucher Code
            if (!string.IsNullOrEmpty(voucherCode))
            {
                var voucher = allDiscounts.FirstOrDefault(d => d.DiscountName.Equals(voucherCode.Trim(), StringComparison.OrdinalIgnoreCase));
                if (voucher != null)
                {
                    bool isValid = true;
                    if (fullRequest != null) isValid = ValidateDiscountRules(voucher, fullRequest).IsValid;

                    if (isValid)
                    {
                        decimal vAmount = 0;
                        if (voucher.DiscountAmount.HasValue) vAmount = voucher.DiscountAmount.Value;
                        else if (voucher.DiscountRate.HasValue) vAmount = result.BasePrice * voucher.DiscountRate.Value;

                        accumulatedDiscount += vAmount;
                        result.Breakdown.Add($"Voucher ({voucher.DiscountName}): -RM{vAmount:N2}");
                    }
                }
            }

            // Safety: Cap discount at total price (cannot be negative)
            if (accumulatedDiscount > result.BasePrice) accumulatedDiscount = result.BasePrice;

            result.TotalDiscount = accumulatedDiscount;
            result.FinalAmount = result.BasePrice - accumulatedDiscount;

            return result;
        }

        // =========================================================
        // HELPER: RULE VALIDATION
        // =========================================================
        private (bool IsValid, string ErrorMessage) ValidateDiscountRules(DiscountType d, PriceRequest r)
        {
            if (!d.IsActive) return (false, "This voucher is inactive.");
            if (d.StartDate.HasValue && DateTime.Now < d.StartDate.Value) return (false, "Promotion has not started yet.");
            if (d.EndDate.HasValue && DateTime.Now > d.EndDate.Value) return (false, "Promotion has expired.");
            if (d.MinPax.HasValue && r.People < d.MinPax.Value) return (false, $"Minimum {d.MinPax} people required.");

            if (!string.IsNullOrEmpty(d.AgeCriteria) && d.AgeLimit.HasValue && r.MainPassengerDOB.HasValue)
            {
                int age = CalculateAge(r.MainPassengerDOB.Value);
                if (d.AgeCriteria == "Greater" && age < d.AgeLimit.Value) return (false, $"Only valid for passengers older than {d.AgeLimit.Value}.");
                if (d.AgeCriteria == "Less" && age > d.AgeLimit.Value) return (false, $"Only valid for passengers younger than {d.AgeLimit.Value}.");
            }

            if (d.EarlyBirdDays.HasValue && d.EarlyBirdDays.Value > 0 && r.TravelDate.HasValue)
            {
                var daysUntilTrip = (r.TravelDate.Value - DateTime.Now).TotalDays;
                if (daysUntilTrip < d.EarlyBirdDays.Value) return (false, $"Must book at least {d.EarlyBirdDays.Value} days in advance.");
            }

            return (true, "");
        }

        // =========================================================
        // STEP 4: PAYMENT METHOD SELECTION
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

            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo != null)
            {
                var package = await _context.Packages.FindAsync(customerInfo.PackageID);
                if (package != null && (package.StartDate - DateTime.Now).TotalDays <= 14)
                {
                    if (model.PaymentType == "Deposit")
                    {
                        TempData["Error"] = "Deposit option is not available due to late booking.";
                        return RedirectToAction("PaymentMethod");
                    }
                }
            }

            HttpContext.Session.SetPaymentMethod(model.SelectedMethod);
            HttpContext.Session.SetString("PaymentType", model.PaymentType ?? "Full");

            return RedirectToAction("StripeCheckout");
        }

        // =========================================================
        // STEP 5: STRIPE CHECKOUT
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> StripeCheckout()
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null) return RedirectToAction("Index", "Home");

            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            string selectedMethod = HttpContext.Session.GetPaymentMethod();

            decimal amountToPay = (paymentType == "Deposit")
                ? customerInfo.FinalAmount * 0.30m
                : customerInfo.FinalAmount;

            var domain = $"{Request.Scheme}://{Request.Host}";

            var paymentMethodTypes = new List<string> { "card" };
            if (selectedMethod == "Online Banking") paymentMethodTypes.Add("fpx");
            if (selectedMethod == "GrabPay") paymentMethodTypes.Add("grabpay");
            if (selectedMethod == "TouchNGo") { paymentMethodTypes.Add("grabpay"); }

            try
            {
                var session = await _stripeService.CreateCheckoutSessionAsync(
                    amountToPay,
                    "myr",
                    $"{domain}/Payment/StripeCallback?session_id={{CHECKOUT_SESSION_ID}}",
                    $"{domain}/Payment/PaymentMethod",
                    $"BK-{DateTime.Now.Ticks}",
                    paymentMethodTypes
                );

                return Redirect(session.Url);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Stripe Connection Error: {ex.Message}";
                return RedirectToAction("PaymentMethod");
            }
        }

        [HttpGet]
        public async Task<IActionResult> StripeCallback(string session_id)
        {
            var service = new Stripe.Checkout.SessionService();
            var session = await service.GetAsync(session_id);

            if (session.PaymentStatus == "paid")
            {
                string method = HttpContext.Session.GetPaymentMethod() ?? "Stripe";
                return await ProcessBooking(method + " (Online)", session.PaymentIntentId);
            }

            TempData["Error"] = "Payment verification failed or was cancelled.";
            return RedirectToAction("PaymentMethod");
        }

        // =========================================================
        // CORE PROCESSING LOGIC (DB SAVE)
        // =========================================================
        private async Task<IActionResult> ProcessBooking(string paymentMethod, string transactionId)
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

            string bookingStatus = isDeposit ? "Deposit" : "Confirmed";

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

            var payment = new Payment
            {
                BookingID = booking.BookingID,
                PaymentMethod = paymentMethod,
                AmountPaid = paidAmount,
                IsDeposit = isDeposit,
                PaymentDate = DateTime.Now,
                PaymentStatus = "Completed",
                TransactionID = transactionId
            };

            _context.Payments.Add(payment);
            package.AvailableSlots -= customerInfo.NumberOfPeople;

            await _context.SaveChangesAsync();
            HttpContext.Session.ClearPaymentSession();

            return RedirectToAction("Success", new { bookingId = booking.BookingID });
        }

        // =========================================================
        // VIEW ACTIONS & HISTORY
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

            if (booking.BookingStatus == "Confirmed" && booking.Package.EndDate < DateTime.Now)
            {
                booking.BookingStatus = "Completed";
                _context.Bookings.Update(booking);
                await _context.SaveChangesAsync();
            }

            ViewBag.CanCancel = (booking.Package.StartDate - DateTime.Now).TotalDays > 14
                                && booking.BookingStatus != "Cancelled"
                                && booking.BookingStatus != "Completed";

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

            var daysUntilTrip = (booking.Package.StartDate - DateTime.Now).TotalDays;
            if (daysUntilTrip <= 14)
            {
                TempData["Error"] = "Cancellation is not allowed within 14 days of the trip.";
                return RedirectToAction("BookingHistoryDetails", new { id = bookingId });
            }

            try
            {
                foreach (var payment in booking.Payments)
                {
                    if (payment.PaymentStatus == "Completed" || payment.PaymentStatus == "Deposit")
                    {
                        if (!string.IsNullOrEmpty(payment.TransactionID) && payment.TransactionID.StartsWith("pi_"))
                        {
                            await _stripeService.RefundPaymentAsync(payment.TransactionID);
                            payment.PaymentStatus = "Refunded";
                        }
                    }
                }

                booking.BookingStatus = "Cancelled";
                booking.Package.AvailableSlots += booking.NumberOfPeople;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Booking cancelled successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling booking");
                TempData["Error"] = "An error occurred while cancelling.";
            }

            return RedirectToAction("BookingHistoryDetails", new { id = bookingId });
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

        private int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return age;
        }

        [HttpGet]
        public async Task<IActionResult> GetDiscounts()
        {
            var discounts = await _context.DiscountTypes
                .Select(d => new { name = d.DiscountName, rate = d.DiscountRate, amount = d.DiscountAmount })
                .ToListAsync();
            return Json(discounts);
        }
    }
}