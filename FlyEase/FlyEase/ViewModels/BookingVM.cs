using System;
using System.Collections.Generic;

namespace FlyEase.ViewModels
{
    public class BookingVM
    {
        // 1. Basic Package Details
        public int PackageID { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int AvailableSlots { get; set; }

        // 2. Dates
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // 3. Images
        public string MainImage { get; set; } = "/img/default-package.jpg";
        public List<string> GalleryImages { get; set; } = new List<string>();
        public List<string> AllImages { get; set; } = new List<string>();

        // 4. NEW: Database-Driven Inclusions (ID + Name)
        // This list will be populated from the database
        public List<InclusionItemVM> Inclusions { get; set; } = new List<InclusionItemVM>();

        // 5. Itinerary
        public List<ItineraryViewModel> Itinerary { get; set; } = new List<ItineraryViewModel>();

        // 6. Map Coordinates
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }

    // Helper class for Inclusions
    public class InclusionItemVM
    {
        public int Id { get; set; }       // InclusionID from DB
        public string Name { get; set; } = string.Empty; // InclusionItem from DB
    }

    // Helper class for Itinerary
    public class ItineraryViewModel
    {
        public int DayNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ActivityDescription { get; set; } = string.Empty;
    }
}