using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class PackagePerformanceReportVM
    {
        // ========== FILTER PARAMETERS ==========
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Category")]
        public string CategoryFilter { get; set; } = "All";

        // ========== SUMMARY STATISTICS ==========
        public int TotalPackages { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRevenuePerPackage { get; set; }
        public decimal AverageRating { get; set; }

        // ========== CHART DATA ==========
        public List<string> PackageNames { get; set; } = new List<string>();
        public List<int> BookingCounts { get; set; } = new List<int>();
        public List<string> TopPackageColors { get; set; } = new List<string>();

        public List<string> RevenuePackageNames { get; set; } = new List<string>();
        public List<decimal> RevenueValues { get; set; } = new List<decimal>();

        public List<string> RatingPackageNames { get; set; } = new List<string>();
        public List<double> RatingValues { get; set; } = new List<double>();

        // ========== DETAILED TABLE DATA ==========
        public List<PackagePerformanceDetailVM> Details { get; set; } = new List<PackagePerformanceDetailVM>();

        // ========== AVAILABLE OPTIONS (FOR DROPDOWNS) ==========
        public List<string> AvailableCategories { get; set; } = new List<string>();

        // ========== TOP & BOTTOM PERFORMERS ==========
        public List<PackagePerformanceDetailVM> TopPerformers { get; set; } = new List<PackagePerformanceDetailVM>();
        public List<PackagePerformanceDetailVM> BottomPerformers { get; set; } = new List<PackagePerformanceDetailVM>();
    }

    // Detail row for the table
    public class PackagePerformanceDetailVM
    {
        public int PackageID { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TotalBookings { get; set; }
        public int TotalPeople { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRevenuePerBooking { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int AvailableSlots { get; set; }
        public int BookedSlots => AvailableSlots > 0 ? TotalPeople : TotalPeople; // Simplified calculation
        public decimal OccupancyRate { get; set; } // Percentage
    }
}