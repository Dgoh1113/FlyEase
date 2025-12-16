using System.Collections.Generic;
using FlyEase.Data;
using X.PagedList;

namespace FlyEase.ViewModels
{
    public class AdminDashboardVM
    {
        // Totals
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal TotalRevenue { get; set; }

        // Lists
        public List<Booking> RecentBookings { get; set; } = new List<Booking>();
        public List<Package> LowStockPackages { get; set; } = new List<Package>();

        // --- Revenue Chart Data (Current vs Previous) ---
        // 1 Year
        public List<string> RevenueLabels1Year { get; set; } = new List<string>();
        public List<decimal> RevenueValues1Year { get; set; } = new List<decimal>();
        public List<decimal> RevenueValues1YearPrev { get; set; } = new List<decimal>();

        // 30 Days
        public List<string> RevenueLabels30Days { get; set; } = new List<string>();
        public List<decimal> RevenueValues30Days { get; set; } = new List<decimal>();
        public List<decimal> RevenueValues30DaysPrev { get; set; } = new List<decimal>();

        // 7 Days
        public List<string> RevenueLabels7Days { get; set; } = new List<string>();
        public List<decimal> RevenueValues7Days { get; set; } = new List<decimal>();
        public List<decimal> RevenueValues7DaysPrev { get; set; } = new List<decimal>();

        // Top Packages Chart
        public List<string> PackageRevenueLabels { get; set; } = new List<string>();
        public List<decimal> PackageRevenueValues { get; set; } = new List<decimal>();

        // Legacy/Unused properties kept for compatibility if needed
        public int CompletedBookingsCount { get; set; }
        public int CancelledBookingsCount { get; set; }
        public int PendingBookingsCount { get; set; }
        public List<string> RevenueMonths { get; set; } = new List<string>();
        public List<decimal> RevenueValues { get; set; } = new List<decimal>();
    }

    // Other VMs needed for AdminDashboardController methods
    public class UsersPageVM
    {
        public IPagedList<User> Users { get; set; }
        public User CurrentUser { get; set; } = new User();
        public string? SearchTerm { get; set; }
        public string? RoleFilter { get; set; }
    }

    public class BookingsPageVM
    {
        public IPagedList<Booking> Bookings { get; set; }
        public Booking CurrentBooking { get; set; } = new Booking();
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
    }

    public class PackagesPageVM
    {
        public IPagedList<Package> Packages { get; set; }
        public List<PackageCategory> Categories { get; set; } = new List<PackageCategory>();
        public Package CurrentPackage { get; set; } = new Package();
        public string? SearchTerm { get; set; }
    }

   
}