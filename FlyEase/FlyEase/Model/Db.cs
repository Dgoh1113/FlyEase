using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace FlyEase.Data
{
    public class FlyEaseDbContext : DbContext
    {
        public FlyEaseDbContext(DbContextOptions<FlyEaseDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Booking> Bookings { get; set; } = null!;
        public DbSet<Package> Packages { get; set; } = null!;
        public DbSet<PackageCategory> PackageCategories { get; set; } = null!;
        public DbSet<PackageInclusion> PackageInclusions { get; set; } = null!;

        // === 1. NEW TABLE FOR ITINERARY ===
        public DbSet<PackageItinerary> PackageItineraries { get; set; } = null!;

        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<DiscountType> DiscountTypes { get; set; } = null!;
        public DbSet<BookingDiscount> BookingDiscounts { get; set; } = null!;
        public DbSet<Feedback> Feedbacks { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User
            modelBuilder.Entity<User>()
                .HasKey(u => u.UserID);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // PackageCategory
            modelBuilder.Entity<PackageCategory>()
                .HasKey(pc => pc.CategoryID);

            // Package
            modelBuilder.Entity<Package>()
                .HasKey(p => p.PackageID);

            modelBuilder.Entity<Package>()
                .HasOne(p => p.Category)
                .WithMany(pc => pc.Packages)
                .HasForeignKey(p => p.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            // PackageInclusion
            modelBuilder.Entity<PackageInclusion>()
                .HasKey(pi => pi.InclusionID);

            modelBuilder.Entity<PackageInclusion>()
                .HasOne(pi => pi.Package)
                .WithMany(p => p.PackageInclusions)
                .HasForeignKey(pi => pi.PackageID)
                .OnDelete(DeleteBehavior.Cascade);

            // === 2. CONFIGURATION FOR ITINERARY ===
            modelBuilder.Entity<PackageItinerary>()
                .HasKey(pi => pi.ItineraryID);

            modelBuilder.Entity<PackageItinerary>()
                .HasOne(pi => pi.Package)
                .WithMany(p => p.Itinerary)
                .HasForeignKey(pi => pi.PackageID)
                .OnDelete(DeleteBehavior.Cascade);
            // ======================================

            // Booking
            modelBuilder.Entity<Booking>()
                .HasKey(b => b.BookingID);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Package)
                .WithMany(p => p.Bookings)
                .HasForeignKey(b => b.PackageID)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment
            modelBuilder.Entity<Payment>()
                .HasKey(p => p.PaymentID);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Booking)
                .WithMany(b => b.Payments)
                .HasForeignKey(p => p.BookingID)
                .OnDelete(DeleteBehavior.Cascade);

            // DiscountType
            modelBuilder.Entity<DiscountType>()
                .HasKey(dt => dt.DiscountTypeID);

            // BookingDiscount
            modelBuilder.Entity<BookingDiscount>()
                .HasKey(bd => bd.BookingDiscountID);

            modelBuilder.Entity<BookingDiscount>()
                .HasOne(bd => bd.Booking)
                .WithMany(b => b.BookingDiscounts)
                .HasForeignKey(bd => bd.BookingID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingDiscount>()
                .HasOne(bd => bd.DiscountType)
                .WithMany(dt => dt.BookingDiscounts)
                .HasForeignKey(bd => bd.DiscountTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            // Feedback
            modelBuilder.Entity<Feedback>()
                .HasKey(f => f.FeedbackID);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Booking)
                .WithMany(b => b.Feedbacks)
                .HasForeignKey(f => f.BookingID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    // ===== Entities =====
    public class User
    {
        public int TokenID { get; set; }
        public int UserID { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string? Address { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiryTime { get; set; }
        public DateTime CreatedDate { get; set; }
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }

    public class PackageCategory
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = null!;

        public ICollection<Package> Packages { get; set; } = new List<Package>();
    }

    public class Package
    {
        public int PackageID { get; set; }
        public int CategoryID { get; set; }
        public string PackageName { get; set; } = null!;
        public string? Description { get; set; }
        public string Destination { get; set; } = null!;
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int AvailableSlots { get; set; }
        public string? ImageURL { get; set; }

        // === 3. NEW LOCATION FIELDS ===
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [NotMapped]
        public double AverageRating { get; set; }

        [NotMapped]
        public List<IFormFile>? ImageFiles { get; set; }

        public PackageCategory Category { get; set; } = null!;
        public ICollection<PackageInclusion> PackageInclusions { get; set; } = new List<PackageInclusion>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        // === 4. NEW ITINERARY RELATIONSHIP ===
        public ICollection<PackageItinerary> Itinerary { get; set; } = new List<PackageItinerary>();
    }

    // === 5. NEW ITINERARY CLASS ===
    public class PackageItinerary
    {
        public int ItineraryID { get; set; }
        public int PackageID { get; set; }
        public int DayNumber { get; set; } // e.g., 1, 2, 3
        public string Title { get; set; } = null!; // e.g., "Arrival"
        public string ActivityDescription { get; set; } = null!;

        public Package Package { get; set; } = null!;
    }

    public class PackageInclusion
    {
        public int InclusionID { get; set; }
        public int PackageID { get; set; }
        public string InclusionItem { get; set; } = null!;
        public Package Package { get; set; } = null!;
    }

    public class Booking
    {
        public int BookingID { get; set; }
        public int UserID { get; set; }
        public int PackageID { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime TravelDate { get; set; }
        public int NumberOfPeople { get; set; }
        public decimal TotalBeforeDiscount { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string BookingStatus { get; set; } = null!;

        public User User { get; set; } = null!;
        public Package Package { get; set; } = null!;
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<BookingDiscount> BookingDiscounts { get; set; } = new List<BookingDiscount>();
        public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }

    public class Payment
    {
        public int PaymentID { get; set; }
        public int BookingID { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public decimal AmountPaid { get; set; }
        public bool IsDeposit { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentStatus { get; set; } = null!;
        public Booking Booking { get; set; } = null!;
    }

    public class DiscountType
    {
        public int DiscountTypeID { get; set; }
        public string DiscountName { get; set; } = null!;
        public decimal? DiscountRate { get; set; }
        public decimal? DiscountAmount { get; set; }
        public ICollection<BookingDiscount> BookingDiscounts { get; set; } = new List<BookingDiscount>();
    }

    public class BookingDiscount
    {
        public int BookingDiscountID { get; set; }
        public int BookingID { get; set; }
        public int DiscountTypeID { get; set; }
        public decimal AppliedAmount { get; set; }
        public Booking Booking { get; set; } = null!;
        public DiscountType DiscountType { get; set; } = null!;
    }

    public class Feedback
    {
        public int FeedbackID { get; set; }
        public int BookingID { get; set; }
        public int UserID { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedDate { get; set; }
        public Booking Booking { get; set; } = null!;
        public User User { get; set; } = null!;
        public string Emotion { get; set; }
    }

    // --- VIEW MODELS (DTOs) KEPT FOR COMPATIBILITY ---
    public class StaffBookingsViewModel
    {
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public int TotalBookingsCount { get; set; }
        public int PendingBookingsCount { get; set; }

        [DataType(DataType.Currency)]
        public decimal TotalRevenueGenerated { get; set; }

        public List<BookingItemVM> Bookings { get; set; } = new List<BookingItemVM>();
    }

    public class BookingItemVM
    {
        public int BookingID { get; set; }

        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Display(Name = "Package")]
        public string PackageName { get; set; } = string.Empty;

        [Display(Name = "Travel Date")]
        [DataType(DataType.Date)]
        public DateTime TravelDate { get; set; }

        [Display(Name = "Pax")]
        public int NumberOfPeople { get; set; }

        [Display(Name = "Booked On")]
        [DataType(DataType.Date)]
        public DateTime BookingDate { get; set; }

        [DataType(DataType.Currency)]
        public decimal FinalAmount { get; set; }

        [DataType(DataType.Currency)]
        public decimal AmountPaid { get; set; }

        public decimal BalanceDue => FinalAmount - AmountPaid;

        public string PaymentStatus
        {
            get
            {
                if (FinalAmount <= 0) return "Free";
                if (BalanceDue <= 0) return "Paid in Full";
                if (AmountPaid > 0) return "Partial";
                return "Unpaid";
            }
        }
        public string BookingStatus { get; set; } = string.Empty;
    }
}