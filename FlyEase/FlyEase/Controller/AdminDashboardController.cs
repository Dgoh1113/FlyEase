using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlyEase.Services;

namespace FlyEase.Controllers
{
    [Route("AdminDashboard")]
    [Authorize(Roles = "Admin")]

    public class AdminDashboardController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminDashboardController(FlyEaseDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // ==========================================
        // 1. MAIN DASHBOARD SUMMARY
        // ==========================================
        [HttpGet("AdminDashboard")]
        public async Task<IActionResult> AdminDashboard()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalBookings = await _context.Bookings.CountAsync();
            var pendingBookings = await _context.Bookings.CountAsync(b => b.BookingStatus == "Pending");
            var totalRevenue = await _context.Payments.Where(p => p.PaymentStatus == "Completed").SumAsync(p => p.AmountPaid);

            var recentBookings = await _context.Bookings
                .Include(b => b.User).Include(b => b.Package)
                .OrderByDescending(b => b.BookingDate).Take(5).ToListAsync();

            var lowStock = await _context.Packages.Where(p => p.AvailableSlots < 10).OrderBy(p => p.AvailableSlots).Take(5).ToListAsync();

            var sixMonthsAgo = DateTime.Now.AddMonths(-6);

            var rawRevenueData = await _context.Payments
                .Where(p => p.PaymentDate >= sixMonthsAgo && p.PaymentStatus == "Completed")
                .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(p => p.AmountPaid)
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            var revenueMonths = rawRevenueData
                .Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM"))
                .ToList();

            var revenueValues = rawRevenueData
                .Select(x => x.Total)
                .ToList();

            var statusCounts = await _context.Bookings
                .GroupBy(b => b.BookingStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var vm = new AdminDashboardVM
            {
                TotalUsers = totalUsers,
                TotalBookings = totalBookings,
                PendingBookings = pendingBookings,
                TotalRevenue = totalRevenue,
                RecentBookings = recentBookings,
                LowStockPackages = lowStock,
                RevenueMonths = revenueMonths,
                RevenueValues = revenueValues,
                PendingBookingsCount = statusCounts.FirstOrDefault(x => x.Status == "Pending")?.Count ?? 0,
                CompletedBookingsCount = statusCounts.FirstOrDefault(x => x.Status == "Completed")?.Count ?? 0,
                CancelledBookingsCount = statusCounts.FirstOrDefault(x => x.Status == "Cancelled")?.Count ?? 0
            };

            return View(vm);
        }

        // ==========================================
        // 2. USERS MANAGEMENT
        // ==========================================
        [HttpGet("Users")]
        public async Task<IActionResult> Users()
        {
            var vm = new UsersPageVM { Users = await _context.Users.OrderByDescending(u => u.CreatedDate).ToListAsync() };
            return View(vm);
        }

        [HttpPost("SaveUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUser(UsersPageVM model)
        {
            var input = model.CurrentUser;
            if (input.UserID > 0)
            {
                var user = await _context.Users.FindAsync(input.UserID);
                if (user != null)
                {
                    user.FullName = input.FullName;
                    user.Email = input.Email;
                    user.Phone = input.Phone;
                    user.Role = input.Role;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "User updated successfully!";
                }
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost("DeleteUser")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                if (await _context.Bookings.AnyAsync(b => b.UserID == id))
                    TempData["Error"] = "Cannot delete user with bookings.";
                else
                {
                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "User deleted.";
                }
            }
            return RedirectToAction(nameof(Users));
        }

        // ==========================================
        // 3. BOOKINGS MANAGEMENT
        // ==========================================
        [HttpGet("Bookings")]
        public async Task<IActionResult> Bookings(string status = "All")
        {
            var query = _context.Bookings.Include(b => b.User).Include(b => b.Package).AsQueryable();
            if (status != "All" && !string.IsNullOrEmpty(status)) query = query.Where(b => b.BookingStatus == status);

            var vm = new BookingsPageVM { Bookings = await query.OrderByDescending(b => b.BookingDate).ToListAsync() };
            return View(vm);
        }

        [HttpPost("SaveBooking")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBooking(BookingsPageVM model)
        {
            var input = model.CurrentBooking;
            if (input.BookingID == 0) return RedirectToAction(nameof(Bookings));

            var booking = await _context.Bookings.FindAsync(input.BookingID);
            if (booking != null)
            {
                booking.BookingStatus = input.BookingStatus;
                booking.TravelDate = input.TravelDate;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Booking updated!";
            }
            return RedirectToAction(nameof(Bookings));
        }

        [HttpPost("UpdateBookingStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBookingStatus(BookingsPageVM model)
        {
            var input = model.CurrentBooking;

            // 1. Fetch Booking with related data
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Package)
                .FirstOrDefaultAsync(b => b.BookingID == input.BookingID);

            if (booking != null)
            {
                // 2. Logic to prevent sending duplicate emails if simply saving
                bool isJustCompleted = (input.BookingStatus == "Completed" && booking.BookingStatus != "Completed");

                // 3. Update Status
                booking.BookingStatus = input.BookingStatus;
                await _context.SaveChangesAsync();

                // 4. Send Email ONLY if status changed to Completed
                if (isJustCompleted)
                {
                    try
                    {
                        // Extract the first image if available (ImageURL format: "img1.jpg;img2.jpg")
                        string packageImage = "";
                        if (!string.IsNullOrEmpty(booking.Package.ImageURL))
                        {
                            var images = booking.Package.ImageURL.Split(';');
                            if (images.Length > 0)
                            {
                                packageImage = images[0];
                            }
                        }

                        // Send the email
                        var emailService = new EmailService();
                        await emailService.SendReviewInvitation(
                            booking.User.Email,
                            booking.User.FullName,
                            booking.BookingID,
                            booking.Package.PackageName,
                            packageImage // <--- Pass the image here
                        );

                        TempData["Success"] = "Booking marked Completed & Review Email Sent!";
                    }
                    catch (Exception ex)
                    {
                        TempData["Warning"] = "Booking saved, but Email failed: " + ex.Message;
                    }
                }
                else
                {
                    TempData["Success"] = "Booking status updated successfully.";
                }
            }

            return RedirectToAction("Bookings");
        }

        [HttpPost("DeleteBooking")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var b = await _context.Bookings.FindAsync(id);
            if (b != null)
            {
                _context.Bookings.Remove(b);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Booking deleted.";
            }
            return RedirectToAction(nameof(Bookings));
        }

        // ==========================================
        // 4. PACKAGES MANAGEMENT
        // ==========================================
        [HttpGet("Packages")]
        public async Task<IActionResult> Packages()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings).ThenInclude(b => b.Feedbacks)
                .OrderByDescending(p => p.PackageID)
                .ToListAsync();

            foreach (var p in packages)
            {
                var feedbacks = p.Bookings.SelectMany(b => b.Feedbacks).ToList();
                p.AverageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;
            }

            var vm = new PackagesPageVM
            {
                Packages = packages,
                Categories = await _context.PackageCategories.ToListAsync(),
                CurrentPackage = new Package()
            };
            return View(vm);
        }

        [HttpPost("SavePackage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePackage(PackagesPageVM model)
        {
            var input = model.CurrentPackage;
            var imagePaths = new List<string>();

            if (input.PackageID > 0)
            {
                var existingPkg = await _context.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.PackageID == input.PackageID);
                if (existingPkg != null && !string.IsNullOrEmpty(existingPkg.ImageURL))
                {
                    imagePaths.AddRange(existingPkg.ImageURL.Split(';'));
                }
            }

            if (input.ImageFiles != null && input.ImageFiles.Count > 0)
            {
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "img");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                foreach (var file in input.ImageFiles)
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    using (var fileStream = new FileStream(Path.Combine(uploadsFolder, uniqueFileName), FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    imagePaths.Add("/img/" + uniqueFileName);
                }
            }
            input.ImageURL = imagePaths.Count > 0 ? string.Join(";", imagePaths) : null;

            if (input.PackageID == 0)
            {
                _context.Packages.Add(input);
                TempData["Success"] = "Package created successfully!";
            }
            else
            {
                var existing = await _context.Packages
                    .FirstOrDefaultAsync(p => p.PackageID == input.PackageID);

                if (existing != null)
                {
                    existing.PackageName = input.PackageName;
                    existing.CategoryID = input.CategoryID;
                    existing.Destination = input.Destination;
                    existing.Price = input.Price;
                    existing.StartDate = input.StartDate;
                    existing.EndDate = input.EndDate;
                    existing.AvailableSlots = input.AvailableSlots;
                    existing.Description = input.Description;
                    existing.ImageURL = input.ImageURL;
                    // existing.MapUrl = input.MapUrl; 

                    _context.Packages.Update(existing);
                    TempData["Success"] = "Package updated successfully!";
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Packages));
        }

        [HttpPost("DeletePackage")]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package != null)
            {
                if (await _context.Bookings.AnyAsync(b => b.PackageID == id))
                {
                    TempData["Error"] = "Cannot delete package: Active bookings exist.";
                }
                else
                {
                    _context.Packages.Remove(package);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Package deleted successfully.";
                }
            }
            return RedirectToAction(nameof(Packages));
        }

        // ==========================================
        // 5. SALES REPORT
        // ==========================================ok

        [HttpGet("Report")]
        public async Task<IActionResult> Report(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string dateFilterType = "booking",
            [FromQuery] string paymentMethodFilter = "All",
            [FromQuery] string bookingStatusFilter = "All")
        {
            // Set default dates (last 30 days)
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? DateTime.Now.AddDays(-30);

            // Initialize ViewModel
            var vm = new SalesReportVM
            {
                StartDate = start,
                EndDate = end,
                DateFilterType = dateFilterType,
                PaymentMethodFilter = paymentMethodFilter,
                BookingStatusFilter = bookingStatusFilter
            };

            // ========== FETCH ALL DATA NEEDED FOR REPORT ==========
            var bookingsQuery = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Package)
                .Include(b => b.Payments)
                .AsQueryable();

            var paymentsQuery = _context.Payments
                .Include(p => p.Booking)
                .ThenInclude(b => b.User)
                .Include(p => p.Booking)
                .ThenInclude(b => b.Package)
                .AsQueryable();

            // ========== APPLY DATE FILTERS ==========
            switch (dateFilterType.ToLower())
            {
                case "payment":
                    paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= start && p.PaymentDate <= end);
                    break;
                case "travel":
                    bookingsQuery = bookingsQuery.Where(b => b.TravelDate >= start && b.TravelDate <= end);
                    break;
                case "booking":
                default:
                    bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= start && b.BookingDate <= end);
                    break;
            }

            // ========== APPLY STATUS FILTERS ==========
            if (bookingStatusFilter != "All" && !string.IsNullOrEmpty(bookingStatusFilter))
            {
                bookingsQuery = bookingsQuery.Where(b => b.BookingStatus == bookingStatusFilter);
            }

            // Execute queries
            var bookings = await bookingsQuery.ToListAsync();
            var payments = await paymentsQuery.ToListAsync();

            // ========== CALCULATE SUMMARY STATISTICS ==========
            vm.TotalBookings = bookings.Count;
            vm.TotalPayments = payments.Count;
            vm.TotalRevenue = payments.Where(p => p.PaymentStatus == "Completed").Sum(p => p.AmountPaid);

            vm.CompletedBookings = bookings.Count(b => b.BookingStatus == "Completed");
            vm.PendingBookings = bookings.Count(b => b.BookingStatus == "Pending");
            vm.CancelledBookings = bookings.Count(b => b.BookingStatus == "Cancelled");

            vm.CompletedPayments = payments.Where(p => p.PaymentStatus == "Completed").Sum(p => p.AmountPaid);
            vm.PendingPayments = payments.Where(p => p.PaymentStatus == "Pending").Sum(p => p.AmountPaid);
            vm.FailedPayments = payments.Where(p => p.PaymentStatus == "Failed").Sum(p => p.AmountPaid);

            vm.AverageBookingValue = bookings.Count > 0 ? bookings.Average(b => b.FinalAmount) : 0;

            // Payment Success Rate: (Completed Payments / Total Payments) * 100
            vm.PaymentSuccessRate = payments.Count > 0
                ? (decimal)(payments.Count(p => p.PaymentStatus == "Completed") * 100.0 / payments.Count)
                : 0;

            // ========== BUILD REVENUE CHART DATA (Daily breakdown) ==========
            var revenuByDay = bookings
                .GroupBy(b => dateFilterType.ToLower() == "payment"
                    ? b.Payments.FirstOrDefault()?.PaymentDate.Date ?? b.BookingDate.Date
                    : dateFilterType.ToLower() == "travel"
                    ? b.TravelDate.Date
                    : b.BookingDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(b => b.FinalAmount)
                })
                .ToList();

            vm.RevenueChartDates = revenuByDay.Select(r => r.Date.ToString("dd MMM")).ToList();
            vm.RevenueChartValues = revenuByDay.Select(r => r.Revenue).ToList();

            // ========== BUILD PAYMENT METHOD PIE CHART DATA ==========
            var paymentByMethod = payments
                .Where(p => paymentMethodFilter == "All" || p.PaymentMethod.Contains(paymentMethodFilter))
                .GroupBy(p => ExtractPaymentMethod(p.PaymentMethod))
                .Select(g => new
                {
                    Method = g.Key,
                    Amount = g.Sum(p => p.AmountPaid),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            vm.PaymentMethodLabels = paymentByMethod.Select(p => $"{p.Method} ({p.Count})").ToList();
            vm.PaymentMethodValues = paymentByMethod.Select(p => p.Amount).ToList();
            vm.PaymentMethodColors = GenerateColors(paymentByMethod.Count);

            // ========== BUILD BOOKING STATUS CHART DATA ==========
            vm.BookingStatusLabels = new List<string> { "Completed", "Pending", "Cancelled" };
            vm.BookingStatusValues = new List<int>
            {
                vm.CompletedBookings,
                vm.PendingBookings,
                vm.CancelledBookings
            };

            // ========== BUILD DETAILED TABLE DATA ==========
            foreach (var booking in bookings.OrderByDescending(b => b.BookingDate))
            {
                var totalPaid = booking.Payments.Where(p => p.PaymentStatus == "Completed").Sum(p => p.AmountPaid);
                var lastPayment = booking.Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();

                var detail = new SalesReportDetailVM
                {
                    BookingID = booking.BookingID,
                    CustomerName = booking.User.FullName,
                    CustomerEmail = booking.User.Email,
                    PackageName = booking.Package.PackageName,
                    BookingDate = booking.BookingDate,
                    TravelDate = booking.TravelDate,
                    NumberOfPeople = booking.NumberOfPeople,
                    BookingAmount = booking.FinalAmount,
                    TotalPaid = totalPaid,
                    BalanceDue = booking.FinalAmount - totalPaid,
                    BookingStatus = booking.BookingStatus,
                    PaymentStatus = DeterminePaymentStatus(booking.FinalAmount, totalPaid),
                    PaymentMethod = booking.Payments.FirstOrDefault()?.PaymentMethod ?? "N/A",
                    LastPaymentDate = lastPayment?.PaymentDate
                };

                // Apply payment method filter
                if (paymentMethodFilter != "All")
                {
                    if (!detail.PaymentMethod.Contains(paymentMethodFilter))
                        continue;
                }

                vm.Details.Add(detail);
            }

            // ========== POPULATE DROPDOWN OPTIONS ==========
            vm.AvailablePaymentMethods = new List<string>
            {
                "All",
                "Credit Card",
                "Bank Transfer",
                "Touch 'n Go",
                "Cash Payment"
            };

            vm.AvailableBookingStatuses = new List<string>
            {
                "All",
                "Completed",
                "Pending",
                "Cancelled"
            };

            return View(vm);
        }

        // ========== HELPER METHODS ==========
        private string ExtractPaymentMethod(string paymentMethodText)
        {
            // Extract clean payment method name from stored text
            // Examples: "Bank Transfer (Verified: BANK123456789)" -> "Bank Transfer"
            if (paymentMethodText.Contains("("))
                return paymentMethodText.Substring(0, paymentMethodText.IndexOf("(")).Trim();
            return paymentMethodText;
        }

        private string DeterminePaymentStatus(decimal bookingAmount, decimal totalPaid)
        {
            if (bookingAmount <= 0) return "Free";
            if (totalPaid >= bookingAmount) return "Fully Paid";
            if (totalPaid > 0) return "Partial";
            return "Unpaid";
        }

        private List<string> GenerateColors(int count)
        {
            var colors = new List<string>
            {
                "#4E73DF", // Blue
                "#1CC88A", // Green
                "#36B9CC", // Cyan
                "#F6C23E", // Yellow
                "#E74A3B", // Red
                "#858796", // Gray
                "#FF6B6B", // Light Red
                "#4ECDC4"  // Teal
            };

            while (colors.Count < count)
            {
                colors.AddRange(colors);
            }

            return colors.Take(count).ToList();
        }

        // ==========================================
        // 6. ANALYTICS (Updated Logic)
        // ==========================================
        [HttpGet("Analytics")]
        public async Task<IActionResult> Analytics()
        {
            // 1. Fetch Data
            var allFeedback = await _context.Feedbacks
                .Include(f => f.User)
                .Include(f => f.Booking).ThenInclude(b => b.Package) // Include Package for popularity stats
                .OrderByDescending(f => f.CreatedDate)
                .ToListAsync();

            if (!allFeedback.Any())
            {
                return View(new FeedbackAnalyticsViewModel());
            }

            // 2. Calculate General Stats
            var totalReviews = allFeedback.Count;
            var averageRating = allFeedback.Average(f => f.Rating);
            var positiveCount = allFeedback.Count(f => f.Rating >= 4);
            var positivePercentage = totalReviews > 0 ? (double)positiveCount / totalReviews * 100 : 0;

            // 3. NEW: Calculate Popularity (Group by Package Name)
            var packageStats = allFeedback
                .GroupBy(f => f.Booking.Package.PackageName)
                .Select(g => new PopularPackageViewModel
                {
                    PackageName = g.Key,
                    AverageRating = g.Average(f => f.Rating),
                    ReviewCount = g.Count()
                })
                .ToList();

            var mostPopular = packageStats.OrderByDescending(p => p.AverageRating).ThenByDescending(p => p.ReviewCount).FirstOrDefault();
            var leastPopular = packageStats.OrderBy(p => p.AverageRating).ThenByDescending(p => p.ReviewCount).FirstOrDefault();

            // 4. Prepare Chart Data
            var ratingCounts = new Dictionary<int, int>
    {
        { 5, allFeedback.Count(f => f.Rating == 5) },
        { 4, allFeedback.Count(f => f.Rating == 4) },
        { 3, allFeedback.Count(f => f.Rating == 3) },
        { 2, allFeedback.Count(f => f.Rating == 2) },
        { 1, allFeedback.Count(f => f.Rating == 1) }
    };

            // 5. Map to ViewModel
            var viewModel = new FeedbackAnalyticsViewModel
            {
                AverageRating = averageRating,
                TotalReviews = totalReviews,
                PositivePercentage = positivePercentage,
                RatingCounts = ratingCounts,
                RecentReviews = allFeedback.Take(10).ToList(), // Took 10 for the bottom list
                MostPopularPackage = mostPopular,
                LeastPopularPackage = leastPopular
            };

            return View(viewModel);
        }
        [HttpGet("Discounts")]
        public async Task<IActionResult> Discounts()
        {
            var vm = new DiscountsPageVM
            {
                Discounts = await _context.DiscountTypes.ToListAsync()
            };
            return View(vm);
        }

        [HttpPost("SaveDiscount")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDiscount(DiscountsPageVM model)
        {
            var input = model.CurrentDiscount;

            // Basic Validation
            if (string.IsNullOrEmpty(input.DiscountName))
            {
                TempData["Error"] = "Discount Name is required.";
                return RedirectToAction(nameof(Discounts));
            }

            if (input.DiscountTypeID == 0)
            {
                // --- CREATE ---
                _context.DiscountTypes.Add(input);
                TempData["Success"] = "Discount created successfully!";
            }
            else
            {
                // --- UPDATE ---
                var existing = await _context.DiscountTypes.FindAsync(input.DiscountTypeID);
                if (existing != null)
                {
                    existing.DiscountName = input.DiscountName;
                    existing.DiscountRate = input.DiscountRate;
                    existing.DiscountAmount = input.DiscountAmount;

                    _context.DiscountTypes.Update(existing);
                    TempData["Success"] = "Discount updated successfully!";
                }
                else
                {
                    TempData["Error"] = "Discount not found.";
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Discounts));
        }

        [HttpPost("DeleteDiscount")]
        public async Task<IActionResult> DeleteDiscount(int id)
        {
            var discount = await _context.DiscountTypes.FindAsync(id);
            if (discount != null)
            {
                // Check if used in any bookings before deleting to prevent crashes
                bool isUsed = await _context.BookingDiscounts.AnyAsync(bd => bd.DiscountTypeID == id);

                if (isUsed)
                {
                    TempData["Error"] = "Cannot delete this discount because it has been applied to existing bookings.";
                }
                else
                {
                    _context.DiscountTypes.Remove(discount);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Discount deleted successfully.";
                }
            }
            return RedirectToAction(nameof(Discounts));
        }
    }

}