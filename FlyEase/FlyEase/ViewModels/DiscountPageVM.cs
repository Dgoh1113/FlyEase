using FlyEase.Data;
using System.Collections.Generic;

namespace FlyEase.ViewModels
{
    public class DiscountsPageVM
    {
        // List of all discounts to display in the table
        public List<DiscountType> Discounts { get; set; } = new List<DiscountType>();

        // The object used for Creating or Editing a specific discount
        public DiscountType CurrentDiscount { get; set; } = new DiscountType();
    }
}