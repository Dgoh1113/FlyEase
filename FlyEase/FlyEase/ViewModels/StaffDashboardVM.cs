using System.ComponentModel.DataAnnotations;
using FlyEase.Data;

namespace FlyEase.ViewModels
{
    // 1. For the Main Home Dashboard
    public class StaffDashboardVM
    {
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<Booking> RecentBookings { get; set; } = new List<Booking>();
        public List<Package> LowStockPackages { get; set; } = new List<Package>();
    }

    // 2. For the Users Management Tab
    public class UsersPageVM
    {
        public List<User> Users { get; set; } = new List<User>();
        public User CurrentUser { get; set; } = new User();
    }

    // 3. For the Bookings Management Tab
    public class BookingsPageVM
    {
        public List<Booking> Bookings { get; set; } = new List<Booking>();
        public Booking CurrentBooking { get; set; } = new Booking();
    }

    // 4. For the Packages Management Tab (The one you sent me)
    public class PackagesPageVM
    {
        // The List for the Table
        public List<Package> Packages { get; set; } = new List<Package>();

        // The Data for the Dropdowns
        public List<PackageCategory> Categories { get; set; } = new List<PackageCategory>();

        // The Single Object for the Create/Edit Form
        public Package CurrentPackage { get; set; } = new Package();
    }
}