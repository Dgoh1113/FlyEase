using System;
using System.ComponentModel.DataAnnotations;
using FlyEase.Data;

namespace FlyEase.ViewModels
{
    public class PackageViewModel
    {
        public int PackageID { get; set; }

        [Required]
        [StringLength(100)]
        public string PackageName { get; set; } = null!;

        [Required]
        [Display(Name = "Category")]
        public int CategoryID { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(100)]
        public string Destination { get; set; } = null!;

        [Required]
        [Range(0.01, 100000)]
        public decimal Price { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7);

        [Required]
        [Range(1, 1000)]
        [Display(Name = "Available Slots")]
        public int AvailableSlots { get; set; }

        [Url]
        [Display(Name = "Image URL")]
        public string? ImageURL { get; set; }

        // For dropdown
        public List<PackageCategory>? Categories { get; set; }

        // Action type for the view
        public string Action { get; set; } = "Index";
    }
}