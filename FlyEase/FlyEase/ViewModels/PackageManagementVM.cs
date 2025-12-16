using System.ComponentModel.DataAnnotations;
using FlyEase.Data;

namespace FlyEase.ViewModels
{
    public class PackageManagementViewModel
    {
        public Package Package { get; set; } = new Package();

        // Helper to handle existing images for the main form
        public List<string> ImageList => string.IsNullOrEmpty(Package.ImageURL)
            ? new List<string>()
            : Package.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

        // List Data
        public List<Package> Packages { get; set; } = new List<Package>();
        public List<PackageCategory> Categories { get; set; } = new List<PackageCategory>();
        public List<string> Inclusions { get; set; } = new List<string>();
        public List<PackageItinerary> Itinerary { get; set; } = new List<PackageItinerary>();

        // Form State
        public int? EditingPackageId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? NewCategoryName { get; set; }
        public int? SelectedCategoryId { get; set; }

        // --- PAGINATION PROPERTIES ---
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 4; // Set limit to 4
    }

    public class CreatePackageRequest
    {
        [Required]
        public string PackageName { get; set; } = string.Empty;
        public int? CategoryID { get; set; }
        public string? NewCategoryName { get; set; }
        public string? Description { get; set; }
        [Required]
        public string Destination { get; set; } = string.Empty;
        [Required]
        public decimal Price { get; set; }
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }
        [Required]
        public int AvailableSlots { get; set; }
        public List<IFormFile>? ImageFiles { get; set; }
        public List<string>? Inclusions { get; set; }
        public List<ItineraryItemRequest>? Itinerary { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class UpdatePackageRequest : CreatePackageRequest
    {
        [Required]
        public int PackageID { get; set; }
        public List<string>? DeleteImagePaths { get; set; }
    }

    public class ItineraryItemRequest
    {
        public int DayNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ActivityDescription { get; set; } = string.Empty;
    }
}