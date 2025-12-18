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

    }
}