using ClosedXML.Excel;
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

        // ==========================================
        // 1. SALES REPORT (VIEW)
        // ==========================================
        [HttpGet("SalesReport")]
        [HttpGet("")]
        public async Task<IActionResult> SalesReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string dateFilterType = "booking",
            [FromQuery] string paymentMethodFilter = "All",
            [FromQuery] string bookingStatusFilter = "All")
        {
            // 1. Prepare ViewModel with Data
            var vm = await BuildSalesReportVM(startDate, endDate, dateFilterType, paymentMethodFilter, bookingStatusFilter);
            return View(vm);
        }

        // ==========================================
        // 2. SALES REPORT (EXCEL EXPORT)
        // ==========================================
        [HttpGet("ExportSalesExcel")]
        public async Task<IActionResult> ExportSalesExcel(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string dateFilterType = "booking",
            [FromQuery] string paymentMethodFilter = "All",
            [FromQuery] string bookingStatusFilter = "All")
        {
            // 1. Get Data
            var vm = await BuildSalesReportVM(startDate, endDate, dateFilterType, paymentMethodFilter, bookingStatusFilter);

            // 2. Generate Excel
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Sales Report");

                // Header
                worksheet.Cell(1, 1).Value = "FlyEase Sales Report";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;

                worksheet.Cell(2, 1).Value = $"Generated: {DateTime.Now}";
                worksheet.Cell(3, 1).Value = $"Period: {vm.StartDate:dd/MM/yyyy} - {vm.EndDate:dd/MM/yyyy}";

                // Column Headers
                int row = 5;
                worksheet.Cell(row, 1).Value = "Date";
                worksheet.Cell(row, 2).Value = "Transaction ID";
                worksheet.Cell(row, 3).Value = "Customer";
                worksheet.Cell(row, 4).Value = "Email";
                worksheet.Cell(row, 5).Value = "Package";
                worksheet.Cell(row, 6).Value = "Status";
                worksheet.Cell(row, 7).Value = "Payment Method";
                worksheet.Cell(row, 8).Value = "Amount (RM)";
                worksheet.Cell(row, 9).Value = "Paid (RM)";

                var headerRange = worksheet.Range(row, 1, row, 9);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Data Rows
                row++;
                foreach (var item in vm.Details)
                {
                    worksheet.Cell(row, 1).Value = item.BookingDate;
                    worksheet.Cell(row, 2).Value = item.TransactionID;
                    worksheet.Cell(row, 3).Value = item.CustomerName;
                    worksheet.Cell(row, 4).Value = item.CustomerEmail;
                    worksheet.Cell(row, 5).Value = item.PackageName;
                    worksheet.Cell(row, 6).Value = item.BookingStatus;
                    worksheet.Cell(row, 7).Value = item.PaymentMethod;
                    worksheet.Cell(row, 8).Value = item.BookingAmount;
                    worksheet.Cell(row, 9).Value = item.TotalPaid;
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                // Return File
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SalesReport_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        // Shared Logic Builder
        private async Task<SalesReportVM> BuildSalesReportVM(DateTime? startDate, DateTime? endDate, string dateFilterType, string paymentMethodFilter, string bookingStatusFilter)
        {
            var now = DateTime.Now;
            var start = startDate ?? new DateTime(now.Year, now.Month, 1);
            var end = endDate ?? start.AddMonths(1).AddDays(-1);

            var vm = new SalesReportVM
            {
                StartDate = start,
                EndDate = end,
                DateFilterType = dateFilterType,
                PaymentMethodFilter = paymentMethodFilter,
                BookingStatusFilter = bookingStatusFilter,
                GeneratedAt = DateTime.Now,
                GeneratedBy = User.Identity?.Name ?? "Admin"
            };

            // Query Construction
            var bookingsQuery = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Package)
                .Include(b => b.Payments)
                .AsQueryable();

            // Date Filters
            if (dateFilterType.ToLower() == "travel")
                bookingsQuery = bookingsQuery.Where(b => b.TravelDate >= start && b.TravelDate <= end);
            else
                bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= start && b.BookingDate <= end.AddDays(1).AddTicks(-1));

            // Status Filter
            if (bookingStatusFilter != "All" && !string.IsNullOrEmpty(bookingStatusFilter))
                bookingsQuery = bookingsQuery.Where(b => b.BookingStatus == bookingStatusFilter);

            var bookings = await bookingsQuery.OrderByDescending(b => b.BookingDate).ToListAsync();

            // Build Details
            foreach (var booking in bookings)
            {
                var payments = booking.Payments?.ToList() ?? new List<Payment>();
                var totalPaid = payments.Where(p => p.PaymentStatus == "Completed").Sum(p => p.AmountPaid);
                var paymentMethod = payments.FirstOrDefault()?.PaymentMethod ?? "Unpaid";
                var cleanPaymentMethod = ExtractPaymentMethod(paymentMethod);

                // Payment Method Filter
                if (paymentMethodFilter != "All" && !cleanPaymentMethod.Contains(paymentMethodFilter)) continue;

                vm.Details.Add(new SalesReportDetailVM
                {
                    BookingID = booking.BookingID,
                    CustomerName = booking.User?.FullName ?? "Guest",
                    CustomerEmail = booking.User?.Email ?? "N/A",
                    PackageName = booking.Package?.PackageName ?? "Unknown Package",
                    BookingDate = booking.BookingDate,
                    TravelDate = booking.TravelDate,
                    NumberOfPeople = booking.NumberOfPeople,
                    BookingAmount = booking.FinalAmount,
                    TotalPaid = totalPaid,
                    BalanceDue = booking.FinalAmount - totalPaid,
                    BookingStatus = booking.BookingStatus,
                    PaymentStatus = DeterminePaymentStatus(booking.FinalAmount, totalPaid),
                    PaymentMethod = cleanPaymentMethod,
                    LastPaymentDate = payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentDate
                });
            }

            // Summaries
            vm.TotalBookings = vm.Details.Count;
            vm.TotalRevenue = vm.Details.Sum(d => d.TotalPaid);
            vm.CompletedBookings = vm.Details.Count(d => d.BookingStatus == "Completed");
            vm.PendingBookings = vm.Details.Count(d => d.BookingStatus == "Pending");
            vm.CancelledBookings = vm.Details.Count(d => d.BookingStatus == "Cancelled");

            // Dropdowns
            vm.AvailablePaymentMethods = new List<string> { "All", "Credit Card", "Bank Transfer", "Touch 'n Go", "Cash" };
            vm.AvailableBookingStatuses = new List<string> { "All", "Completed", "Pending", "Cancelled" };

            return vm;
        }

        // ==========================================
        // 3. PACKAGE PERFORMANCE REPORT (VIEW)
        // ==========================================
        [HttpGet("PackagePerformanceReport")]
        public async Task<IActionResult> PackagePerformanceReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string categoryFilter = "All",
            [FromQuery] int? packageFilter = null)
        {
            var vm = await BuildPackagePerformanceVM(startDate, endDate, categoryFilter, packageFilter);
            return View(vm);
        }

        // ==========================================
        // 4. PACKAGE PERFORMANCE REPORT (EXCEL)
        // ==========================================
        [HttpGet("ExportPackageExcel")]
        public async Task<IActionResult> ExportPackageExcel(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string categoryFilter = "All",
            [FromQuery] int? packageFilter = null)
        {
            var vm = await BuildPackagePerformanceVM(startDate, endDate, categoryFilter, packageFilter);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Package Performance");

                // Header
                worksheet.Cell(1, 1).Value = "FlyEase Package Performance Report";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Cell(2, 1).Value = $"Generated: {DateTime.Now}";
                worksheet.Cell(3, 1).Value = $"Period: {vm.StartDate:dd/MM/yyyy} - {vm.EndDate:dd/MM/yyyy}";

                // Column Headers
                int row = 5;
                worksheet.Cell(row, 1).Value = "Package Name";
                worksheet.Cell(row, 2).Value = "Category";
                worksheet.Cell(row, 3).Value = "Destination";
                worksheet.Cell(row, 4).Value = "Price (RM)";
                worksheet.Cell(row, 5).Value = "Bookings";
                worksheet.Cell(row, 6).Value = "Pax Sold";
                worksheet.Cell(row, 7).Value = "Revenue (RM)";
                worksheet.Cell(row, 8).Value = "Rating";
                worksheet.Cell(row, 9).Value = "Occupancy %";

                var headerRange = worksheet.Range(row, 1, row, 9);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Data Rows
                row++;
                foreach (var item in vm.Details)
                {
                    worksheet.Cell(row, 1).Value = item.PackageName;
                    worksheet.Cell(row, 2).Value = item.Category;
                    worksheet.Cell(row, 3).Value = item.Destination;
                    worksheet.Cell(row, 4).Value = item.Price;
                    worksheet.Cell(row, 5).Value = item.TotalBookings;
                    worksheet.Cell(row, 6).Value = item.TotalPeople;
                    worksheet.Cell(row, 7).Value = item.TotalRevenue;
                    worksheet.Cell(row, 8).Value = item.AverageRating > 0 ? item.AverageRating.ToString("N1") : "-";
                    worksheet.Cell(row, 9).Value = (double)item.OccupancyRate / 100.0; // Store as decimal for % formatting
                    worksheet.Cell(row, 9).Style.NumberFormat.Format = "0.0%";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PackageReport_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        // Shared Logic Builder
        private async Task<PackagePerformanceReportVM> BuildPackagePerformanceVM(DateTime? startDate, DateTime? endDate, string categoryFilter, int? packageFilter)
        {
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? DateTime.Now.AddDays(-30);

            var vm = new PackagePerformanceReportVM
            {
                StartDate = start,
                EndDate = end,
                CategoryFilter = categoryFilter,
                PackageFilter = packageFilter,
                GeneratedAt = DateTime.Now,
                GeneratedBy = User.Identity?.Name ?? "Admin"
            };

            // Query Construction
            var packagesQuery = _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings)
                .ThenInclude(b => b.Feedbacks)
                .AsQueryable();

            if (categoryFilter != "All" && !string.IsNullOrEmpty(categoryFilter))
                packagesQuery = packagesQuery.Where(p => p.Category.CategoryName == categoryFilter);

            if (packageFilter.HasValue && packageFilter.Value > 0)
                packagesQuery = packagesQuery.Where(p => p.PackageID == packageFilter.Value);

            var packages = await packagesQuery.ToListAsync();

            // Build Details
            foreach (var package in packages)
            {
                // Filter bookings by date range
                var relevantBookings = package.Bookings
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToList();

                var totalRevenue = relevantBookings.Sum(b => b.FinalAmount);
                var totalPeople = relevantBookings.Sum(b => b.NumberOfPeople);
                var feedbacks = relevantBookings.SelectMany(b => b.Feedbacks).ToList();

                var estimatedTotalCapacity = package.AvailableSlots + totalPeople;
                var occupancy = estimatedTotalCapacity > 0 ? (decimal)totalPeople / estimatedTotalCapacity * 100 : 0;

                vm.Details.Add(new PackagePerformanceDetailVM
                {
                    PackageID = package.PackageID,
                    PackageName = package.PackageName,
                    Destination = package.Destination,
                    Category = package.Category?.CategoryName ?? "Uncategorized",
                    Price = package.Price,
                    TotalBookings = relevantBookings.Count,
                    TotalPeople = totalPeople,
                    TotalRevenue = totalRevenue,
                    AverageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0,
                    TotalReviews = feedbacks.Count,
                    AvailableSlots = package.AvailableSlots,
                    OccupancyRate = occupancy
                });
            }

            // Summaries
            vm.TotalPackages = vm.Details.Count;
            vm.TotalBookings = vm.Details.Sum(d => d.TotalBookings);
            vm.TotalRevenue = vm.Details.Sum(d => d.TotalRevenue);
            vm.AverageRating = vm.Details.Any(d => d.TotalReviews > 0)
                ? (decimal)vm.Details.Where(d => d.TotalReviews > 0).Average(d => d.AverageRating)
                : 0;

            // [NEW] POPULATE CHART DATA (Top 10 Packages by Revenue)
            var topPackages = vm.Details.OrderByDescending(d => d.TotalRevenue).Take(10).ToList();
            vm.ChartLabels = topPackages.Select(d => d.PackageName).ToList();
            vm.ChartValues = topPackages.Select(d => d.TotalRevenue).ToList();

            // Dropdowns
            vm.AvailableCategories = new List<string> { "All" };
            vm.AvailableCategories.AddRange(await _context.PackageCategories.Select(c => c.CategoryName).Distinct().ToListAsync());

            vm.AvailablePackages = await _context.Packages
                .Select(p => new PackageSelectOption { Id = p.PackageID, Name = p.PackageName })
                .OrderBy(p => p.Name)
                .ToListAsync();

            return vm;
        }


        // ==========================================
        // 5. CUSTOMER INSIGHT REPORT (VIEW)
        // ==========================================
        [HttpGet("CustomerInsightReport")]
        public async Task<IActionResult> CustomerInsightReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string sortBy = "Spending")
        {
            var vm = await BuildCustomerInsightVM(startDate, endDate, sortBy);
            return View(vm);
        }

        // ==========================================
        // 6. CUSTOMER INSIGHT REPORT (EXCEL)
        // ==========================================
        [HttpGet("ExportCustomerExcel")]
        public async Task<IActionResult> ExportCustomerExcel(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string sortBy = "Spending")
        {
            var vm = await BuildCustomerInsightVM(startDate, endDate, sortBy);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Customer Insights");

                // Header
                worksheet.Cell(1, 1).Value = "FlyEase Customer Ledger";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Cell(2, 1).Value = $"Generated: {DateTime.Now}";
                worksheet.Cell(3, 1).Value = $"Period: {vm.StartDate:dd/MM/yyyy} - {vm.EndDate:dd/MM/yyyy}";

                // Column Headers
                int row = 5;
                worksheet.Cell(row, 1).Value = "Customer Name";
                worksheet.Cell(row, 2).Value = "Email";
                worksheet.Cell(row, 3).Value = "Phone";
                worksheet.Cell(row, 4).Value = "Tier";
                worksheet.Cell(row, 5).Value = "Total Bookings";
                worksheet.Cell(row, 6).Value = "Total Spent (RM)";
                worksheet.Cell(row, 7).Value = "Last Booking";
                worksheet.Cell(row, 8).Value = "Avg Rating";

                var headerRange = worksheet.Range(row, 1, row, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Data Rows
                row++;
                foreach (var item in vm.Details)
                {
                    worksheet.Cell(row, 1).Value = item.CustomerName;
                    worksheet.Cell(row, 2).Value = item.Email;
                    worksheet.Cell(row, 3).Value = item.Phone;
                    worksheet.Cell(row, 4).Value = item.CustomerTier;
                    worksheet.Cell(row, 5).Value = item.TotalBookings;
                    worksheet.Cell(row, 6).Value = item.TotalSpent;
                    worksheet.Cell(row, 7).Value = item.LastBookingDate;
                    worksheet.Cell(row, 8).Value = item.AverageRating > 0 ? item.AverageRating.ToString("N1") : "-";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"CustomerReport_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        // Shared Logic Builder
        private async Task<CustomerInsightReportVM> BuildCustomerInsightVM(DateTime? startDate, DateTime? endDate, string sortBy)
        {
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? DateTime.Now.AddDays(-90);

            var vm = new CustomerInsightReportVM
            {
                StartDate = start,
                EndDate = end,
                SortBy = sortBy,
                GeneratedAt = DateTime.Now,
                GeneratedBy = User.Identity?.Name ?? "Admin"
            };

            // Fetch Customers who have bookings in the range
            var customersQuery = _context.Users
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Payments)
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Feedbacks)
                .Where(u => u.Bookings.Any(b => b.BookingDate >= start && b.BookingDate <= end))
                .AsQueryable();

            var customers = await customersQuery.ToListAsync();

            foreach (var customer in customers)
            {
                var relevantBookings = customer.Bookings
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToList();

                if (!relevantBookings.Any()) continue;

                var totalSpent = relevantBookings.Sum(b => b.Payments
                    .Where(p => p.PaymentStatus == "Completed")
                    .Sum(p => p.AmountPaid));

                var feedbacks = relevantBookings.SelectMany(b => b.Feedbacks).ToList();

                // Determine Tier based on SPENDING in this period
                string tier = totalSpent switch
                {
                    >= 10000 => "VIP",
                    >= 5000 => "Premium",
                    >= 2000 => "Standard",
                    _ => "New"
                };

                vm.Details.Add(new CustomerInsightDetailVM
                {
                    UserID = customer.UserID,
                    CustomerName = customer.FullName,
                    Email = customer.Email,
                    Phone = customer.Phone ?? "N/A",
                    TotalBookings = relevantBookings.Count,
                    TotalPeople = relevantBookings.Sum(b => b.NumberOfPeople),
                    TotalSpent = totalSpent,
                    AverageSpentPerBooking = relevantBookings.Count > 0 ? totalSpent / relevantBookings.Count : 0,
                    FirstBookingDate = relevantBookings.Min(b => b.BookingDate),
                    LastBookingDate = relevantBookings.Max(b => b.BookingDate),
                    AverageRating = feedbacks.Count > 0 ? (decimal)feedbacks.Average(f => f.Rating) : 0,
                    ReviewsGiven = feedbacks.Count,
                    CustomerTier = tier
                });
            }

            // Summaries
            vm.TotalCustomers = vm.Details.Count;
            vm.TotalBookings = vm.Details.Sum(d => d.TotalBookings);
            vm.TotalRevenue = vm.Details.Sum(d => d.TotalSpent);
            vm.AverageSpendingPerCustomer = vm.TotalCustomers > 0 ? vm.TotalRevenue / vm.TotalCustomers : 0;
            vm.ActiveCustomers = vm.Details.Count(d => d.LastBookingDate >= DateTime.Now.AddDays(-30));

            // Sorting
            if (sortBy == "Frequency")
                vm.Details = vm.Details.OrderByDescending(d => d.TotalBookings).ThenByDescending(d => d.TotalSpent).ToList();
            else // Default: Spending
                vm.Details = vm.Details.OrderByDescending(d => d.TotalSpent).ThenByDescending(d => d.TotalBookings).ToList();

            // CHART DATA: Top 5 Customers by Spending
            var topCustomers = vm.Details.Take(5).ToList();
            vm.ChartLabels = topCustomers.Select(c => c.CustomerName).ToList();
            vm.ChartValues = topCustomers.Select(c => c.TotalSpent).ToList();

            return vm;
        }
    }
}