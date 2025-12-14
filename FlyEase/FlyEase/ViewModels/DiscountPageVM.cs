using FlyEase.Data;

public class DiscountsPageVM
{
    public List<DiscountType> Discounts { get; set; } = new List<DiscountType>();

    // The object used for Creating or Editing a specific discount
    public DiscountType CurrentDiscount { get; set; } = new DiscountType();

    // Search
    public string? SearchTerm { get; set; }
}