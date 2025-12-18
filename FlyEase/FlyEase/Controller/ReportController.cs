using ClosedXML.Excel;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace FlyEase.Controllers
{
    [Route("Report")]
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public ReportController(FlyEaseDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. DASHBOARD VIEW (Interactive)
        // ==========================================
        [HttpGet("SalesReport")]
        public async Task<IActionResult> SalesReport(
            [FromQuery] string viewMode = "monthly",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string search = "",
            [FromQuery] int? packageId = null,
            [FromQuery] int page = 1)
        {
            // 1. Logic Reuse: Get Query & Dates
            var (query, start, end) = BuildSalesQuery(viewMode, startDate, endDate, search, packageId);

            // 2. Calculate Totals (Before Pagination)
            var grandTotalPax = await query.SumAsync(b => b.NumberOfPeople);
            var grandTotalRevenue = await query.SumAsync(b => b.FinalAmount);

            // 3. Pagination Logic
            int pageSize = 10;
            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            page = Math.Max(1, Math.Min(page, totalPages == 0 ? 1 : totalPages));

            // 4. Fetch Data (Paged)
            var bookings = await query
                .OrderByDescending(b => b.BookingDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new SalesReportDetailVM
                {
                    BookingID = b.BookingID,
                    BookingDate = b.BookingDate,
                    CustomerName = b.User.FullName,
                    PackageName = b.Package.PackageName,
                    Pax = b.NumberOfPeople,
                    Amount = b.FinalAmount,
                    PaymentStatus = b.BookingStatus
                })
                .ToListAsync();

            // 5. Populate VM
            var vm = new SalesReportVM
            {
                ViewMode = viewMode,
                StartDate = start,
                EndDate = end,
                SearchTerm = search,
                SelectedPackageID = packageId,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                GrandTotalPax = grandTotalPax,
                GrandTotalRevenue = grandTotalRevenue,
                Details = bookings,
                GeneratedBy = User.Identity?.Name ?? "Admin",
                AvailablePackages = await GetPackagesList()
            };

            return View(vm);
        }

        // ==========================================
        // 2. PRINTED VIEW (Static + Chart)
        // ==========================================
        [HttpGet("SalesReportPrinted")]
        public async Task<IActionResult> SalesReportPrinted(
            [FromQuery] string viewMode = "monthly",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string search = "",
            [FromQuery] int? packageId = null)
        {
            // 1. Get Base Query (Same as Dashboard)
            var (query, start, end) = BuildSalesQuery(viewMode, startDate, endDate, search, packageId);

            // 2. Fetch ALL Data (No Pagination for Print)
            var bookings = await query
                .OrderByDescending(b => b.BookingDate)
                .Select(b => new SalesReportDetailVM
                {
                    BookingID = b.BookingID,
                    BookingDate = b.BookingDate,
                    CustomerName = b.User.FullName,
                    PackageName = b.Package.PackageName,
                    Pax = b.NumberOfPeople,
                    Amount = b.FinalAmount
                })
                .ToListAsync();

            // 3. Prepare Chart Data (Group by Package)
            var chartData = bookings
                .GroupBy(b => b.PackageName)
                .Select(g => new {
                    Name = g.Key,
                    Revenue = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10) // Top 10 for chart
                .ToList();

            var vm = new SalesReportVM
            {
                ViewMode = viewMode,
                StartDate = start,
                EndDate = end,
                SearchTerm = search,
                SelectedPackageID = packageId,
                GrandTotalOrders = bookings.Count,
                GrandTotalPax = bookings.Sum(b => b.Pax),
                GrandTotalRevenue = bookings.Sum(b => b.Amount),
                Details = bookings,
                GeneratedBy = User.Identity?.Name ?? "Admin",

                // Chart Data
                ChartLabels = chartData.Select(x => x.Name).ToList(),
                ChartValues = chartData.Select(x => x.Revenue).ToList()
            };

            return View("SalesReportPrinted", vm);
        }

        // ==========================================
        // HELPER METHODS (To avoid code duplication)
        // ==========================================
        private (IQueryable<Booking> query, DateTime start, DateTime end) BuildSalesQuery(
            string viewMode, DateTime? startDate, DateTime? endDate, string search, int? packageId)
        {
            DateTime start, end;
            DateTime now = DateTime.Now;

            // Date Logic
            switch (viewMode?.ToLower())
            {
                case "daily":
                    start = now.Date;
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "monthly":
                    start = new DateTime(now.Year, now.Month, 1);
                    end = start.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
                    break;
                case "custom":
                default:
                    start = startDate ?? new DateTime(now.Year, now.Month, 1);
                    end = (endDate ?? now).Date.AddDays(1).AddTicks(-1);
                    break;
            }

            // Query Construction
            var query = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Package)
                .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                .AsQueryable();

            // Filters
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(b => b.User.FullName.Contains(search) || b.Package.PackageName.Contains(search));
            }

            if (packageId.HasValue && packageId.Value > 0)
            {
                query = query.Where(b => b.PackageID == packageId.Value);
            }

            return (query, start, end);
        }

        private async Task<List<PackageSelectOption>> GetPackagesList()
        {
            return await _context.Packages
               .Select(p => new PackageSelectOption 
               { 
                   Id = p.PackageID, 
                   Name = p.PackageName 
               })
               .OrderBy(p => p.Name)
               .ToListAsync();
        }

        // [Excel Export Method remains mostly the same, reuse BuildSalesQuery logic if needed]
        [HttpGet("ExportSalesExcel")]
        public async Task<IActionResult> ExportSalesExcel(
            string viewMode = "custom",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string search = "",
            int? packageId = null)
        {
            // (Reuse logic from SalesReport but without pagination)
            // ... [Identical Logic for Start/End/Query/Filters as above] ...
            // NOTE: For brevity, repeating the date logic essentially here:
            DateTime start, end;
            DateTime now = DateTime.Now;
            switch (viewMode.ToLower())
            {
                case "daily": start = now.Date; end = now.Date.AddDays(1).AddTicks(-1); break;
                case "monthly": start = new DateTime(now.Year, now.Month, 1); end = start.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59); break;
                default: start = startDate ?? new DateTime(now.Year, now.Month, 1); end = (endDate ?? now).Date.AddDays(1).AddTicks(-1); break;
            }

            var query = _context.Bookings.Include(b => b.User).Include(b => b.Package).Where(b => b.BookingDate >= start && b.BookingDate <= end).AsQueryable();
            if (!string.IsNullOrEmpty(search)) query = query.Where(b => b.User.FullName.Contains(search) || b.Package.PackageName.Contains(search));
            if (packageId.HasValue && packageId.Value > 0) query = query.Where(b => b.PackageID == packageId.Value);

            var data = await query.OrderByDescending(b => b.BookingDate).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Vendor Report");

                // Headers matching PHP style
                worksheet.Cell(1, 1).Value = "Date";
                worksheet.Cell(1, 2).Value = "Order ID";
                worksheet.Cell(1, 3).Value = "Customer";
                worksheet.Cell(1, 4).Value = "Product";
                worksheet.Cell(1, 5).Value = "Qty";
                worksheet.Cell(1, 6).Value = "Subtotal";

                var header = worksheet.Range(1, 1, 1, 6);
                header.Style.Font.Bold = true;

                int row = 2;
                foreach (var item in data)
                {
                    worksheet.Cell(row, 1).Value = item.BookingDate;
                    worksheet.Cell(row, 2).Value = "#" + item.BookingID;
                    worksheet.Cell(row, 3).Value = item.User.FullName;
                    worksheet.Cell(row, 4).Value = item.Package.PackageName;
                    worksheet.Cell(row, 5).Value = item.NumberOfPeople;
                    worksheet.Cell(row, 6).Value = item.FinalAmount;
                    row++;
                }

                // Grand Total Row
                worksheet.Cell(row, 4).Value = "GRAND TOTAL";
                worksheet.Cell(row, 4).Style.Font.Bold = true;
                worksheet.Cell(row, 5).Value = data.Sum(x => x.NumberOfPeople);
                worksheet.Cell(row, 5).Style.Font.Bold = true;
                worksheet.Cell(row, 6).Value = data.Sum(x => x.FinalAmount);
                worksheet.Cell(row, 6).Style.Font.Bold = true;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "DetailedReport.xlsx");
                }
            }
        }

        // ==========================================
        // 3. PACKAGE PERFORMANCE REPORT (Dashboard)
        // ==========================================
        [HttpGet("PackagePerformanceReport")]
        public async Task<IActionResult> PackagePerformanceReport(
            [FromQuery] string viewMode = "monthly",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string category = "All",
            [FromQuery] int? packageId = null,
            [FromQuery] string sortBy = "Revenue",
            [FromQuery] int page = 1)
        {
            var (start, end) = GetDateRange(viewMode, startDate, endDate);

            // 1. Build Query
            var query = _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings)
                .AsQueryable();

            if (category != "All" && !string.IsNullOrEmpty(category))
                query = query.Where(p => p.Category.CategoryName == category);

            if (packageId.HasValue && packageId.Value > 0)
                query = query.Where(p => p.PackageID == packageId.Value);

            // 2. Fetch & Aggregate (In-Memory)
            var packages = await query.ToListAsync();

            var reportData = packages.Select(p => {
                var relevantBookings = p.Bookings
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToList();

                return new PackagePerformanceDetailVM
                {
                    PackageID = p.PackageID,
                    PackageName = p.PackageName,
                    Category = p.Category.CategoryName,
                    Price = p.Price,
                    TotalBookings = relevantBookings.Count,
                    TotalPax = relevantBookings.Sum(b => b.NumberOfPeople),
                    Revenue = relevantBookings.Sum(b => b.FinalAmount),
                    Rating = p.AverageRating
                };
            })
            .Where(x => x.TotalBookings > 0)
            .ToList();

            // 3. Apply Sorting
            if (sortBy == "Volume")
                reportData = reportData.OrderByDescending(x => x.TotalPax).ThenByDescending(x => x.Revenue).ToList();
            else
                reportData = reportData.OrderByDescending(x => x.Revenue).ThenByDescending(x => x.TotalPax).ToList();

            // 4. Pagination
            var totalRevenue = reportData.Sum(x => x.Revenue);
            var totalPax = reportData.Sum(x => x.TotalPax);

            int pageSize = 10;
            int totalPages = (int)Math.Ceiling((double)reportData.Count / pageSize);
            page = Math.Max(1, Math.Min(page, totalPages == 0 ? 1 : totalPages));

            var pagedData = reportData.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // 5. Prepare VM
            var vm = new PackagePerformanceReportVM
            {
                ViewMode = viewMode,
                StartDate = start,
                EndDate = end,
                CategoryFilter = category,
                PackageFilter = packageId,
                SortBy = sortBy,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                TotalRevenue = totalRevenue,
                TotalPackagesSold = totalPax,
                Details = pagedData,
                GeneratedBy = User.Identity?.Name ?? "Admin",
                AvailableCategories = await _context.PackageCategories.Select(c => c.CategoryName).ToListAsync(),
                AvailablePackages = await _context.Packages
                    .Select(p => new PackageFilterOption { Id = p.PackageID, Name = p.PackageName })
                    .OrderBy(p => p.Name)
                    .ToListAsync()
            };

            if (!vm.AvailableCategories.Contains("All")) vm.AvailableCategories.Insert(0, "All");

            return View(vm);
        }

        // ==========================================
        // 4. PRINTED REPORT (Dual Charts)
        // ==========================================
        [HttpGet("PackagePerformancePrinted")]
        public async Task<IActionResult> PackagePerformancePrinted(
            string viewMode = "monthly",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string category = "All",
            int? packageId = null,
            string sortBy = "Revenue")
        {
            var (start, end) = GetDateRange(viewMode, startDate, endDate);

            var query = _context.Packages.Include(p => p.Category).Include(p => p.Bookings).AsQueryable();
            if (category != "All" && !string.IsNullOrEmpty(category)) query = query.Where(p => p.Category.CategoryName == category);
            if (packageId.HasValue && packageId > 0) query = query.Where(p => p.PackageID == packageId);

            var packages = await query.ToListAsync();
            var reportData = packages.Select(p => {
                var bookings = p.Bookings.Where(b => b.BookingDate >= start && b.BookingDate <= end).ToList();
                return new PackagePerformanceDetailVM
                {
                    PackageID = p.PackageID,
                    PackageName = p.PackageName,
                    Category = p.Category.CategoryName,
                    TotalPax = bookings.Sum(b => b.NumberOfPeople),
                    Revenue = bookings.Sum(b => b.FinalAmount)
                };
            }).Where(x => x.TotalPax > 0).ToList();

            // Calculate "Most Sold" 
            var mostSold = reportData.OrderByDescending(x => x.TotalPax).FirstOrDefault();

            // 1. Prepare Data for REVENUE Chart (Top 5 by $)
            var topRevenue = reportData.OrderByDescending(x => x.Revenue).Take(5).ToList();

            // 2. Prepare Data for PAX Chart (Top 5 by Qty)
            var topPax = reportData.OrderByDescending(x => x.TotalPax).Take(5).ToList();

            // Apply Sorting to the List
            if (sortBy == "Volume")
                reportData = reportData.OrderByDescending(x => x.TotalPax).ToList();
            else
                reportData = reportData.OrderByDescending(x => x.Revenue).ToList();

            var vm = new PackagePerformanceReportVM
            {
                ViewMode = viewMode,
                StartDate = start,
                EndDate = end,
                GeneratedBy = User.Identity?.Name ?? "Admin",
                SortBy = sortBy,

                // Summaries
                TotalRevenue = reportData.Sum(x => x.Revenue),
                TotalPackagesSold = reportData.Sum(x => x.TotalPax),
                TopPackageName = mostSold?.PackageName ?? "None",
                TopPackageCount = mostSold?.TotalPax ?? 0,

                Details = reportData,

                // Chart 1: Revenue
                RevenueChartLabels = topRevenue.Select(x => x.PackageName).ToList(),
                RevenueChartValues = topRevenue.Select(x => x.Revenue).ToList(),

                // Chart 2: Pax
                PaxChartLabels = topPax.Select(x => x.PackageName).ToList(),
                PaxChartValues = topPax.Select(x => x.TotalPax).ToList()
            };

            return View("PackagePerformancePrinted", vm);
        }

        // ==========================================
        // 5. EXCEL EXPORT (Full Implementation)
        // ==========================================
        [HttpGet("ExportPackageExcel")]
        public async Task<IActionResult> ExportPackageExcel(
            string viewMode = "monthly",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string category = "All",
            int? packageId = null,
            string sortBy = "Revenue")
        {
            var (start, end) = GetDateRange(viewMode, startDate, endDate);

            var query = _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings)
                .AsQueryable();

            if (category != "All" && !string.IsNullOrEmpty(category))
                query = query.Where(p => p.Category.CategoryName == category);

            if (packageId.HasValue && packageId.Value > 0)
                query = query.Where(p => p.PackageID == packageId.Value);

            var packages = await query.ToListAsync();

            var reportData = packages.Select(p => {
                var relevantBookings = p.Bookings
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToList();

                return new PackagePerformanceDetailVM
                {
                    PackageID = p.PackageID,
                    PackageName = p.PackageName,
                    Category = p.Category.CategoryName,
                    Price = p.Price,
                    TotalBookings = relevantBookings.Count,
                    TotalPax = relevantBookings.Sum(b => b.NumberOfPeople),
                    Revenue = relevantBookings.Sum(b => b.FinalAmount)
                };
            })
            .Where(x => x.TotalBookings > 0)
            .ToList();

            // Apply Sorting
            if (sortBy == "Volume")
                reportData = reportData.OrderByDescending(x => x.TotalPax).ThenByDescending(x => x.Revenue).ToList();
            else
                reportData = reportData.OrderByDescending(x => x.Revenue).ThenByDescending(x => x.TotalPax).ToList();

            // Generate Excel
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Package Performance");

                // Title and Meta
                worksheet.Cell(1, 1).Value = "FlyEase Package Performance Report";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;

                worksheet.Cell(2, 1).Value = $"Period: {start:dd/MM/yyyy} - {end:dd/MM/yyyy}";
                worksheet.Cell(3, 1).Value = $"Generated By: {User.Identity?.Name ?? "Admin"}";
                worksheet.Cell(4, 1).Value = $"Sort Order: {(sortBy == "Volume" ? "Most Sold" : "Highest Revenue")}";

                // Headers
                int row = 6;
                worksheet.Cell(row, 1).Value = "Rank";
                worksheet.Cell(row, 2).Value = "Package Name";
                worksheet.Cell(row, 3).Value = "Category";
                worksheet.Cell(row, 4).Value = "Price (RM)";
                worksheet.Cell(row, 5).Value = "Bookings";
                worksheet.Cell(row, 6).Value = "Pax Sold";
                worksheet.Cell(row, 7).Value = "Revenue (RM)";

                var headerRange = worksheet.Range(row, 1, row, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Data Rows
                row++;
                int rank = 1;
                foreach (var item in reportData)
                {
                    worksheet.Cell(row, 1).Value = rank;
                    worksheet.Cell(row, 2).Value = item.PackageName;
                    worksheet.Cell(row, 3).Value = item.Category;
                    worksheet.Cell(row, 4).Value = item.Price;
                    worksheet.Cell(row, 5).Value = item.TotalBookings;
                    worksheet.Cell(row, 6).Value = item.TotalPax;
                    worksheet.Cell(row, 7).Value = item.Revenue;

                    // Formatting
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";

                    row++;
                    rank++;
                }

                // Footer Row
                worksheet.Cell(row, 5).Value = "GRAND TOTAL";
                worksheet.Cell(row, 5).Style.Font.Bold = true;

                worksheet.Cell(row, 6).Value = reportData.Sum(x => x.TotalPax);
                worksheet.Cell(row, 6).Style.Font.Bold = true;

                worksheet.Cell(row, 7).Value = reportData.Sum(x => x.Revenue);
                worksheet.Cell(row, 7).Style.Font.Bold = true;
                worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PackagePerformance_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        // ==========================================
        // 5. CUSTOMER INSIGHT REPORT (Dashboard)
        // ==========================================
        [HttpGet("CustomerInsightReport")]
        public async Task<IActionResult> CustomerInsightReport(
            [FromQuery] string viewMode = "monthly",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string search = "",
            [FromQuery] string sortBy = "Spending",
            [FromQuery] int page = 1)
        {
            var (start, end) = GetDateRange(viewMode, startDate, endDate);

            // 1. Base Query: Users with Bookings in range
            var query = _context.Users
                .Include(u => u.Bookings)
                .ThenInclude(b => b.Feedbacks)
                .Where(u => u.Role == "User" || u.Role == "Customer") // Ensure we only get customers
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
            }

            // 2. Fetch & Aggregate (In-Memory because complex sum/count on filtered child collection is tricky in EF)
            var users = await query.ToListAsync();

            var reportData = users.Select(u => {
                var relevantBookings = u.Bookings
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToList();

                // Skip users with no bookings in this period? Usually yes for reports.
                if (!relevantBookings.Any()) return null;

                var feedbacks = relevantBookings.SelectMany(b => b.Feedbacks).ToList();

                return new CustomerInsightDetailVM
                {
                    UserID = u.UserID,
                    CustomerName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone ?? "-",
                    TotalBookings = relevantBookings.Count,
                    TotalSpent = relevantBookings.Sum(b => b.FinalAmount),
                    LastBookingDate = relevantBookings.Max(b => b.BookingDate),
                    AverageRating = feedbacks.Any() ? (decimal)feedbacks.Average(f => f.Rating) : 0
                };
            })
            .Where(x => x != null)
            .ToList();

            // 3. Apply Sorting
            if (sortBy == "Frequency")
                reportData = reportData.OrderByDescending(x => x.TotalBookings).ThenByDescending(x => x.TotalSpent).ToList();
            else // Default: Spending
                reportData = reportData.OrderByDescending(x => x.TotalSpent).ThenByDescending(x => x.TotalBookings).ToList();

            // 4. Pagination
            var totalRevenue = reportData.Sum(x => x.TotalSpent);
            var totalBookings = reportData.Sum(x => x.TotalBookings);

            int pageSize = 10;
            int totalRecords = reportData.Count;
            int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            page = Math.Max(1, Math.Min(page, totalPages == 0 ? 1 : totalPages));

            var pagedData = reportData.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // 5. VM
            var vm = new CustomerInsightReportVM
            {
                ViewMode = viewMode,
                StartDate = start,
                EndDate = end,
                SearchTerm = search,
                SortBy = sortBy,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                TotalCustomers = totalRecords,
                TotalBookings = totalBookings,
                TotalRevenue = totalRevenue,
                AverageSpendPerCustomer = totalRecords > 0 ? totalRevenue / totalRecords : 0,
                Details = pagedData,
                GeneratedBy = User.Identity?.Name ?? "Admin"
            };

            return View(vm);
        }

        // ==========================================
        // 6. CUSTOMER PRINTED REPORT (Dual Charts)
        // ==========================================
        [HttpGet("CustomerInsightPrinted")]
        public async Task<IActionResult> CustomerInsightPrinted(
            string viewMode = "monthly",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string search = "",
            string sortBy = "Spending")
        {
            var (start, end) = GetDateRange(viewMode, startDate, endDate);

            var query = _context.Users
                .Include(u => u.Bookings)
                .Where(u => u.Role == "User" || u.Role == "Customer")
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));

            var users = await query.ToListAsync();

            var reportData = users.Select(u => {
                var bookings = u.Bookings.Where(b => b.BookingDate >= start && b.BookingDate <= end).ToList();
                if (!bookings.Any()) return null;

                return new CustomerInsightDetailVM
                {
                    UserID = u.UserID,
                    CustomerName = u.FullName,
                    Email = u.Email,
                    TotalBookings = bookings.Count,
                    TotalSpent = bookings.Sum(b => b.FinalAmount)
                };
            })
            .Where(x => x != null)
            .ToList();

            // Top Customer Stats
            var topCustomer = reportData.OrderByDescending(x => x.TotalSpent).FirstOrDefault();

            // Chart Data 1: Top 5 Spenders
            var topSpenders = reportData.OrderByDescending(x => x.TotalSpent).Take(5).ToList();

            // Chart Data 2: Top 5 Frequent
            var topFrequent = reportData.OrderByDescending(x => x.TotalBookings).Take(5).ToList();

            // Sort List for Table
            if (sortBy == "Frequency")
                reportData = reportData.OrderByDescending(x => x.TotalBookings).ToList();
            else
                reportData = reportData.OrderByDescending(x => x.TotalSpent).ToList();

            var vm = new CustomerInsightReportVM
            {
                ViewMode = viewMode,
                StartDate = start,
                EndDate = end,
                GeneratedBy = User.Identity?.Name ?? "Admin",
                SortBy = sortBy,

                TotalCustomers = reportData.Count,
                TotalRevenue = reportData.Sum(x => x.TotalSpent),
                TotalBookings = reportData.Sum(x => x.TotalBookings),
                TopCustomerName = topCustomer?.CustomerName ?? "None",
                TopCustomerValue = topCustomer?.TotalSpent ?? 0,

                Details = reportData,

                // Charts
                SpendingChartLabels = topSpenders.Select(x => x.CustomerName).ToList(),
                SpendingChartValues = topSpenders.Select(x => x.TotalSpent).ToList(),

                FrequencyChartLabels = topFrequent.Select(x => x.CustomerName).ToList(),
                FrequencyChartValues = topFrequent.Select(x => x.TotalBookings).ToList()
            };

            return View("CustomerInsightPrinted", vm);
        }

        // ==========================================
        // 7. CUSTOMER EXCEL EXPORT
        // ==========================================
        [HttpGet("ExportCustomerExcel")]
        public async Task<IActionResult> ExportCustomerExcel(
            string viewMode = "monthly",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string search = "",
            string sortBy = "Spending")
        {
            var (start, end) = GetDateRange(viewMode, startDate, endDate);

            var query = _context.Users
               .Include(u => u.Bookings)
               .Where(u => u.Role == "User" || u.Role == "Customer")
               .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.FullName.Contains(search));

            var users = await query.ToListAsync();

            var reportData = users.Select(u => {
                var bookings = u.Bookings.Where(b => b.BookingDate >= start && b.BookingDate <= end).ToList();
                if (!bookings.Any()) return null;
                return new CustomerInsightDetailVM
                {
                    CustomerName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    TotalBookings = bookings.Count,
                    TotalSpent = bookings.Sum(b => b.FinalAmount),
                    LastBookingDate = bookings.Max(b => b.BookingDate)
                };
            }).Where(x => x != null).ToList();

            if (sortBy == "Frequency")
                reportData = reportData.OrderByDescending(x => x.TotalBookings).ToList();
            else
                reportData = reportData.OrderByDescending(x => x.TotalSpent).ToList();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Customer Insights");

                worksheet.Cell(1, 1).Value = "FlyEase Customer Insight Report";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Cell(2, 1).Value = $"Period: {start:dd/MM/yyyy} - {end:dd/MM/yyyy}";

                int row = 5;
                worksheet.Cell(row, 1).Value = "Rank";
                worksheet.Cell(row, 2).Value = "Customer Name";
                worksheet.Cell(row, 3).Value = "Email";
                worksheet.Cell(row, 4).Value = "Phone";
                worksheet.Cell(row, 5).Value = "Total Bookings";
                worksheet.Cell(row, 6).Value = "Total Spent (RM)";
                worksheet.Cell(row, 7).Value = "Last Booking";

                var header = worksheet.Range(row, 1, row, 7);
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.LightGray;

                row++;
                int rank = 1;
                foreach (var item in reportData)
                {
                    worksheet.Cell(row, 1).Value = rank++;
                    worksheet.Cell(row, 2).Value = item.CustomerName;
                    worksheet.Cell(row, 3).Value = item.Email;
                    worksheet.Cell(row, 4).Value = item.Phone;
                    worksheet.Cell(row, 5).Value = item.TotalBookings;
                    worksheet.Cell(row, 6).Value = item.TotalSpent;
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(row, 7).Value = item.LastBookingDate;
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

        // Helper Method
        private (DateTime start, DateTime end) GetDateRange(string viewMode, DateTime? startDate, DateTime? endDate)
        {
            DateTime now = DateTime.Now;
            if (viewMode == "daily") return (now.Date, now.Date.AddDays(1).AddTicks(-1));
            if (viewMode == "monthly")
            {
                var s = new DateTime(now.Year, now.Month, 1);
                return (s, s.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59));
            }
            return (startDate ?? new DateTime(now.Year, now.Month, 1), (endDate ?? now).Date.AddDays(1).AddTicks(-1));
        }



    }
}