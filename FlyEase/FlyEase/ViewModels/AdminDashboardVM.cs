using System.ComponentModel.DataAnnotations;
using FlyEase.Data;
using System.Collections.Generic;

namespace FlyEase.ViewModels
{
    // 1. For the Main Home Dashboard
    public class AdminDashboardVM
    {
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<Booking> RecentBookings { get; set; } = new List<Booking>();
        public List<Package> LowStockPackages { get; set; } = new List<Package>();

        // For Chart.js
        public List<string> PackageNames { get; set; } = new List<string>();
        public List<double> PackageRatings { get; set; } = new List<double>();
        public List<string> RevenueMonths { get; set; } = new List<string>();
        public List<decimal> RevenueValues { get; set; } = new List<decimal>();
        public List<int> PackageBookingCounts { get; set; } = new List<int>();
        public int CompletedBookingsCount { get; set; }
        public int CancelledBookingsCount { get; set; }
        public int PendingBookingsCount { get; set; }
    }

    // 2. For the Users Management Tab
    public class UsersPageVM
    {
        public List<User> Users { get; set; } = new List<User>();
        public User CurrentUser { get; set; } = new User();

        // Search & Filter
        public string? SearchTerm { get; set; }
        public string? RoleFilter { get; set; }
    }

    // 3. For the Bookings Management Tab
    public class BookingsPageVM
    {
        public List<Booking> Bookings { get; set; } = new List<Booking>();
        public Booking CurrentBooking { get; set; } = new Booking();

        // Search
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; } // Renamed from just 'status' param for clarity
    }

    // 4. For the Packages Management Tab
    public class PackagesPageVM
    {
        public List<Package> Packages { get; set; } = new List<Package>();
        public List<PackageCategory> Categories { get; set; } = new List<PackageCategory>();
        public Package CurrentPackage { get; set; } = new Package();

        // Search
        public string? SearchTerm { get; set; }
    }

    // 5. For the Discounts Management Tab
    public class DiscountsPageVM
    {
        public List<DiscountType> Discounts { get; set; } = new List<DiscountType>();

        // The object used for Creating or Editing a specific discount
        public DiscountType CurrentDiscount { get; set; } = new DiscountType();

        // Search
        public string? SearchTerm { get; set; }
    }
}