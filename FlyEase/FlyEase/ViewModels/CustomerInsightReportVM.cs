using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class CustomerInsightReportVM
    {
        // ========== FILTER PARAMETERS ==========
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string SortBy { get; set; } = "Spending";

        // ========== REPORT METADATA ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = "Admin";

        // ========== SUMMARY STATISTICS ==========
        public int TotalCustomers { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageSpendingPerCustomer { get; set; }
        public int ActiveCustomers { get; set; } // Customers who booked in this period

        // ========== CHART DATA (Top 5 Customers) ==========
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartValues { get; set; } = new List<decimal>();

        // ========== DETAILED CUSTOMER LEDGER ==========
        public List<CustomerInsightDetailVM> Details { get; set; } = new List<CustomerInsightDetailVM>();
    }

    public class CustomerInsightDetailVM
    {
        public int UserID { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        // Metrics
        public int TotalBookings { get; set; }
        public int TotalPeople { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AverageSpentPerBooking { get; set; }

        // Dates
        public DateTime FirstBookingDate { get; set; }
        public DateTime LastBookingDate { get; set; }

        // Loyalty
        public string CustomerTier { get; set; } // VIP, Premium, Standard, New
        public decimal AverageRating { get; set; }
        public int ReviewsGiven { get; set; }
    }
}