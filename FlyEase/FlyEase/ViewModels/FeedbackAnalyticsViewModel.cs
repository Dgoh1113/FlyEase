using FlyEase.Data;
using System.Collections.Generic;

namespace FlyEase.ViewModels
{
    public class FeedbackAnalyticsViewModel
    {
        // --- 1. Stats ---
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public double PositivePercentage { get; set; }

        // --- 2. New: Popular Packages (Restored) ---
        public PopularPackageViewModel? MostPopularPackage { get; set; }
        public PopularPackageViewModel? LeastPopularPackage { get; set; }

        // --- 3. Chart Data ---
        public Dictionary<int, int> RatingCounts { get; set; } = new Dictionary<int, int>();

        // --- 4. Sidebar List ---
        public List<Feedback> RecentReviews { get; set; } = new List<Feedback>();
    }

    // Helper class for the Top/Bottom cards
    public class PopularPackageViewModel
    {
        public string PackageName { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}