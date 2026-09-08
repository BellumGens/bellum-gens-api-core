using BellumGens.Api.Core.Models;
using System;

namespace BellumGens.Api.Core.Providers
{
    public class PaymentOrderRequest
    {
        /// <summary>Amount in minor units (cents).</summary>
        public long AmountMinor { get; set; }
        public string Currency { get; set; }
        public string Description { get; set; }
        public string CustomerEmail { get; set; }
        /// <summary>Merchant reference shown in the provider dashboard and echoed in webhooks.</summary>
        public string Reference { get; set; }
        public string RedirectUrl { get; set; }
        /// <summary>ISO 8601 duration after which an unpaid provider order expires, e.g. PT1H. Null to omit.</summary>
        public string ExpirePendingAfter { get; set; }
    }

    public class PaymentOrderResult
    {
        public string ProviderOrderId { get; set; }
        public string Token { get; set; }
        public string CheckoutUrl { get; set; }
        public PaymentStatus Status { get; set; }
        /// <summary>The provider's own state string, kept for logging.</summary>
        public string RawState { get; set; }
    }

    public class WebhookRegistrationResult
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string[] Events { get; set; }
        /// <summary>Store this as revolut:webhookSigningSecret. It is only returned once.</summary>
        public string SigningSecret { get; set; }
    }

    public class WebhookEvent
    {
        public string EventType { get; set; }
        public string ProviderOrderId { get; set; }
        public string Reference { get; set; }
        /// <summary>Null when the event carries no payment state change we act on.</summary>
        public PaymentStatus? MappedStatus { get; set; }
    }

    public class PaymentProviderException : Exception
    {
        public PaymentProviderException(string message) : base(message) { }
        public PaymentProviderException(string message, Exception inner) : base(message, inner) { }
    }
}
