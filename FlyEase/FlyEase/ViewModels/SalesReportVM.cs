using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class SalesReportVM
    {
        // ========== FILTER PARAMETERS (For the Table) ==========
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Filter By")]
        public string DateFilterType { get; set; } = "booking";

        [Display(Name = "Payment Method")]
        public string PaymentMethodFilter { get; set; } = "All";

        [Display(Name = "Booking Status")]
        public string BookingStatusFilter { get; set; } = "All";

        // ========== SUMMARY STATISTICS ==========
        public int TotalBookings { get; set; }
        public int TotalPayments { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageBookingValue { get; set; }
        public decimal PaymentSuccessRate { get; set; }

        public int CompletedBookings { get; set; }
        public int PendingBookings { get; set; }
        public int CancelledBookings { get; set; }

        public decimal CompletedPayments { get; set; }
        public decimal PendingPayments { get; set; }
        public decimal FailedPayments { get; set; }

        // =========================================================
        //  CHART DATA (7 Days / 30 Days / 1 Year)
        // =========================================================

        // --- 1. REVENUE (Line) ---
        public List<string> RevenueLabels7Days { get; set; } = new List<string>();
        public List<decimal> RevenueValues7Days { get; set; } = new List<decimal>();
        public List<string> RevenueLabels30Days { get; set; } = new List<string>();
        public List<decimal> RevenueValues30Days { get; set; } = new List<decimal>();
        public List<string> RevenueLabels1Year { get; set; } = new List<string>();
        public List<decimal> RevenueValues1Year { get; set; } = new List<decimal>();

        // Revenue Donut (Top Packages by Revenue - Last Year)
        public List<string> RevenueDonutLabels { get; set; } = new List<string>();
        public List<decimal> RevenueDonutValues { get; set; } = new List<decimal>();

        // --- 2. BOOKINGS (Line) ---
        public List<string> BookingLabels7Days { get; set; } = new List<string>();
        public List<int> BookingValues7Days { get; set; } = new List<int>();
        public List<string> BookingLabels30Days { get; set; } = new List<string>();
        public List<int> BookingValues30Days { get; set; } = new List<int>();
        public List<string> BookingLabels1Year { get; set; } = new List<string>();
        public List<int> BookingValues1Year { get; set; } = new List<int>();

        // Booking Donut (Booking Status)
        public List<string> BookingDonutLabels { get; set; } = new List<string>();
        public List<int> BookingDonutValues { get; set; } = new List<int>();

        // --- 3. USERS (Line) ---
        public List<string> UserLabels7Days { get; set; } = new List<string>();
        public List<int> UserValues7Days { get; set; } = new List<int>();
        public List<string> UserLabels30Days { get; set; } = new List<string>();
        public List<int> UserValues30Days { get; set; } = new List<int>();
        public List<string> UserLabels1Year { get; set; } = new List<string>();
        public List<int> UserValues1Year { get; set; } = new List<int>();

        // --- 4. PACKAGES (Bar - Ratings) ---
        public List<string> PackageRatingLabels7Days { get; set; } = new List<string>();
        public List<double> PackageRatingValues7Days { get; set; } = new List<double>();

        public List<string> PackageRatingLabels30Days { get; set; } = new List<string>();
        public List<double> PackageRatingValues30Days { get; set; } = new List<double>();

        public List<string> PackageRatingLabels1Year { get; set; } = new List<string>();
        public List<double> PackageRatingValues1Year { get; set; } = new List<double>();


        // ========== DETAILED TABLE DATA ==========
        public List<SalesReportDetailVM> Details { get; set; } = new List<SalesReportDetailVM>();

        // ========== OPTIONS FOR DROPDOWNS ==========
        public List<string> AvailablePaymentMethods { get; set; } = new List<string>();
        public List<string> AvailableBookingStatuses { get; set; } = new List<string>();

        // Legacy props for compatibility (if needed)
        public List<string> RevenueChartDates { get; set; } = new List<string>();
        public List<decimal> RevenueChartValues { get; set; } = new List<decimal>();
    }

    public class SalesReportDetailVM
    {
        public int BookingID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; }
        public DateTime TravelDate { get; set; }
        public int NumberOfPeople { get; set; }
        public decimal BookingAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string BookingStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime? LastPaymentDate { get; set; }
    }
}