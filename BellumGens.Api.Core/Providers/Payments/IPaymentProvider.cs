using BellumGens.Api.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public interface IPaymentProvider
    {
        PaymentProvider Provider { get; }

        /// <summary>False until a secret key is configured. Order creation is refused while false.</summary>
        bool IsConfigured { get; }

        Task<PaymentOrderResult> CreateOrderAsync(PaymentOrderRequest request, CancellationToken cancellationToken = default);

        Task<PaymentOrderResult> GetOrderAsync(string providerOrderId, CancellationToken cancellationToken = default);

        Task<PaymentOrderResult> RefundAsync(string providerOrderId, long amountMinor, string description, CancellationToken cancellationToken = default);

        Task<WebhookRegistrationResult> RegisterWebhookAsync(string url, CancellationToken cancellationToken = default);

        bool VerifyWebhookSignature(string rawBody, string signatureHeader, string timestampHeader, DateTimeOffset now);

        /// <summary>Parses a verified webhook body. Throws <see cref="System.Text.Json.JsonException"/> on malformed input.</summary>
        WebhookEvent ParseWebhook(string rawBody);
    }
}
