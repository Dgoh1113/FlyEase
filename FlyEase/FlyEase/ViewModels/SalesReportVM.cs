using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class SalesReportVM
    {
        // ========== FILTER PARAMETERS ==========
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Filter By")]
        public string DateFilterType { get; set; } = "booking"; // booking, payment, travel

        [Display(Name = "Payment Method")]
        public string PaymentMethodFilter { get; set; } = "All";

        [Display(Name = "Booking Status")]
        public string BookingStatusFilter { get; set; } = "All";

        // ========== SUMMARY STATISTICS ==========
        public int TotalBookings { get; set; }
        public int TotalPayments { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageBookingValue { get; set; }
        public decimal PaymentSuccessRate { get; set; } // Percentage

        public int CompletedBookings { get; set; }
        public int PendingBookings { get; set; }
        public int CancelledBookings { get; set; }

        public decimal CompletedPayments { get; set; }
        public decimal PendingPayments { get; set; }
        public decimal FailedPayments { get; set; }

        // ========== CHART DATA ==========
        public List<string> RevenueChartDates { get; set; } = new List<string>();
        public List<decimal> RevenueChartValues { get; set; } = new List<decimal>();

        public List<string> PaymentMethodLabels { get; set; } = new List<string>();
        public List<decimal> PaymentMethodValues { get; set; } = new List<decimal>();
        public List<string> PaymentMethodColors { get; set; } = new List<string>();

        public List<string> BookingStatusLabels { get; set; } = new List<string>();
        public List<int> BookingStatusValues { get; set; } = new List<int>();

        // ========== DETAILED TABLE DATA ==========
        public List<SalesReportDetailVM> Details { get; set; } = new List<SalesReportDetailVM>();

        // ========== AVAILABLE OPTIONS (FOR DROPDOWNS) ==========
        public List<string> AvailablePaymentMethods { get; set; } = new List<string>();
        public List<string> AvailableBookingStatuses { get; set; } = new List<string>();
    }

    // Detail row for the table
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