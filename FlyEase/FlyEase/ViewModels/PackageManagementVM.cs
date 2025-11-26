using System.ComponentModel.DataAnnotations;
using FlyEase.Data;

namespace FlyEase.ViewModels
{
    public class PackageManagementViewModel
    {
        public Package Package { get; set; } = new Package();

        // Helper to split the single DB string into a list of paths for the View
        public List<string> ImageList => string.IsNullOrEmpty(Package.ImageURL)
            ? new List<string>()
            : Package.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

        public List<Package> Packages { get; set; } = new List<Package>();
        public List<PackageCategory> Categories { get; set; } = new List<PackageCategory>();
        public List<string> Inclusions { get; set; } = new List<string>();
        public int? EditingPackageId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? NewCategoryName { get; set; }
        public int? SelectedCategoryId { get; set; }
    }

    public class CreatePackageRequest
    {
        [Required] public string PackageName { get; set; } = null!;
        public int? CategoryID { get; set; }
        public string? NewCategoryName { get; set; }
        public string? Description { get; set; }
        [Required] public string Destination { get; set; } = null!;
        [Required][Range(0.01, double.MaxValue)] public decimal Price { get; set; }
        [Required] public DateTime StartDate { get; set; }
        [Required] public DateTime EndDate { get; set; }
        [Required][Range(1, int.MaxValue)] public int AvailableSlots { get; set; }

        // IMPORTANT: Must be a List to accept multiple files
        public List<IFormFile> ImageFiles { get; set; } = new List<IFormFile>();

        public List<string> Inclusions { get; set; } = new List<string>();
    }

    public class UpdatePackageRequest : CreatePackageRequest
    {
        [Required]
        public int PackageID { get; set; }

        // Stores the paths (e.g., "/img/pic1.jpg") of images the user clicked "X" on
        public List<string> DeleteImagePaths { get; set; } = new List<string>();
    }
}