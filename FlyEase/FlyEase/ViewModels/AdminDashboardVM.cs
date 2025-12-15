using System.Collections.Generic;
using FlyEase.Data;
using X.PagedList; // <--- Required

namespace FlyEase.ViewModels
{
    // 1. Main Dashboard (Unchanged)
    public class AdminDashboardVM
    {
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<Booking> RecentBookings { get; set; } = new List<Booking>();
        public List<Package> LowStockPackages { get; set; } = new List<Package>();
        public List<string> RevenueMonths { get; set; } = new List<string>();
        public List<decimal> RevenueValues { get; set; } = new List<decimal>();
        public int CompletedBookingsCount { get; set; }
        public int CancelledBookingsCount { get; set; }
        public int PendingBookingsCount { get; set; }
    }

    // 2. Users Tab
    public class UsersPageVM
    {
        public IPagedList<User> Users { get; set; } // Changed to IPagedList
        public User CurrentUser { get; set; } = new User();
        public string? SearchTerm { get; set; }
        public string? RoleFilter { get; set; }
    }

    // 3. Bookings Tab
    public class BookingsPageVM
    {
        public IPagedList<Booking> Bookings { get; set; } // Changed to IPagedList
        public Booking CurrentBooking { get; set; } = new Booking();
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
    }

    // 4. Packages Tab
    public class PackagesPageVM
    {
        public IPagedList<Package> Packages { get; set; } // Changed to IPagedList
        public List<PackageCategory> Categories { get; set; } = new List<PackageCategory>();
        public Package CurrentPackage { get; set; } = new Package();
        public string? SearchTerm { get; set; }
    }
}