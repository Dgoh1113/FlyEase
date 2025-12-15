using FlyEase.Data;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class CustomerInsightReportVM
    {
        // Summary Statistics
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public decimal AverageSpendingPerCustomer { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalBookings { get; set; }

        // Date Filters
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string SortBy { get; set; } = "Spending"; // "Spending" or "Frequency"

        // Chart Data
        public List<string> TopCustomerNames { get; set; } = new List<string>();
        public List<decimal> TopCustomerSpending { get; set; } = new List<decimal>();
        public List<int> TopCustomerBookings { get; set; } = new List<int>();
        public List<string> ChartColors { get; set; } = new List<string>();

        // Detailed Table Data
        public List<CustomerInsightDetailVM> Details { get; set; } = new List<CustomerInsightDetailVM>();
    }

    public class CustomerInsightDetailVM
    {
        public int UserID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int TotalBookings { get; set; }
        public int TotalPeople { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AverageSpentPerBooking { get; set; }
        public DateTime FirstBookingDate { get; set; }
        public DateTime LastBookingDate { get; set; }
        public decimal AverageRating { get; set; }
        public int ReviewsGiven { get; set; }
        public string CustomerTier { get; set; } = "Standard"; // VIP, Premium, Standard, New
    }
}