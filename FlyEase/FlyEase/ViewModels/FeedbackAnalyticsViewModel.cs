using FlyEase.Data;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlyEase.ViewModels
{
    public class FeedbackAnalyticsViewModel
    {
        // --- 1. Existing Stats ---
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public double PositivePercentage { get; set; }

        // --- 2. Popular Packages ---
        public PopularPackageViewModel? MostPopularPackage { get; set; }
        public PopularPackageViewModel? LeastPopularPackage { get; set; }

        // --- 3. Chart Data ---
        public Dictionary<int, int> RatingCounts { get; set; } = new Dictionary<int, int>();

        // --- 4. Sidebar List ---
        public List<Feedback> RecentReviews { get; set; } = new List<Feedback>();

        // --- 5. NEW: Unrated Bookings Analysis (REQUIRED) ---
        public int UnratedCount { get; set; }
        public List<Booking> UnratedBookings { get; set; } = new List<Booking>();

        // --- 6. NEW: Category Analysis (REQUIRED) ---
        public Dictionary<string, double> CategoryRatings { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    }

    public class PopularPackageViewModel
    {
        public string PackageName { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

}