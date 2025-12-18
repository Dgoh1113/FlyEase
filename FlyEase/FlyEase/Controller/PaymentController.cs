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
    // This string can now contain multiple codes separated by commas (e.g. "CODE1,CODE2")
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

            // Check Available Discount Types (For UI toggle only)
            var activeDiscounts = await _context.DiscountTypes
                .Where(d => d.IsActive)
                .Select(d => d.DiscountName.ToUpper())
                .ToListAsync();

            ViewBag.HasSeniorParams = activeDiscounts.Any(n => n.Contains("SENIOR"));
            ViewBag.HasJuniorParams = activeDiscounts.Any(n => n.Contains("JUNIOR"));

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
                NumberOfJuniors = 0,
                VoucherCode = null
            };

            // Restore Session
            var sessionInfo = HttpContext.Session.GetCustomerInfo();
            if (sessionInfo != null && sessionInfo.PackageID == packageId)
            {
                vm.NumberOfPeople = sessionInfo.NumberOfPeople;
                vm.NumberOfSeniors = sessionInfo.NumberOfSeniors;
                vm.NumberOfJuniors = sessionInfo.NumberOfJuniors;
                vm.VoucherCode = sessionInfo.VoucherCode;
                vm.TravelDate = sessionInfo.TravelDate;
                vm.SpecialRequests = sessionInfo.SpecialRequests;
                vm.Phone = sessionInfo.Phone;
                vm.Address = sessionInfo.Address;
            }

            // Calculate
            var calc = await CalculateBookingDetailsAsync(
                packageId,
                vm.NumberOfPeople,
                vm.NumberOfSeniors,
                vm.NumberOfJuniors,
                vm.VoucherCode
            );

            vm.DiscountAmount = calc.TotalDiscount;
            vm.FinalAmount = calc.FinalAmount;

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
                return RedirectToAction("Index", "Home");
            }

            var activeDiscounts = await _context.DiscountTypes
                .Where(d => d.IsActive)
                .Select(d => d.DiscountName.ToUpper())
                .ToListAsync();

            bool seniorAllowed = activeDiscounts.Any(n => n.Contains("SENIOR"));
            bool juniorAllowed = activeDiscounts.Any(n => n.Contains("JUNIOR"));

            if (model.NumberOfSeniors > 0 && !seniorAllowed) ModelState.AddModelError("NumberOfSeniors", "Senior discount unavailable.");
            if (model.NumberOfJuniors > 0 && !juniorAllowed) ModelState.AddModelError("NumberOfJuniors", "Junior discount unavailable.");

            if ((model.NumberOfSeniors + model.NumberOfJuniors) > model.NumberOfPeople)
            {
                ModelState.AddModelError("NumberOfPeople", "Total people count must include Seniors and Juniors.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.HasSeniorParams = seniorAllowed;
                ViewBag.HasJuniorParams = juniorAllowed;
                return View(model);
            }

            // Recalculate including Voucher Code
            var calc = await CalculateBookingDetailsAsync(
                model.PackageID,
                model.NumberOfPeople,
                model.NumberOfSeniors,
                model.NumberOfJuniors,
                model.VoucherCode
            );

            model.BasePrice = calc.BasePrice;
            model.DiscountAmount = calc.TotalDiscount;
            model.FinalAmount = calc.FinalAmount;

            HttpContext.Session.SetCustomerInfo(model);

            return RedirectToAction("PaymentMethod");
        }

        // =========================================================
        // API: GET AVAILABLE DISCOUNTS (WITH RULES)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetDiscounts()
        {
            var discounts = await _context.DiscountTypes
                .Where(d => d.IsActive)
                .ToListAsync();

            var filtered = discounts
                .Where(d => !d.DiscountName.ToUpper().Contains("SENIOR")
                         && !d.DiscountName.ToUpper().Contains("JUNIOR"))
                .Select(d => new {
                    name = d.DiscountName,
                    rate = d.DiscountRate,
                    amount = d.DiscountAmount,
                    // Rules for Frontend Validation
                    minPax = d.MinPax,
                    minSpend = d.MinSpend,
                    startDate = d.StartDate,
                    endDate = d.EndDate,
                    earlyBird = d.EarlyBirdDays
                })
                .ToList();

            return Json(filtered);
        }

        // =========================================================
        // API: CALCULATE PRICE (MULTI-VOUCHER STACKING LOGIC)
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

        private async Task<CalculationResult> CalculateBookingDetailsAsync(
            int packageId, int people, int seniors, int juniors, string voucherCode, PriceRequest fullRequest = null)
        {
            var result = new CalculationResult();
            var package = await _context.Packages.FindAsync(packageId);
            if (package == null) return result;

            result.BasePrice = package.Price * people;
            decimal accumulatedDiscount = 0;
            var allDiscounts = await _context.DiscountTypes.Where(d => d.IsActive).ToListAsync();

            // 1. Senior (Automatic)
            if (seniors > 0)
            {
                var snrConfig = allDiscounts.FirstOrDefault(x => x.DiscountName.ToUpper().Contains("SENIOR"));
                if (snrConfig != null)
                {
                    decimal snrAmount = 0;
                    if (snrConfig.DiscountRate.HasValue) snrAmount = (package.Price * snrConfig.DiscountRate.Value) * seniors;
                    else if (snrConfig.DiscountAmount.HasValue) snrAmount = snrConfig.DiscountAmount.Value * seniors;

                    accumulatedDiscount += snrAmount;
                    result.Breakdown.Add($"Senior (x{seniors}): -RM{snrAmount:N2}");
                }
            }

            // 2. Junior (Automatic)
            if (juniors > 0)
            {
                var jnrConfig = allDiscounts.FirstOrDefault(x => x.DiscountName.ToUpper().Contains("JUNIOR"));
                if (jnrConfig != null)
                {
                    decimal jnrAmount = 0;
                    if (jnrConfig.DiscountRate.HasValue) jnrAmount = (package.Price * jnrConfig.DiscountRate.Value) * juniors;
                    else if (jnrConfig.DiscountAmount.HasValue) jnrAmount = jnrConfig.DiscountAmount.Value * juniors;

                    accumulatedDiscount += jnrAmount;
                    result.Breakdown.Add($"Junior (x{juniors}): -RM{jnrAmount:N2}");
                }
            }

            // 3. Bulk/System (Automatic based on Pax)
            foreach (var d in allDiscounts.Where(x =>
                string.IsNullOrEmpty(x.AgeCriteria) &&
                (x.MinPax.HasValue || x.MinSpend.HasValue) &&
                !x.DiscountName.ToUpper().Contains("SENIOR") &&
                !x.DiscountName.ToUpper().Contains("JUNIOR")))
            {
                // Simple Bulk logic: If name contains "Bulk" and conditions met
                if (d.DiscountName.Contains("Bulk") && people >= (d.MinPax ?? 3))
                {
                    decimal bulkAmt = 0;
                    if (d.DiscountRate.HasValue) bulkAmt = result.BasePrice * d.DiscountRate.Value;
                    else if (d.DiscountAmount.HasValue) bulkAmt = d.DiscountAmount.Value;

                    accumulatedDiscount += bulkAmt;
                    result.Breakdown.Add($"{d.DiscountName}: -RM{bulkAmt:N2}");
                }
            }

            // 4. Voucher Codes (Manual & Stackable)
            if (!string.IsNullOrEmpty(voucherCode))
            {
                // Split by comma to handle multiple codes: "CODE1,CODE2"
                var codes = voucherCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var code in codes)
                {
                    var voucher = allDiscounts.FirstOrDefault(d =>
                        d.DiscountName.Equals(code, StringComparison.OrdinalIgnoreCase) &&
                        !d.DiscountName.ToUpper().Contains("SENIOR") &&
                        !d.DiscountName.ToUpper().Contains("JUNIOR"));

                    if (voucher != null)
                    {
                        bool isValid = true;
                        if (fullRequest != null) isValid = ValidateDiscountRules(voucher, fullRequest).IsValid;

                        // Ensure we don't apply the same voucher logic twice if it was already caught by "Bulk" auto-logic above
                        // (Check if this discount name is already in the breakdown)
                        bool alreadyApplied = result.Breakdown.Any(b => b.Contains(voucher.DiscountName));

                        if (isValid && !alreadyApplied)
                        {
                            decimal vAmount = 0;
                            if (voucher.DiscountAmount.HasValue) vAmount = voucher.DiscountAmount.Value;
                            else if (voucher.DiscountRate.HasValue) vAmount = result.BasePrice * voucher.DiscountRate.Value;

                            accumulatedDiscount += vAmount;
                            result.Breakdown.Add($"Voucher ({voucher.DiscountName}): -RM{vAmount:N2}");
                        }
                    }
                }
            }

            if (accumulatedDiscount > result.BasePrice) accumulatedDiscount = result.BasePrice;
            result.TotalDiscount = accumulatedDiscount;
            result.FinalAmount = result.BasePrice - accumulatedDiscount;

            return result;
        }

        private (bool IsValid, string ErrorMessage) ValidateDiscountRules(DiscountType d, PriceRequest r)
        {
            if (!d.IsActive) return (false, "Inactive");
            if (d.StartDate.HasValue && DateTime.Now < d.StartDate.Value) return (false, "Not started");
            if (d.EndDate.HasValue && DateTime.Now > d.EndDate.Value) return (false, "Expired");
            if (d.MinPax.HasValue && r.People < d.MinPax.Value) return (false, $"Min {d.MinPax} people");

            if (d.EarlyBirdDays.HasValue && d.EarlyBirdDays.Value > 0 && r.TravelDate.HasValue)
            {
                var daysUntilTrip = (r.TravelDate.Value - DateTime.Now).TotalDays;
                if (daysUntilTrip < d.EarlyBirdDays.Value) return (false, $"Book {d.EarlyBirdDays.Value} days ahead");
            }
            return (true, "");
        }

        // ... Standard Payment Methods ...

        private async Task<User> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId)) return await _context.Users.FindAsync(userId);
            return null;
        }

        // Step 4: Payment Method Selection
        [HttpGet]
        public async Task<IActionResult> PaymentMethod()
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null)
            {
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
            HttpContext.Session.SetPaymentMethod(model.SelectedMethod);
            HttpContext.Session.SetString("PaymentType", model.PaymentType ?? "Full");

            return RedirectToAction("StripeCheckout");
        }

        // Step 5: Stripe Checkout
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

        // Core Processing Logic (DB Save)
        private async Task<IActionResult> ProcessBooking(string paymentMethod, string transactionId)
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            var userId = HttpContext.Session.GetUserId();
            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            bool isDeposit = (paymentType == "Deposit");

            if (customerInfo == null || userId == 0) return RedirectToAction("Index", "Home");

            var package = await _context.Packages.FindAsync(customerInfo.PackageID);

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
                BookingStatus = isDeposit ? "Deposit" : "Confirmed"
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

        [HttpGet]
        public async Task<IActionResult> Success(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Package).Include(b => b.User).FirstOrDefaultAsync(b => b.BookingID == bookingId);
            if (booking == null) return NotFound();
            return View(booking);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyVoucher([FromBody] PriceRequest request)
        {
            if (string.IsNullOrEmpty(request.VoucherCode)) return Json(new { success = false, message = "Code is empty." });

            // Simple validator for single code usage
            var discount = await _context.DiscountTypes.FirstOrDefaultAsync(d => d.DiscountName == request.VoucherCode);

            if (discount == null) return Json(new { success = false, message = "Invalid Code." });

            if (discount.DiscountName.ToUpper().Contains("SENIOR") || discount.DiscountName.ToUpper().Contains("JUNIOR"))
            {
                return Json(new { success = false, message = "This discount is applied automatically based on passenger details." });
            }

            var validationResult = ValidateDiscountRules(discount, request);
            if (!validationResult.IsValid) return Json(new { success = false, message = validationResult.ErrorMessage });

            return Json(new
            {
                success = true,
                message = "Voucher Applied Successfully!",
                discountAmount = discount.DiscountAmount ?? 0,
                discountRate = discount.DiscountRate ?? 0
            });
        }
    }
}