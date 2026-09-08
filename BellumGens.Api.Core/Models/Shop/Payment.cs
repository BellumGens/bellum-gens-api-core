using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
    public class Payment
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid OrderId { get; set; }

        public PaymentProvider Provider { get; set; } = PaymentProvider.Revolut;

        /// <summary>The provider's order id. Webhooks are resolved by this value.</summary>
        public string ProviderOrderId { get; set; }

        /// <summary>Short-lived token used to initialise the provider's checkout widget.</summary>
        [JsonIgnore]
        public string ProviderToken { get; set; }

        public string CheckoutUrl { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "EUR";

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset UpdatedOn { get; set; } = DateTimeOffset.Now;

        public string LastEventType { get; set; }

        [JsonIgnore]
        public string LastPayload { get; set; }

        [JsonIgnore]
        [ForeignKey("OrderId")]
        public virtual ShopOrder Order { get; set; }
    }
}
