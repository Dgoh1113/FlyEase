using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlyEase.Controllers
{
    [Route("Report")]
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ReportController(FlyEaseDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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
        // 5. SALES REPORT
        // ==========================================
        [HttpGet("SalesReport")]
        [HttpGet("")]  // Also respond to /Report/ for default
        public async Task<IActionResult> SalesReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string dateFilterType = "booking",
            [FromQuery] string paymentMethodFilter = "All",
            [FromQuery] string bookingStatusFilter = "All")
        {
            // Set default dates (last 30 days)
            var end = endDate ?? DateTime.Now.Date.AddDays(1); // End of day
            var start = startDate ?? DateTime.Now.AddDays(-30).Date; // Start of day

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
                    // Filter payments by payment date
                    paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= start && p.PaymentDate < end);
                    
                    // Get booking IDs from filtered payments
                    var paymentBookingIds = await paymentsQuery
                        .Select(p => p.BookingID)
                        .Distinct()
                        .ToListAsync();
                    
                    // Filter bookings by those IDs
                    bookingsQuery = bookingsQuery.Where(b => paymentBookingIds.Contains(b.BookingID));
                    break;

                case "travel":
                    bookingsQuery = bookingsQuery.Where(b => b.TravelDate >= start && b.TravelDate < end);
                    break;

                case "booking":
                default:
                    bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= start && b.BookingDate < end);
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
                    ? b.Payments.Where(p => p.PaymentDate >= start && p.PaymentDate < end).FirstOrDefault()?.PaymentDate.Date ?? b.BookingDate.Date
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
                .Where(p => paymentMethodFilter == "All" || ExtractPaymentMethod(p.PaymentMethod).Contains(paymentMethodFilter))
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
                    if (!ExtractPaymentMethod(detail.PaymentMethod).Contains(paymentMethodFilter))
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
                "Cash"
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

        // ==========================================
        // 5B. PACKAGE PERFORMANCE REPORT
        // ==========================================
        [HttpGet("PackagePerformanceReport")]
        public async Task<IActionResult> PackagePerformanceReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string categoryFilter = "All")
        {
            // Set default dates (last 30 days)
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? DateTime.Now.AddDays(-30);

            // Initialize ViewModel
            var vm = new PackagePerformanceReportVM
            {
                StartDate = start,
                EndDate = end,
                CategoryFilter = categoryFilter
            };

            // ========== FETCH ALL PACKAGES WITH RELATED DATA ==========
            var packagesQuery = _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings)
                .ThenInclude(b => b.Feedbacks)
                .AsQueryable();

            // Apply category filter
            if (categoryFilter != "All" && !string.IsNullOrEmpty(categoryFilter))
            {
                packagesQuery = packagesQuery.Where(p => p.Category.CategoryName == categoryFilter);
            }

            var packages = await packagesQuery.ToListAsync();

            // ========== CALCULATE PACKAGE PERFORMANCE METRICS ==========
            foreach (var package in packages)
            {
                // Filter bookings by date range
                var packageBookings = package.Bookings
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToList();

                if (packageBookings.Count == 0) continue; // Skip packages with no bookings in range

                var totalPeople = packageBookings.Sum(b => b.NumberOfPeople);
                var totalRevenue = packageBookings.Sum(b => b.FinalAmount);
                var feedbacks = packageBookings.SelectMany(b => b.Feedbacks).ToList();
                var averageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;

                // Calculate occupancy rate (booked slots / total slots * 100)
                var totalDays = (package.EndDate - package.StartDate).Days + 1;
                var bookingDays = packageBookings.Count * totalDays; // Assume each booking covers full package days
                var occupancyRate = (decimal)(totalPeople * 100.0 / (bookingDays > 0 ? bookingDays : 1));

                var detail = new PackagePerformanceDetailVM
                {
                    PackageID = package.PackageID,
                    PackageName = package.PackageName,
                    Destination = package.Destination,
                    Category = package.Category?.CategoryName ?? "Uncategorized",
                    Price = package.Price,
                    TotalBookings = packageBookings.Count,
                    TotalPeople = totalPeople,
                    TotalRevenue = totalRevenue,
                    AverageRevenuePerBooking = packageBookings.Count > 0 ? totalRevenue / packageBookings.Count : 0,
                    AverageRating = averageRating,
                    TotalReviews = feedbacks.Count,
                    AvailableSlots = package.AvailableSlots,
                    OccupancyRate = occupancyRate
                };

                vm.Details.Add(detail);
            }

            // ========== CALCULATE SUMMARY STATISTICS ==========
            vm.TotalPackages = vm.Details.Count;
            vm.TotalBookings = vm.Details.Sum(d => d.TotalBookings);
            vm.TotalRevenue = vm.Details.Sum(d => d.TotalRevenue);
            vm.AverageRevenuePerPackage = vm.Details.Count > 0 ? vm.TotalRevenue / vm.Details.Count : 0;
            vm.AverageRating = vm.Details.Count > 0 ? (decimal)vm.Details.Average(d => d.AverageRating) : 0;

            // ========== BUILD TOP PERFORMERS (By Bookings) ==========
            vm.TopPerformers = vm.Details
                .OrderByDescending(d => d.TotalBookings)
                .ThenByDescending(d => d.TotalRevenue)
                .Take(5)
                .ToList();

            // ========== BUILD BOTTOM PERFORMERS (By Bookings) ==========
            vm.BottomPerformers = vm.Details
                .OrderBy(d => d.TotalBookings)
                .ThenBy(d => d.TotalRevenue)
                .Take(5)
                .ToList();

            // ========== BUILD CHART DATA - TOP 10 BY BOOKINGS ==========
            var topByBookings = vm.Details
                .OrderByDescending(d => d.TotalBookings)
                .Take(10)
                .ToList();

            vm.PackageNames = topByBookings.Select(d => d.PackageName).ToList();
            vm.BookingCounts = topByBookings.Select(d => d.TotalBookings).ToList();
            vm.TopPackageColors = GenerateColors(topByBookings.Count);

            // ========== BUILD CHART DATA - TOP 10 BY REVENUE ==========
            var topByRevenue = vm.Details
                .OrderByDescending(d => d.TotalRevenue)
                .Take(10)
                .ToList();

            vm.RevenuePackageNames = topByRevenue.Select(d => d.PackageName).ToList();
            vm.RevenueValues = topByRevenue.Select(d => d.TotalRevenue).ToList();

            // ========== BUILD CHART DATA - TOP 10 BY RATING ==========
            var topByRating = vm.Details
                .Where(d => d.TotalReviews > 0) // Only packages with reviews
                .OrderByDescending(d => d.AverageRating)
                .Take(10)
                .ToList();

            vm.RatingPackageNames = topByRating.Select(d => d.PackageName).ToList();
            vm.RatingValues = topByRating.Select(d => d.AverageRating).ToList();

            // ========== POPULATE DROPDOWN OPTIONS ==========
            vm.AvailableCategories = new List<string> { "All" };
            var allCategories = await _context.PackageCategories
                .Select(c => c.CategoryName)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            vm.AvailableCategories.AddRange(allCategories);

            return View(vm);
        }


        // ==========================================
        // 5C. CUSTOMER INSIGHT REPORT
        // ==========================================
        [HttpGet("CustomerInsightReport")]
        public async Task<IActionResult> CustomerInsightReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string sortBy = "Spending")
        {
            // Set default dates (last 90 days)
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? DateTime.Now.AddDays(-90);

            // Initialize ViewModel
            var vm = new CustomerInsightReportVM
            {
                StartDate = start,
                EndDate = end,
                SortBy = sortBy
            };

            // ========== FETCH ALL CUSTOMERS WITH BOOKING DATA ==========
            var customersQuery = _context.Users
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Payments)
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Feedbacks)
                .Where(u => u.Bookings.Any(b => b.BookingDate >= start && b.BookingDate <= end))
                .AsQueryable();

            var customers = await customersQuery.ToListAsync();

            // ========== CALCULATE CUSTOMER METRICS ==========
            foreach (var customer in customers)
            {
                // Filter bookings by date range
                var customerBookings = customer.Bookings
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToList();

                if (customerBookings.Count == 0) continue; // Skip customers with no bookings in range

                // Calculate totals
                var totalSpent = customerBookings.Sum(b => b.Payments
                    .Where(p => p.PaymentStatus == "Completed")
                    .Sum(p => p.AmountPaid));

                var totalPeople = customerBookings.Sum(b => b.NumberOfPeople);
                var feedbacks = customerBookings.SelectMany(b => b.Feedbacks).ToList();
                var avgRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;

                // Determine Customer Tier based on spending
                string tier = totalSpent switch
                {
                    >= 10000 => "VIP",
                    >= 5000 => "Premium",
                    >= 2000 => "Standard",
                    _ => "New"
                };

                var detail = new CustomerInsightDetailVM
                {
                    UserID = customer.UserID,
                    CustomerName = customer.FullName,
                    Email = customer.Email,
                    Phone = customer.Phone ?? "N/A",
                    TotalBookings = customerBookings.Count,
                    TotalPeople = totalPeople,
                    TotalSpent = totalSpent,
                    AverageSpentPerBooking = customerBookings.Count > 0 ? totalSpent / customerBookings.Count : 0,
                    FirstBookingDate = customerBookings.Min(b => b.BookingDate),
                    LastBookingDate = customerBookings.Max(b => b.BookingDate),
                    AverageRating = (decimal)avgRating,
                    ReviewsGiven = feedbacks.Count,
                    CustomerTier = tier
                };

                vm.Details.Add(detail);
            }

            // ========== CALCULATE SUMMARY STATISTICS ==========
            vm.TotalCustomers = vm.Details.Count;
            vm.TotalBookings = vm.Details.Sum(d => d.TotalBookings);
            vm.TotalRevenue = vm.Details.Sum(d => d.TotalSpent);
            vm.AverageSpendingPerCustomer = vm.Details.Count > 0 ? vm.TotalRevenue / vm.Details.Count : 0;
            vm.ActiveCustomers = vm.Details.Count(d => d.LastBookingDate >= DateTime.Now.AddDays(-30));

            // ========== SORT DATA ==========
            var sortedDetails = sortBy.ToLower() == "frequency"
                ? vm.Details.OrderByDescending(d => d.TotalBookings).ThenByDescending(d => d.TotalSpent).ToList()
                : vm.Details.OrderByDescending(d => d.TotalSpent).ThenByDescending(d => d.TotalBookings).ToList();

            // ========== BUILD CHART DATA - TOP 10 CUSTOMERS ==========
            var topCustomers = sortedDetails.Take(10).ToList();

            vm.TopCustomerNames = topCustomers.Select(d => d.CustomerName).ToList();
            vm.TopCustomerSpending = topCustomers.Select(d => d.TotalSpent).ToList();
            vm.TopCustomerBookings = topCustomers.Select(d => d.TotalBookings).ToList();
            vm.ChartColors = GenerateColors(topCustomers.Count);

            // ========== POPULATE SORTED DETAILS FOR TABLE ==========
            vm.Details = sortedDetails;

            return View(vm);
        }
    }
}