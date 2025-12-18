using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class PackagePerformanceReportVM
    {
        // ========== FILTER PARAMETERS ==========
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string CategoryFilter { get; set; } = "All";
        public int? PackageFilter { get; set; }

        // ========== REPORT METADATA ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = "Admin";

        // ========== SUMMARY STATISTICS ==========
        public int TotalPackages { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRevenuePerPackage { get; set; }
        public decimal AverageRating { get; set; }

        // ========== CHART DATA (Re-added) ==========
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartValues { get; set; } = new List<decimal>();

        // ========== DETAILED DATA ==========
        public List<PackagePerformanceDetailVM> Details { get; set; } = new List<PackagePerformanceDetailVM>();

        // ========== DROPDOWN OPTIONS ==========
        public List<string> AvailableCategories { get; set; } = new List<string>();
        public List<PackageSelectOption> AvailablePackages { get; set; } = new List<PackageSelectOption>();
    }

    public class PackagePerformanceDetailVM
    {
        public int PackageID { get; set; }
        public string PackageName { get; set; }
        public string Destination { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }

        // Performance Metrics
        public int TotalBookings { get; set; }
        public int TotalPeople { get; set; }
        public decimal TotalRevenue { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }

        // Capacity Metrics
        public int AvailableSlots { get; set; }
        public decimal OccupancyRate { get; set; }
        public string Status { get; set; }
    }

    public class PackageSelectOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}