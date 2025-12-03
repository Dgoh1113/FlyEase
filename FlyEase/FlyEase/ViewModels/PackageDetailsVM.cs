using FlyEase.Data;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class PackageDetailsVM
    {
        public int PackageID { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int AvailableSlots { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        public List<string> Inclusions { get; set; } = new List<string>();

        // === Images ===
        public List<string> AllImages { get; set; } = new List<string>();
        public string MainImage { get; set; } = "/img/default-package.jpg";
        public List<string> GalleryImages { get; set; } = new List<string>();

        // === Accommodation & AddOns ===
        public List<string> AccommodationList { get; set; } = new List<string>();
        public List<string> AddOnList { get; set; } = new List<string>();

        // === Itinerary (Day-by-Day) ===
        public List<PackageItinerary> Itinerary { get; set; } = new List<PackageItinerary>();

        // === Location (For Map) ===
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}