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
        // [UPDATE THIS METHOD]
        // Step 4: Payment Method Selection (Modified to handle Balance Payment)
        [HttpGet]
        public async Task<IActionResult> PaymentMethod()
        {
            // CHECK 1: Is this a Balance Payment?
            string paymentType = HttpContext.Session.GetString("PaymentType");
            int? balanceBookingId = HttpContext.Session.GetInt32("BalancePayment_BookingID");

            if (paymentType == "Balance" && balanceBookingId.HasValue)
            {
                var booking = await _context.Bookings.Include(b => b.Package).FirstOrDefaultAsync(b => b.BookingID == balanceBookingId.Value);
                decimal amount = decimal.Parse(HttpContext.Session.GetString("BalancePayment_Amount") ?? "0");

                var balanceModel = new PaymentMethodViewModel
                {
                    PackageName = booking.Package.PackageName,
                    FinalAmount = amount,
                    DepositAmount = 0, // No deposit option for balance payments
                    SelectedMethod = "Credit Card",
                    PaymentType = "Balance"
                };

                ViewBag.AllowDeposit = false;
                ViewBag.DepositMessage = "Paying remaining balance.";
                return View(balanceModel);
            }

            // CHECK 2: Standard New Booking Flow (Existing Code)
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

        // [UPDATE THIS METHOD]
        // Step 5: Stripe Checkout (Modified to calculate amount based on PaymentType)
        [HttpGet]
        public async Task<IActionResult> StripeCheckout()
        {
            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            string selectedMethod = HttpContext.Session.GetPaymentMethod(); // Retrieved from Session (set in PaymentMethod POST)

            decimal amountToPay = 0;
            string referenceId = "";

            // LOGIC: Determine Amount based on Type
            if (paymentType == "Balance")
            {
                // Handle Balance
                string balanceStr = HttpContext.Session.GetString("BalancePayment_Amount");
                if (decimal.TryParse(balanceStr, out decimal bal)) amountToPay = bal;

                int bookingId = HttpContext.Session.GetInt32("BalancePayment_BookingID") ?? 0;
                referenceId = $"BAL-BK{bookingId}-{DateTime.Now.Ticks}";
            }
            else
            {
                // Handle New Booking (Deposit or Full)
                var customerInfo = HttpContext.Session.GetCustomerInfo();
                if (customerInfo == null) return RedirectToAction("Index", "Home");

                amountToPay = (paymentType == "Deposit")
                    ? customerInfo.FinalAmount * 0.30m
                    : customerInfo.FinalAmount;

                referenceId = $"BK-{DateTime.Now.Ticks}";
            }

            var domain = $"{Request.Scheme}://{Request.Host}";

            var paymentMethodTypes = new List<string> { "card" };
            if (selectedMethod == "Online Banking") paymentMethodTypes.Add("fpx");
            if (selectedMethod == "GrabPay") paymentMethodTypes.Add("grabpay");
            if (selectedMethod == "TouchNGo") { paymentMethodTypes.Add("grabpay"); } // TNG via GrabPay usually, or adjust accordingly

            try
            {
                var session = await _stripeService.CreateCheckoutSessionAsync(
                    amountToPay,
                    "myr",
                    $"{domain}/Payment/StripeCallback?session_id={{CHECKOUT_SESSION_ID}}",
                    $"{domain}/Payment/PaymentMethod",
                    referenceId,
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

        // [UPDATE THIS METHOD]
        // Stripe Callback (Modified to route to correct processor)
        [HttpGet]
        public async Task<IActionResult> StripeCallback(string session_id)
        {
            var service = new Stripe.Checkout.SessionService();
            var session = await service.GetAsync(session_id);

            if (session.PaymentStatus == "paid")
            {
                string method = HttpContext.Session.GetPaymentMethod() ?? "Stripe";
                string paymentType = HttpContext.Session.GetString("PaymentType");

                if (paymentType == "Balance")
                {
                    return await ProcessBalancePayment(method + " (Online)", session.PaymentIntentId);
                }
                else
                {
                    return await ProcessBooking(method + " (Online)", session.PaymentIntentId);
                }
            }

            TempData["Error"] = "Payment verification failed or was cancelled.";
            return RedirectToAction("PaymentMethod");
        }

        // [ADD THIS NEW PRIVATE METHOD]
        // Handles saving the balance payment to the DB
        private async Task<IActionResult> ProcessBalancePayment(string paymentMethod, string transactionId)
        {
            int bookingId = HttpContext.Session.GetInt32("BalancePayment_BookingID") ?? 0;
            string amountStr = HttpContext.Session.GetString("BalancePayment_Amount");
            decimal amount = decimal.Parse(amountStr ?? "0");

            var booking = await _context.Bookings.FindAsync(bookingId);

            if (booking != null)
            {
                // 1. Add Payment Record
                var payment = new Payment
                {
                    BookingID = bookingId,
                    PaymentMethod = paymentMethod,
                    AmountPaid = amount,
                    IsDeposit = false,
                    PaymentDate = DateTime.Now,
                    PaymentStatus = "Completed",
                    TransactionID = transactionId
                };
                _context.Payments.Add(payment);

                // 2. Update Booking Status -> Confirmed (Fully Paid)
                booking.BookingStatus = "Confirmed";

                await _context.SaveChangesAsync();
            }

            // Cleanup Session
            HttpContext.Session.Remove("BalancePayment_BookingID");
            HttpContext.Session.Remove("BalancePayment_Amount");
            HttpContext.Session.Remove("PaymentType");
            HttpContext.Session.ClearPaymentSession(); // Clears method selection

            return RedirectToAction("Success", new { bookingId = bookingId });
        }
        private async Task<IActionResult> ProcessBooking(string paymentMethod, string transactionId)
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            var userId = HttpContext.Session.GetUserId();
            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";

            // --- 1. DETERMINE STATUSES ---

            bool isDeposit = (paymentType == "Deposit");
            bool isCash = !string.IsNullOrEmpty(paymentMethod) &&
                          paymentMethod.Contains("Cash", StringComparison.OrdinalIgnoreCase);

            string finalPaymentStatus;
            string finalBookingStatus;

            // UPDATED LOGIC ORDER:
            // 1. Check Deposit First ("deposit status will be deposit for all")
            if (isDeposit)
            {
                // Applies to both Online Deposit and Cash Deposit
                finalPaymentStatus = "Deposit";
                finalBookingStatus = "Deposit";
            }
            // 2. Then Check Cash ("for cash it will pending")
            // This now applies only to Full Cash payments, as Deposit was handled above
            else if (isCash)
            {
                finalPaymentStatus = "Pending";
                finalBookingStatus = "Pending";
            }
            // 3. Default to Online Full Payment ("booking status confirmed")
            else
            {
                // Online Full Payment (Card, FPX, GrabPay, TnG)
                finalPaymentStatus = "Completed";
                finalBookingStatus = "Confirmed";
            }

            // --- VALIDATION & SETUP ---

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

            // --- 2. SAVE BOOKING ---

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
                BookingStatus = finalBookingStatus // Set based on logic above
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // --- 3. SAVE DISCOUNTS ---

            var appliedDiscounts = HttpContext.Session.GetObject<List<DiscountInfo>>("AppliedDiscounts")
                                   ?? new List<DiscountInfo>();

            foreach (var discount in appliedDiscounts)
            {
                var bookingDiscount = new BookingDiscount
                {
                    BookingID = booking.BookingID,
                    DiscountTypeID = discount.DiscountId ?? 0,
                    AppliedAmount = discount.Amount
                };

                // Handle dynamically created system discounts (Bulk/Senior/Junior)
                if (discount.DiscountId == null || discount.DiscountId == 0)
                {
                    var discountType = await _context.DiscountTypes
                        .FirstOrDefaultAsync(d => d.DiscountName == discount.Name);

                    if (discountType == null)
                    {
                        discountType = new DiscountType
                        {
                            DiscountName = discount.Name,
                            DiscountRate = discount.Rate,
                            IsActive = true
                        };
                        _context.DiscountTypes.Add(discountType);
                        await _context.SaveChangesAsync();
                    }
                    bookingDiscount.DiscountTypeID = discountType.DiscountTypeID;
                }
                _context.BookingDiscounts.Add(bookingDiscount);
            }

            // --- 4. SAVE PAYMENT ---

            decimal paidAmount = isDeposit ? (customerInfo.FinalAmount * 0.30m) : customerInfo.FinalAmount;

            var payment = new Payment
            {
                BookingID = booking.BookingID,
                PaymentMethod = paymentMethod,
                AmountPaid = paidAmount,
                IsDeposit = isDeposit,
                PaymentDate = DateTime.Now,
                PaymentStatus = finalPaymentStatus, // Set based on logic above
                TransactionID = transactionId
            };

            _context.Payments.Add(payment);

            // Deduct slots
            package.AvailableSlots -= customerInfo.NumberOfPeople;

            await _context.SaveChangesAsync();

            // Cleanup Session
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
            // 1. Identify the User
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            // distinct query to ensure we get the user ID even if they logged in via cookie claim
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToAction("Login", "Auth");

            // 2. Find the Booking (Ensure it belongs to this user)
            var booking = await _context.Bookings
                .Include(b => b.Package)
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.UserID == user.UserID);

            if (booking == null) return NotFound();

            // 3. Validate 14-Day Cancellation Policy
            var daysUntilTrip = (booking.Package.StartDate - DateTime.Now).TotalDays;
            if (daysUntilTrip <= 14)
            {
                TempData["Error"] = "Cancellation is not allowed within 14 days of the trip.";
                return RedirectToAction("BookingHistoryDetails", new { id = bookingId });
            }

            // 4. Process Refunds & Cancellation
            try
            {
                bool refundAttempted = false;

                foreach (var payment in booking.Payments)
                {
                    // Only refund Completed or Deposit payments
                    if (payment.PaymentStatus == "Completed" || payment.PaymentStatus == "Deposit")
                    {
                        // Check if it's a valid Stripe Transaction (Starts with 'pi_')
                        if (!string.IsNullOrEmpty(payment.TransactionID) && payment.TransactionID.StartsWith("pi_"))
                        {
                            // Call the Service to process refund via Stripe API
                            await _stripeService.RefundPaymentAsync(payment.TransactionID);

                            payment.PaymentStatus = "Refunded";
                            refundAttempted = true;
                        }
                    }
                }

                // 5. Update Database
                booking.BookingStatus = "Cancelled";

                // Return the slots to the package
                booking.Package.AvailableSlots += booking.NumberOfPeople;

                await _context.SaveChangesAsync();

                if (refundAttempted)
                {
                    TempData["Success"] = "Booking cancelled and refund processed successfully.";
                }
                else
                {
                    TempData["Success"] = "Booking cancelled successfully.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling booking ID {BookingId}", bookingId);
                TempData["Error"] = "Booking cancelled locally, but there was an issue processing the refund with the bank. Please contact support.";

                // Still mark as cancelled in DB so the user doesn't try again, 
                // but the payment status might need manual admin intervention
                booking.BookingStatus = "Cancelled";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("BookingHistoryDetails", new { id = bookingId });
        }
        // Initiates the balance payment flow by storing details in session
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> PayBalance(int bookingId)
        {
            var user = await GetCurrentUserAsync();
            // Verify booking belongs to user
            var booking = await _context.Bookings
                .Include(b => b.Package)
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.UserID == user.UserID);

            if (booking == null) return NotFound();

            // Calculate Balance
            decimal totalPaid = booking.Payments
                .Where(p => p.PaymentStatus == "Completed" || p.PaymentStatus == "Deposit")
                .Sum(p => p.AmountPaid);

            decimal balance = booking.FinalAmount - totalPaid;

            if (balance <= 0)
            {
                TempData["Error"] = "This booking is already fully paid.";
                return RedirectToAction("BookingHistoryDetails", new { id = bookingId });
            }

            // Store Payment Context in Session
            HttpContext.Session.SetInt32("BalancePayment_BookingID", bookingId);
            HttpContext.Session.SetString("BalancePayment_Amount", balance.ToString());
            HttpContext.Session.SetString("PaymentType", "Balance"); // Mark as Balance payment

            // Redirect to the existing Payment Method selection page
            return RedirectToAction("PaymentMethod");
        }
    }


}