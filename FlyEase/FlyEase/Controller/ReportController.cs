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
    }
}