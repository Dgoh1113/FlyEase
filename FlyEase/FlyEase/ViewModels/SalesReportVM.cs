using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class SalesReportVM
    {
        // ========== FILTER PARAMETERS ==========
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DateFilterType { get; set; }
        public string PaymentMethodFilter { get; set; }
        public string BookingStatusFilter { get; set; }

        // ========== REPORT METADATA ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = "Admin";

        // ========== SUMMARY STATISTICS ==========
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }

        // Status Counts
        public int CompletedBookings { get; set; }
        public int PendingBookings { get; set; }
        public int CancelledBookings { get; set; }

        // Financials
        public decimal CompletedPayments { get; set; }
        public decimal PendingPayments { get; set; }

        // ========== DETAILED TRANSACTION LOG (The Real Report) ==========
        public List<SalesReportDetailVM> Details { get; set; } = new List<SalesReportDetailVM>();

        // ========== DROPDOWN OPTIONS ==========
        public List<string> AvailablePaymentMethods { get; set; } = new List<string>();
        public List<string> AvailableBookingStatuses { get; set; } = new List<string>();
    }

    public class SalesReportDetailVM
    {
        public int BookingID { get; set; }
        public string TransactionID => $"TRX-{BookingID:D6}";
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string PackageName { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime TravelDate { get; set; }
        public int NumberOfPeople { get; set; }
        public decimal BookingAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string BookingStatus { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }
}