using System.ComponentModel.DataAnnotations;
using FlyEase.Data;

namespace FlyEase.Models
{
    public class PackageManagementViewModel
    {
        public Package Package { get; set; } = new Package();
        public List<Package> Packages { get; set; } = new List<Package>();
        public List<PackageCategory> Categories { get; set; } = new List<PackageCategory>();
        public List<string> Inclusions { get; set; } = new List<string>();
        public int? EditingPackageId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? NewCategoryName { get; set; }
        public int? SelectedCategoryId { get; set; }
        public IFormFile? ImageFile { get; set; }
    }

    public class CreatePackageRequest
    {
        [Required]
        public string PackageName { get; set; } = null!;

        public int? CategoryID { get; set; }

        public string? NewCategoryName { get; set; }

        public string? Description { get; set; }

        [Required]
        public string Destination { get; set; } = null!;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int AvailableSlots { get; set; }

        public IFormFile? ImageFile { get; set; }

        public List<string> Inclusions { get; set; } = new List<string>();
    }

    public class UpdatePackageRequest : CreatePackageRequest
    {
        [Required]
        public int PackageID { get; set; }

        public string? ExistingImagePath { get; set; }
    }
}