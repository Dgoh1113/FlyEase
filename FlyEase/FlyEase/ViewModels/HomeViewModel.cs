using FlyEase.Data; // Ensure this namespace matches your Package class
using System.Collections.Generic;
using System.Linq;

namespace FlyEase.ViewModels
{
    public class HomeViewModel
    {
        // For the Left Sidebar
        public List<string> Destinations { get; set; }

        // For the Top Slider (Featured/Recent)
        public List<Package> SliderPackages { get; set; }

        // For the Bottom Grid (Categorized Packages)
        public IEnumerable<IGrouping<string, Package>> CategorizedPackages { get; set; }
    }
}