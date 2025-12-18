using System;
using System.Collections.Generic;

namespace FlyEase.ViewModels
{
    public class SalesReportVM
    {
        // ========== FILTER PARAMETERS ==========
        public string ViewMode { get; set; } = "custom"; // daily, monthly, custom
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string SearchTerm { get; set; }
        public int? SelectedPackageID { get; set; }

        // ========== PAGINATION ==========
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        // ========== SUMMARY STATS (Header/Footer) ==========
        public int GrandTotalPax { get; set; }
        public decimal GrandTotalRevenue { get; set; }
        public int GrandTotalOrders { get; set; } // Added for Print View Summary Card

        // ========== CHART DATA (For Printed Report) ==========
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartValues { get; set; } = new List<decimal>();

        // ========== DATA ==========
        public List<SalesReportDetailVM> Details { get; set; } = new List<SalesReportDetailVM>();

        // ========== DROPDOWNS ==========
        public List<PackageSelectOption> AvailablePackages { get; set; } = new List<PackageSelectOption>();

        // ========== METADATA ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; }
    }

    public class SalesReportDetailVM
    {
        public int BookingID { get; set; }
        public string TransactionID => $"#{BookingID}";
        public DateTime BookingDate { get; set; }
        public string CustomerName { get; set; }
        public string PackageName { get; set; }
        public int Pax { get; set; }
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; }
    }

    public class PackageSelectOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}