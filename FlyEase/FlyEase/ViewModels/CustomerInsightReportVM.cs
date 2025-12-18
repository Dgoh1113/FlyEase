using System;
using System.Collections.Generic;

namespace FlyEase.ViewModels
{
    public class CustomerInsightReportVM
    {
        // ========== FILTER PARAMETERS ==========
        public string ViewMode { get; set; } = "custom"; // daily, monthly, custom
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string SearchTerm { get; set; }
        public string SortBy { get; set; } = "Spending"; // "Spending" or "Frequency"

        // ========== PAGINATION ==========
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        // ========== SUMMARY STATISTICS ==========
        public int TotalCustomers { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageSpendPerCustomer { get; set; }

        // Top Customer (For Summary Box)
        public string TopCustomerName { get; set; } = "N/A";
        public decimal TopCustomerValue { get; set; } // Spent amount

        // ========== CHART 1: SPENDING (Top 5 by $) ==========
        public List<string> SpendingChartLabels { get; set; } = new List<string>();
        public List<decimal> SpendingChartValues { get; set; } = new List<decimal>();

        // ========== CHART 2: FREQUENCY (Top 5 by #) ==========
        public List<string> FrequencyChartLabels { get; set; } = new List<string>();
        public List<int> FrequencyChartValues { get; set; } = new List<int>();

        // ========== DETAILED REPORT DATA ==========
        public List<CustomerInsightDetailVM> Details { get; set; } = new List<CustomerInsightDetailVM>();

        // ========== METADATA ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = "Admin";
    }

    public class CustomerInsightDetailVM
    {
        public int UserID { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        public int TotalBookings { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime? LastBookingDate { get; set; }
        public decimal AverageRating { get; set; } // If they left feedback
    }
}