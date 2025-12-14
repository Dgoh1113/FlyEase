using System;
using System.Collections.Generic;

namespace FlyEase.ViewModels
{
    public class FeedbackAnalyticsViewModel
    {
        // Stats
        public double AverageRating { get; set; }
        public int TotalFeedbackCount { get; set; }

        // Charts & Lists
        public Dictionary<int, int> RatingBreakdown { get; set; }
        public List<LatestFeedbackViewModel> LatestFeedback { get; set; }

        // New: Top and Bottom Packages
        public PopularPackageViewModel MostPopularPackage { get; set; }
        public PopularPackageViewModel LeastPopularPackage { get; set; }
    }

    public class LatestFeedbackViewModel
    {
        public int FeedbackId { get; set; }
        public string UserName { get; set; }
        public string PackageName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PopularPackageViewModel
    {
        public string PackageName { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}