using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using FlyEase.Data;

namespace FlyEase.ViewModels
{
    // Added IValidatableObject to enforce "either select existing category OR enter new category" rule
    public class PackageViewModel : IValidatableObject
    {
        public int PackageID { get; set; }

        [Required]
        [StringLength(100)]
        public string PackageName { get; set; } = null!;

        // Allow null so empty select can bind. Validation ensures one of CategoryID or NewCategoryName is provided.
        [Display(Name = "Category")]
        public int? CategoryID { get; set; }

        [Display(Name = "Category Name")]
        [StringLength(100)]
        // Validate client/server side when provided: letters, numbers, spaces, hyphens allowed.
        [RegularExpression(@"^[A-Za-z0-9\s\-]{1,100}$", ErrorMessage = "Category name must be 1-100 characters and contain only letters, numbers, spaces or hyphens.")]
        public string? NewCategoryName { get; set; }

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

        // For file upload
        [Display(Name = "Package Image")]
        public IFormFile? ImageFile { get; set; }

        // For displaying existing image
        public string? ImageURL { get; set; }

        // For dropdown
        public List<PackageCategory>? Categories { get; set; }

        // Action type for the view
        public string Action { get; set; } = "Index";

        // Cross-field validation: ensure either an existing category is selected OR a new category name is provided.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            bool hasSelectedCategory = CategoryID.HasValue && CategoryID.Value > 0;
            bool hasNewCategoryName = !string.IsNullOrWhiteSpace(NewCategoryName);

            if (!hasSelectedCategory && !hasNewCategoryName)
            {
                // Emit member names so validation message shows next to both fields
                results.Add(new ValidationResult(
                    "Please select an existing category or enter a new category name.",
                    new[] { nameof(CategoryID), nameof(NewCategoryName) }));
            }

            return results;
        }
    }
}