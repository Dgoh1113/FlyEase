using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FlyEase.ViewModels
{
    public class BookingVM
    {
        // Package Info
        public int PackageID { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;

        // Logistics
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int AvailableSlots { get; set; }

        // Images (Split logic happens in Controller)
        public string MainImage { get; set; } = "/img/default-package.jpg";
        public List<string> GalleryImages { get; set; } = new List<string>();
        public List<string> AllImages { get; set; } = new List<string>();

        // Lists
        public List<string> Inclusions { get; set; } = new List<string>();


        // === NEW FIELD ===
        [Display(Name = "Selected Option")]
        public string? SelectedOption { get; set; }
    }
}
