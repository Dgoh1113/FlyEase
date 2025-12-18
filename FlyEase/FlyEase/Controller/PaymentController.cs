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

// Data Transfer Object for Price Calculation API
public class PriceRequest
{
    public int PackageId { get; set; }
    public int People { get; set; }
    public int Seniors { get; set; } // Count of Seniors
    public int Juniors { get; set; } // Count of Juniors
    public DateTime TravelDate { get; set; }
}

namespace FlyEase.Controllers
{
    public class PaymentController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly StripeService _stripeService; // Dependency Injection
        private readonly ILogger<PaymentController> _logger;

        // --- UPDATED CONSTRUCTOR ---
        public PaymentController(
            FlyEaseDbContext context,
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            StripeService stripeService) // Inject Service here
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _stripeService = stripeService; // Assign the injected instance
        }

        // =========================================================
        // STEP 1: CUSTOMER INFO
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> CustomerInfo(int packageId, int people = 1)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) { TempData["Error"] = "Please login first"; return RedirectToAction("Login", "Auth"); }

            // --- RESTRICT ADMIN & STAFF FROM BOOKING ---
            if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                TempData["Error"] = "Administrator and Staff accounts are not allowed to make bookings.";
                return RedirectToAction("Index", "Home");
            }

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
                Address = user.Address,
                NumberOfSeniors = 0,
                NumberOfJuniors = 0
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
            if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                TempData["Error"] = "Administrator and Staff accounts are not allowed to make bookings.";
                return RedirectToAction("Index", "Home");
            }

            if ((model.NumberOfSeniors + model.NumberOfJuniors) > model.NumberOfPeople)
            {
                ModelState.AddModelError("NumberOfPeople", "Total people must be greater than or equal to Seniors + Juniors.");
            }

            if (!ModelState.IsValid) return View(model);

            CalculateDiscounts(model);

            HttpContext.Session.SetCustomerInfo(model);
            // Clear previous applied discounts (they'll be recalculated when user clicks "Check Discounts")
            HttpContext.Session.Remove("AppliedDiscounts");
            return RedirectToAction("PaymentMethod");
        }

        // =========================================================
        // API: DYNAMIC PRICE CALCULATION
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> CalculatePrice([FromBody] PriceRequest request)
        {
            var package = await _context.Packages.FindAsync(request.PackageId);
            if (package == null) return Json(new { success = false, message = "Package not found" });

            decimal basePriceTotal = package.Price * request.People;
            decimal totalDiscount = 0;
            var discountDetails = new List<string>();

            // List to track applied discounts for database storage
            var appliedDiscounts = new List<DiscountInfo>();

            // 1. BULK DISCOUNT (> 3 People) - Hardcoded logic
            if (request.People > 3)
            {
                decimal bulkDisc = basePriceTotal * 0.10m;
                totalDiscount += bulkDisc;
                discountDetails.Add($"Bulk Group (>3 pax): -RM {bulkDisc:N2}");

                appliedDiscounts.Add(new DiscountInfo
                {
                    Name = "Bulk Group Discount",
                    Type = "Bulk",
                    Rate = 0.10m,
                    Amount = bulkDisc
                });
            }

            // 2. SENIOR DISCOUNT (60+) - Hardcoded logic
            if (request.Seniors > 0)
            {
                decimal seniorDiscAmount = (package.Price * 0.20m) * request.Seniors;
                totalDiscount += seniorDiscAmount;
                discountDetails.Add($"Senior Citizen ({request.Seniors}x): -RM {seniorDiscAmount:N2}");

                appliedDiscounts.Add(new DiscountInfo
                {
                    Name = "Senior Citizen Discount",
                    Type = "Senior",
                    Rate = 0.20m,
                    Amount = seniorDiscAmount
                });
            }

            // 3. JUNIOR DISCOUNT (<12) - Hardcoded logic
            if (request.Juniors > 0)
            {
                decimal juniorDiscAmount = (package.Price * 0.15m) * request.Juniors;
                totalDiscount += juniorDiscAmount;
                discountDetails.Add($"Junior/Child ({request.Juniors}x): -RM {juniorDiscAmount:N2}");

                appliedDiscounts.Add(new DiscountInfo
                {
                    Name = "Junior/Child Discount",
                    Type = "Junior",
                    Rate = 0.15m,
                    Amount = juniorDiscAmount
                });
            }

            // 4. DATABASE DISCOUNTS - Apply only if criteria match
            var dbDiscounts = await _context.DiscountTypes
                .Where(d => d.IsActive)
                .ToListAsync();

            var now = DateTime.Now;
            var travelDate = request.TravelDate;
            var daysUntilTravel = (travelDate - now).Days;

            foreach (var d in dbDiscounts)
            {
                bool isEligible = true;
                List<string> reasons = new List<string>();

                // Check Min Pax criteria
                if (d.MinPax.HasValue && request.People < d.MinPax.Value)
                {
                    isEligible = false;
                    reasons.Add($"Min {d.MinPax.Value} people required");
                }

                // Check Min Spend criteria
                if (d.MinSpend.HasValue && basePriceTotal < d.MinSpend.Value)
                {
                    isEligible = false;
                    reasons.Add($"Min spend RM {d.MinSpend.Value} required");
                }

                // Check Date Range
                if (d.StartDate.HasValue && now < d.StartDate.Value)
                {
                    isEligible = false;
                    reasons.Add($"Starts on {d.StartDate.Value:dd MMM yyyy}");
                }

                if (d.EndDate.HasValue && now > d.EndDate.Value)
                {
                    isEligible = false;
                    reasons.Add($"Expired on {d.EndDate.Value:dd MMM yyyy}");
                }

                // Check Age-based discounts
                if (d.AgeLimit.HasValue && !string.IsNullOrEmpty(d.AgeCriteria))
                {
                    if (d.AgeCriteria == "Greater" && request.Seniors == 0)
                    {
                        isEligible = false;
                        reasons.Add($"Requires at least 1 senior (60+)");
                    }
                    else if (d.AgeCriteria == "Less" && request.Juniors == 0)
                    {
                        isEligible = false;
                        reasons.Add($"Requires at least 1 junior (<12)");
                    }
                }

                // Check Early Bird Discount
                if (d.EarlyBirdDays.HasValue && daysUntilTravel < d.EarlyBirdDays.Value)
                {
                    isEligible = false;
                    reasons.Add($"Book at least {d.EarlyBirdDays.Value} days in advance");
                }

                if (isEligible)
                {
                    decimal discountAmount = 0;

                    if (d.DiscountRate.HasValue && d.DiscountRate.Value > 0)
                    {
                        discountAmount = basePriceTotal * d.DiscountRate.Value;
                    }
                    else if (d.DiscountAmount.HasValue && d.DiscountAmount.Value > 0)
                    {
                        discountAmount = d.DiscountAmount.Value;
                    }

                    if (discountAmount > 0)
                    {
                        totalDiscount += discountAmount;
                        string discountText = d.DiscountRate.HasValue
                            ? $"{d.DiscountName} ({d.DiscountRate.Value * 100:0}%): -RM {discountAmount:N2}"
                            : $"{d.DiscountName}: -RM {discountAmount:N2}";

                        discountDetails.Add(discountText);

                        appliedDiscounts.Add(new DiscountInfo
                        {
                            Name = d.DiscountName,
                            Type = d.DiscountTypeID.ToString(),
                            Rate = d.DiscountRate,
                            Amount = discountAmount,
                            DiscountId = d.DiscountTypeID
                        });
                    }
                }
            }

            if (totalDiscount > basePriceTotal) totalDiscount = basePriceTotal;
            decimal finalAmount = basePriceTotal - totalDiscount;

            // Store applied discounts in session for later use when creating booking
            HttpContext.Session.SetObject("AppliedDiscounts", appliedDiscounts);

            return Json(new
            {
                success = true,
                basePrice = basePriceTotal,
                discountAmount = totalDiscount,
                finalAmount = finalAmount,
                breakdown = discountDetails,
                appliedDiscounts = appliedDiscounts
            });
        }

        // Helper class for discount information
        public class DiscountInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty; // "Bulk", "Senior", "Junior", or DB DiscountTypeID
            public decimal? Rate { get; set; }
            public decimal Amount { get; set; }
            public int? DiscountId { get; set; } // For database discounts only
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

            // --- Route EVERYTHING to Stripe Checkout ---
            return RedirectToAction("StripeCheckout");
        }

        // =========================================================
        // STEP 3: UNIFIED STRIPE CHECKOUT
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> StripeCheckout()
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            if (customerInfo == null) return RedirectToAction("Index", "Home");

            // 1. Get Payment Info
            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            string selectedMethod = HttpContext.Session.GetPaymentMethod(); // "Credit Card", "TouchNGo", etc.

            decimal amountToPay = (paymentType == "Deposit")
                ? customerInfo.FinalAmount * 0.30m
                : customerInfo.FinalAmount;

            var domain = $"{Request.Scheme}://{Request.Host}";

            // 2. Configure Stripe Method Types based on selection
            var paymentMethodTypes = new List<string>();

            switch (selectedMethod)
            {
                case "Credit Card":
                    paymentMethodTypes.Add("card");
                    break;
                case "Online Banking":
                    paymentMethodTypes.Add("fpx"); // FPX for Malaysia
                    break;
                case "GrabPay":
                    paymentMethodTypes.Add("grabpay");
                    break;
                case "TouchNGo":
                    // Stripe often handles TNG via FPX or specialized wallets. 
                    // Adding both ensures the user sees valid options.
                    paymentMethodTypes.Add("fpx");
                    paymentMethodTypes.Add("grabpay");
                    break;
                default:
                    // Fallback: show everything relevant
                    paymentMethodTypes.Add("card");
                    paymentMethodTypes.Add("fpx");
                    paymentMethodTypes.Add("grabpay");
                    break;
            }

            try
            {
                // 3. Create Session with SPECIFIC method types
                var session = await _stripeService.CreateCheckoutSessionAsync(
                    amountToPay,
                    "myr",
                    $"{domain}/Payment/StripeCallback?session_id={{CHECKOUT_SESSION_ID}}",
                    $"{domain}/Payment/PaymentMethod",
                    $"BK-{DateTime.Now.Ticks}",
                    paymentMethodTypes // <--- Passing the list here
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
                // Payment Successful - Process Booking
                // We use the generic "Online Payment" label or the one from session
                string method = HttpContext.Session.GetPaymentMethod() ?? "Stripe";

                return await ProcessBooking(method + " (Online)", session.PaymentIntentId);
            }

            TempData["Error"] = "Payment verification failed or was cancelled.";
            return RedirectToAction("PaymentMethod");
        }

        // =========================================================
        // CORE PROCESSING LOGIC
        // =========================================================

        private async Task<IActionResult> ProcessBooking(string paymentMethod, string transactionId)
        {
            var customerInfo = HttpContext.Session.GetCustomerInfo();
            var userId = HttpContext.Session.GetUserId();
            string paymentType = HttpContext.Session.GetString("PaymentType") ?? "Full";
            bool isDeposit = (paymentType == "Deposit");

            // Get applied discounts from session
            var appliedDiscounts = HttpContext.Session.GetObject<List<DiscountInfo>>("AppliedDiscounts")
                                   ?? new List<DiscountInfo>();

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

            // Status is Confirmed because payment is successful via Stripe
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
            await _context.SaveChangesAsync(); // Save to get BookingID

            // Save discounts to BookingDiscount table
            foreach (var discount in appliedDiscounts)
            {
                var bookingDiscount = new BookingDiscount
                {
                    BookingID = booking.BookingID,
                    DiscountTypeID = discount.DiscountId ?? 0, // 0 for non-database discounts
                    AppliedAmount = discount.Amount
                };

                // For non-database discounts (Bulk, Senior, Junior), 
                // we need to find or create a DiscountType record
                if (discount.DiscountId == null || discount.DiscountId == 0)
                {
                    // Check if a DiscountType exists for this type
                    var discountType = await _context.DiscountTypes
                        .FirstOrDefaultAsync(d => d.DiscountName == discount.Name);

                    if (discountType == null)
                    {
                        // Create a new DiscountType for system discounts
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

        private void CalculateDiscounts(CustomerInfoViewModel model)
        {
            decimal totalDiscount = 0;
            if (model.NumberOfPeople > 3) totalDiscount += model.BasePrice * 0.10m;
            decimal unitPrice = model.BasePrice / (model.NumberOfPeople > 0 ? model.NumberOfPeople : 1);
            totalDiscount += (unitPrice * 0.20m) * model.NumberOfSeniors;
            totalDiscount += (unitPrice * 0.15m) * model.NumberOfJuniors;
            if (totalDiscount > model.BasePrice) totalDiscount = model.BasePrice;
            model.DiscountAmount = totalDiscount;
            model.FinalAmount = model.BasePrice - totalDiscount;
        }

        [HttpGet]
        public async Task<IActionResult> GetDiscounts()
        {
            var discounts = await _context.DiscountTypes.Select(d => new { name = d.DiscountName, rate = d.DiscountRate, amount = d.DiscountAmount }).ToListAsync();
            discounts.Add(new { name = "Bulk Discount (>3 Pax)", rate = (decimal?)0.10, amount = (decimal?)null });
            discounts.Add(new { name = "Senior Citizen (60+)", rate = (decimal?)0.20, amount = (decimal?)null });
            discounts.Add(new { name = "Junior/Child (<12)", rate = (decimal?)0.15, amount = (decimal?)null });
            return Json(discounts);
        }
    }
}