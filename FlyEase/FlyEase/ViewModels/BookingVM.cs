using System;
using System.Collections.Generic;
using FlyEase.Data;

namespace FlyEase.ViewModels
{
    public class BookingVM
    {
        // === Basic Package Details ===
        public int PackageID { get; set; }
        public string PackageName { get; set; }
        public string Destination { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int AvailableSlots { get; set; }
        public string CategoryName { get; set; }

        // === Map Coordinates (Added to fix the Map script) ===
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // === Lists ===
        public List<string> Inclusions { get; set; } = new List<string>();

        // === FIXED: Added these missing lists ===
        public List<string> AccommodationList { get; set; } = new List<string>();
        public List<string> AddOnList { get; set; } = new List<string>();

        // === Image Logic ===
        public List<string> AllImages { get; set; }
        public string MainImage { get; set; }
        public List<string> GalleryImages { get; set; }

        // === Itinerary ===
        public List<PackageItinerary> Itinerary { get; set; }

        // === Reviews & Stats ===
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public string TopReviewComment { get; set; }
        public string TopReviewUser { get; set; }
        public int? TopReviewRating { get; set; }
        public List<Feedback> AllReviews { get; set; }
    }
}