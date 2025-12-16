using System.Collections.Generic;
using FlyEase.Data;
using X.PagedList; // <--- VITAL: Add this using statement

namespace FlyEase.ViewModels
{
    // Class name matches Controller: "DiscountPageVM" (Singular)
    public class DiscountPageVM
    {
        // CHANGED: List -> IPagedList
        public IPagedList<DiscountType> Discounts { get; set; }

        public DiscountType CurrentDiscount { get; set; } = new DiscountType();

        public string? SearchTerm { get; set; }
    }
}