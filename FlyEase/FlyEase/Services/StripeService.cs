using Stripe;
using Stripe.Checkout;

namespace FlyEase.Services
{
    public class StripeService
    {
        public StripeService(IConfiguration configuration)
        {
            // This takes the value from your secrets.json/appsettings
            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        }

        public async Task<Session> CreateCheckoutSessionAsync(
            decimal amount,
            string currency,
            string successUrl,
            string cancelUrl,
            string bookingReference)
        {
            var options = new SessionCreateOptions
            {
                // 1. ADD ALL PAYMENT METHODS HERE
                PaymentMethodTypes = new List<string>
                {
                    "card",
                    "grabpay",
                    "fpx", // Online Banking (Maybank2u, CIMB Clicks, etc.)
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(amount * 100), // Amount in cents
                            Currency = currency, // e.g., "myr"
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "FlyEase Travel Booking",
                                Description = $"Booking Ref: {bookingReference}"
                            },
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment", // "payment" for one-time payments
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "booking_ref", bookingReference }
                }
            };

            var service = new SessionService();
            return await service.CreateAsync(options);
        }

        // =================================================================
        // NEW: REFUND METHOD FOR CANCELLATIONS
        // =================================================================
        public async Task<Refund> RefundPaymentAsync(string paymentIntentId)
        {
            var options = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
                // Note: If you leave Amount null, it defaults to a full refund.
                // To do a partial refund, set: Amount = (long)(amountToRefund * 100)
            };

            var service = new RefundService();
            return await service.CreateAsync(options);
        }
    }
}