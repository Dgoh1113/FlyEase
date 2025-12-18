using System.Collections.Generic;
using FlyEase.Data;
using X.PagedList;

namespace FlyEase.ViewModels
{
    public class DiscountPageVM
    {
        // IPagedList is used for the pagination helper in the View
        public IPagedList<DiscountType>? Discounts { get; set; }

        // Used for binding when Creating/Editing a discount in a modal
        public DiscountType CurrentDiscount { get; set; } = new DiscountType();

        public string? SearchTerm { get; set; }
    }
    public class DiscountInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal? Rate { get; set; }
        public decimal Amount { get; set; }
        public int? DiscountId { get; set; }
    }
}