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

        [HttpGet("SalesReport")]
        [HttpGet("")]
        public async Task<IActionResult> SalesReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string dateFilterType = "booking",
            [FromQuery] string paymentMethodFilter = "All",
            [FromQuery] string bookingStatusFilter = "All")
        {
            // 1. Defaults for Filtered Table
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? DateTime.Now.AddDays(-30);

            var vm = new SalesReportVM
            {
                StartDate = start,
                EndDate = end,
                DateFilterType = dateFilterType,
                PaymentMethodFilter = paymentMethodFilter,
                BookingStatusFilter = bookingStatusFilter
            };

            // 2. Fetch Data for Table
            var bookingsQuery = _context.Bookings
                .Include(b => b.User).Include(b => b.Package).Include(b => b.Payments).AsQueryable();
            var paymentsQuery = _context.Payments
                .Include(p => p.Booking).ThenInclude(b => b.User)
                .Include(p => p.Booking).ThenInclude(b => b.Package).AsQueryable();

            // Apply filters for Table
            if (dateFilterType.ToLower() == "payment") paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= start && p.PaymentDate <= end);
            else if (dateFilterType.ToLower() == "travel") bookingsQuery = bookingsQuery.Where(b => b.TravelDate >= start && b.TravelDate <= end);
            else bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= start && b.BookingDate <= end);

            if (bookingStatusFilter != "All" && !string.IsNullOrEmpty(bookingStatusFilter))
                bookingsQuery = bookingsQuery.Where(b => b.BookingStatus == bookingStatusFilter);

            var bookingsTable = await bookingsQuery.ToListAsync();
            var paymentsTable = await paymentsQuery.ToListAsync();

            // 3. Stats for Cards
            vm.TotalBookings = bookingsTable.Count;
            vm.TotalPayments = paymentsTable.Count;
            vm.TotalRevenue = paymentsTable.Where(p => p.PaymentStatus == "Completed").Sum(p => p.AmountPaid);
            vm.AverageBookingValue = bookingsTable.Count > 0 ? bookingsTable.Average(b => b.FinalAmount) : 0;
            vm.PaymentSuccessRate = paymentsTable.Count > 0 ? (decimal)(paymentsTable.Count(p => p.PaymentStatus == "Completed") * 100.0 / paymentsTable.Count) : 0;

            // 4. Populate Detail List
            foreach (var b in bookingsTable.OrderByDescending(x => x.BookingDate))
            {
                var paid = b.Payments.Where(p => p.PaymentStatus == "Completed").Sum(p => p.AmountPaid);
                var lastPay = b.Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();
                var pm = b.Payments.FirstOrDefault()?.PaymentMethod ?? "N/A";

                if (paymentMethodFilter != "All" && !pm.Contains(paymentMethodFilter)) continue;

                vm.Details.Add(new SalesReportDetailVM
                {
                    BookingID = b.BookingID,
                    CustomerName = b.User.FullName,
                    CustomerEmail = b.User.Email,
                    PackageName = b.Package.PackageName,
                    BookingDate = b.BookingDate,
                    TravelDate = b.TravelDate,
                    NumberOfPeople = b.NumberOfPeople,
                    BookingAmount = b.FinalAmount,
                    TotalPaid = paid,
                    BalanceDue = b.FinalAmount - paid,
                    BookingStatus = b.BookingStatus,
                    PaymentStatus = DeterminePaymentStatus(b.FinalAmount, paid),
                    PaymentMethod = pm,
                    LastPaymentDate = lastPay?.PaymentDate
                });
            }

            // =========================================================================
            //  CHART DATA GENERATION (Independent of Table Filters)
            // =========================================================================
            var oneYearAgo = DateTime.Now.AddYears(-1).AddDays(-1);

            // A. Fetch Base Data (Last 1 Year)
            var allPayments = await _context.Payments
                .Where(p => p.PaymentStatus == "Completed" && p.PaymentDate >= oneYearAgo)
                .Select(p => new { p.PaymentDate, p.AmountPaid, PackageName = p.Booking.Package.PackageName })
                .ToListAsync();

            var allBookings = await _context.Bookings
                .Where(b => b.BookingDate >= oneYearAgo)
                .Select(b => new { b.BookingDate, b.BookingStatus })
                .ToListAsync();

            var allUsers = await _context.Users
                .Where(u => u.CreatedDate >= oneYearAgo)
                .Select(u => new { u.CreatedDate })
                .ToListAsync();

            var allFeedbacks = await _context.Feedbacks
                .Include(f => f.Booking).ThenInclude(b => b.Package)
                .Where(f => f.CreatedDate >= oneYearAgo)
                .Select(f => new { f.CreatedDate, f.Rating, PackageName = f.Booking.Package.PackageName })
                .ToListAsync();


            // --- HELPER: Fill Time Series Data (Decimal) ---
            void FillTimeSeries(IEnumerable<dynamic> source, Func<dynamic, DateTime> dateSelector, Func<IEnumerable<dynamic>, decimal> aggregator,
                List<string> labels7, List<decimal> values7,
                List<string> labels30, List<decimal> values30,
                List<string> labels1Y, List<decimal> values1Y)
            {
                // 7 Days
                for (int i = 6; i >= 0; i--)
                {
                    var d = DateTime.Now.AddDays(-i).Date;
                    labels7.Add(d.ToString("dd MMM"));
                    values7.Add(aggregator(source.Where(x => dateSelector(x).Date == d)));
                }
                // 30 Days
                for (int i = 29; i >= 0; i--)
                {
                    var d = DateTime.Now.AddDays(-i).Date;
                    labels30.Add(d.ToString("dd MMM"));
                    values30.Add(aggregator(source.Where(x => dateSelector(x).Date == d)));
                }
                // 1 Year (Monthly)
                for (int i = 11; i >= 0; i--)
                {
                    var d = DateTime.Now.AddMonths(-i);
                    var startM = new DateTime(d.Year, d.Month, 1);
                    var endM = startM.AddMonths(1).AddTicks(-1);
                    labels1Y.Add(startM.ToString("MMM yyyy"));
                    values1Y.Add(aggregator(source.Where(x => dateSelector(x) >= startM && dateSelector(x) <= endM)));
                }
            }

            // --- 1. REVENUE CHARTS ---
            FillTimeSeries(allPayments, p => p.PaymentDate, list => list.Sum(x => (decimal)x.AmountPaid),
                vm.RevenueLabels7Days, vm.RevenueValues7Days,
                vm.RevenueLabels30Days, vm.RevenueValues30Days,
                vm.RevenueLabels1Year, vm.RevenueValues1Year);

            // Revenue Donut (Top 5 Packages)
            var revDonut = allPayments.GroupBy(p => (string)p.PackageName)
                .Select(g => new { Name = g.Key, Total = g.Sum(x => (decimal)x.AmountPaid) })
                .OrderByDescending(x => x.Total).Take(5).ToList();
            vm.RevenueDonutLabels = revDonut.Select(x => x.Name).ToList();
            vm.RevenueDonutValues = revDonut.Select(x => x.Total).ToList();


            // --- HELPER: Fill Time Series Data (Int) ---
            void FillTimeSeriesInt(IEnumerable<dynamic> source, Func<dynamic, DateTime> dateSelector,
                List<string> labels7, List<int> values7,
                List<string> labels30, List<int> values30,
                List<string> labels1Y, List<int> values1Y)
            {
                // 7 Days
                for (int i = 6; i >= 0; i--)
                {
                    var d = DateTime.Now.AddDays(-i).Date;
                    labels7.Add(d.ToString("dd MMM"));
                    values7.Add(source.Count(x => dateSelector(x).Date == d));
                }
                // 30 Days
                for (int i = 29; i >= 0; i--)
                {
                    var d = DateTime.Now.AddDays(-i).Date;
                    labels30.Add(d.ToString("dd MMM"));
                    values30.Add(source.Count(x => dateSelector(x).Date == d));
                }
                // 1 Year
                for (int i = 11; i >= 0; i--)
                {
                    var d = DateTime.Now.AddMonths(-i);
                    var startM = new DateTime(d.Year, d.Month, 1);
                    var endM = startM.AddMonths(1).AddTicks(-1);
                    labels1Y.Add(startM.ToString("MMM yyyy"));
                    values1Y.Add(source.Count(x => dateSelector(x) >= startM && dateSelector(x) <= endM));
                }
            }

            // --- 2. BOOKING CHARTS ---
            FillTimeSeriesInt(allBookings, b => b.BookingDate,
                vm.BookingLabels7Days, vm.BookingValues7Days,
                vm.BookingLabels30Days, vm.BookingValues30Days,
                vm.BookingLabels1Year, vm.BookingValues1Year);

            // Booking Donut
            var bookDonut = allBookings.GroupBy(b => (string)b.BookingStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() }).ToList();
            vm.BookingDonutLabels = bookDonut.Select(x => x.Status).ToList();
            vm.BookingDonutValues = bookDonut.Select(x => x.Count).ToList();


            // --- 3. USER CHARTS ---
            FillTimeSeriesInt(allUsers, u => u.CreatedDate,
                vm.UserLabels7Days, vm.UserValues7Days,
                vm.UserLabels30Days, vm.UserValues30Days,
                vm.UserLabels1Year, vm.UserValues1Year);


            // --- 4. PACKAGES BAR CHART ---
            void FillPackageRatings(DateTime cutoffDate, List<string> labels, List<double> values)
            {
                var ratings = allFeedbacks
                    .Where(f => f.CreatedDate >= cutoffDate)
                    .GroupBy(f => (string)f.PackageName)
                    .Select(g => new { Name = g.Key, Avg = g.Average(f => (double)f.Rating) })
                    .OrderByDescending(x => x.Avg)
                    .Take(10) // Take top 10 for bar chart
                    .ToList();

                labels.AddRange(ratings.Select(x => x.Name));
                values.AddRange(ratings.Select(x => x.Avg));
            }

            FillPackageRatings(DateTime.Now.AddDays(-7), vm.PackageRatingLabels7Days, vm.PackageRatingValues7Days);
            FillPackageRatings(DateTime.Now.AddDays(-30), vm.PackageRatingLabels30Days, vm.PackageRatingValues30Days);
            FillPackageRatings(DateTime.Now.AddYears(-1), vm.PackageRatingLabels1Year, vm.PackageRatingValues1Year);


            // Dropdowns
            vm.AvailablePaymentMethods = new List<string> { "All", "Credit Card", "Bank Transfer", "Touch 'n Go", "Cash Payment" };
            vm.AvailableBookingStatuses = new List<string> { "All", "Completed", "Pending", "Cancelled" };

            return View(vm);
        }

        private string DeterminePaymentStatus(decimal bookingAmount, decimal totalPaid)
        {
            if (bookingAmount <= 0) return "Free";
            if (totalPaid >= bookingAmount) return "Fully Paid";
            if (totalPaid > 0) return "Partial";
            return "Unpaid";
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