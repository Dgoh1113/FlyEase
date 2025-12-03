// StripeService.cs
using Stripe;

public class StripeService
{
    public StripeService(IConfiguration configuration)
    {
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    public async Task<string> CreatePaymentIntentAsync(decimal amount, string currency = "myr")
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Convert to cents
                Currency = currency,
                PaymentMethodTypes = new List<string> { "card" },
                Description = "FlyEase Travel Booking",
                Metadata = new Dictionary<string, string>
                {
                    { "service", "FlyEase Travel" }
                }
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);

            return paymentIntent.Id; // REAL Stripe ID
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stripe Error: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> ConfirmPaymentAsync(string paymentIntentId)
    {
        try
        {
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);

            return paymentIntent.Status == "succeeded";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stripe Error: {ex.Message}");
            return false;
        }
    }
}