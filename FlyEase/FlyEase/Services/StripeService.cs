using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using FlyEase.Model; // Ensure this namespace matches where your StripeSettings class is
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlyEase.Services
{
    public class StripeService
    {
        private readonly StripeSettings _settings;

        // 1. Inject IOptions<StripeSettings> to get the key from secrets.json/appsettings
        public StripeService(IOptions<StripeSettings> settings)
        {
            _settings = settings.Value;

            // Set the API Key globally for this instance context
            StripeConfiguration.ApiKey = _settings.SecretKey;
        }

        public async Task<Session> CreateCheckoutSessionAsync(
            decimal amount,
            string currency,
            string successUrl,
            string cancelUrl,
            string bookingReference,
            List<string> paymentMethodTypes) // <--- Added this parameter
        {
            var options = new SessionCreateOptions
            {
                // 2. Use the dynamic list passed from Controller
                PaymentMethodTypes = paymentMethodTypes,

                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(amount * 100),
                            Currency = currency,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "FlyEase Travel Booking",
                                Description = $"Booking Ref: {bookingReference}"
                            },
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
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

        public async Task<Refund> RefundPaymentAsync(string paymentIntentId)
        {
            var options = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
            };

            var service = new RefundService();
            return await service.CreateAsync(options);
        }
    }
}