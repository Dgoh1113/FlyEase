using FlyEase.ViewModels;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace FlyEase
{
    public static class SessionExtensions
    {
        private const string CustomerInfoKey = "CustomerInfo";
        private const string UserIdKey = "UserId";
        private const string PaymentMethodKey = "PaymentMethod";

        public static void SetCustomerInfo(this ISession session, CustomerInfoViewModel info)
        {
            session.SetString(CustomerInfoKey, JsonSerializer.Serialize(info));
        }

        public static CustomerInfoViewModel GetCustomerInfo(this ISession session)
        {
            var json = session.GetString(CustomerInfoKey);
            return json == null ? null : JsonSerializer.Deserialize<CustomerInfoViewModel>(json);
        }

        public static void SetUserId(this ISession session, int userId)
        {
            session.SetInt32(UserIdKey, userId);
        }

        public static int GetUserId(this ISession session)
        {
            return session.GetInt32(UserIdKey) ?? 0;
        }

        public static void SetPaymentMethod(this ISession session, string method)
        {
            session.SetString(PaymentMethodKey, method);
        }

        public static string GetPaymentMethod(this ISession session)
        {
            return session.GetString(PaymentMethodKey) ?? "card";
        }

        public static void ClearPaymentSession(this ISession session)
        {
            // Keep customer info for potential retry, but clear other payment data
            session.Remove("CurrentDiscountKey");
            session.Remove("PaymentType");
            session.Remove("PaymentMethod");
            session.Remove("BookingDiscounts");
            session.Remove("DiscountSourceKey");
        }
        // Add these to SessionExtensions.cs
        public static void SetStripePaymentIntentId(this ISession session, string paymentIntentId)
        {
            session.SetString("StripePaymentIntentId", paymentIntentId);
        }

        public static string GetStripePaymentIntentId(this ISession session)
        {
            return session.GetString("StripePaymentIntentId");
        }

        public static void SetObject<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? GetObject<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }
    }
}