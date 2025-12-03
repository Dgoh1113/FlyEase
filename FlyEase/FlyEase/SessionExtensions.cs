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
            session.Remove(CustomerInfoKey);
            session.Remove(UserIdKey);
            session.Remove(PaymentMethodKey);
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
    }
}