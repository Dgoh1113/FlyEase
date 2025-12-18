using System;
using System.Collections.Generic;

namespace FlyEase.ViewModels
{
    public class PackagePerformanceReportVM
    {
        // ========== FILTER PARAMETERS ==========
        public string ViewMode { get; set; } = "custom"; // daily, monthly, custom
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string CategoryFilter { get; set; }
        public int? PackageFilter { get; set; }
        public string SortBy { get; set; } = "Revenue"; // "Revenue" or "Volume"

        // ========== PAGINATION ==========
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        // ========== SUMMARY STATISTICS ==========
        public int TotalPackagesSold { get; set; } // Total Qty
        public decimal TotalRevenue { get; set; }
        public double AverageRating { get; set; }

        // Most Sold Package (For Summary Box)
        public string TopPackageName { get; set; } = "N/A";
        public int TopPackageCount { get; set; }

        // ========== CHART 1: REVENUE (Top 5 by $) ==========
        public List<string> RevenueChartLabels { get; set; } = new List<string>();
        public List<decimal> RevenueChartValues { get; set; } = new List<decimal>();

        // ========== CHART 2: PAX (Top 5 by Qty) ==========
        public List<string> PaxChartLabels { get; set; } = new List<string>();
        public List<int> PaxChartValues { get; set; } = new List<int>();

        // ========== DETAILED REPORT DATA ==========
        public List<PackagePerformanceDetailVM> Details { get; set; } = new List<PackagePerformanceDetailVM>();

        // ========== DROPDOWNS ==========
        public List<string> AvailableCategories { get; set; } = new List<string>();
        public List<PackageFilterOption> AvailablePackages { get; set; } = new List<PackageFilterOption>();

        // ========== METADATA ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = "Admin";
    }

    public class PackagePerformanceDetailVM
    {
        public int PackageID { get; set; }
        public string PackageName { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }

        public int TotalBookings { get; set; }
        public int TotalPax { get; set; } // Quantity sold
        public decimal Revenue { get; set; }
        public double Rating { get; set; }
    }

    // Helper class specific to this report to prevent conflicts
    public class PackageFilterOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}