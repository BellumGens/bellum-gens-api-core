using BellumGens.Api.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    /// <summary>
    /// Revolut Merchant API client. Orders: POST /api/orders, GET /api/orders/{id}, POST /api/orders/{id}/refund.
    /// Webhooks: POST /api/1.0/webhooks. Signatures: HMAC-SHA256 over "v1.{timestamp}.{raw body}".
    /// </summary>
    public class RevolutPaymentProvider : IPaymentProvider
    {
        public const string SignatureHeader = "Revolut-Signature";
        public const string TimestampHeader = "Revolut-Request-Timestamp";

        public static readonly string[] SubscribedEvents =
        [
            "ORDER_COMPLETED",
            "ORDER_AUTHORISED",
            "ORDER_CANCELLED",
            "ORDER_FAILED",
            "ORDER_PAYMENT_DECLINED",
            "ORDER_PAYMENT_FAILED"
        ];

        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;
        private readonly RevolutOptions _options;
        private readonly ILogger<RevolutPaymentProvider> _logger;

        public RevolutPaymentProvider(HttpClient http, IOptions<RevolutOptions> options, ILogger<RevolutPaymentProvider> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;

            if (_http.BaseAddress == null && !string.IsNullOrEmpty(_options.BaseUrl))
            {
                _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            }
            if (!string.IsNullOrEmpty(_options.SecretKey))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
            }
            _http.DefaultRequestHeaders.Remove("Revolut-Api-Version");
            _http.DefaultRequestHeaders.Add("Revolut-Api-Version", _options.ApiVersion);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public PaymentProvider Provider => PaymentProvider.Revolut;

        public bool IsConfigured => !string.IsNullOrEmpty(_options.SecretKey);

        public async Task<PaymentOrderResult> CreateOrderAsync(PaymentOrderRequest request, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var body = new CreateOrderBody
            {
                Amount = request.AmountMinor,
                Currency = request.Currency,
                Description = request.Description,
                Customer = string.IsNullOrEmpty(request.CustomerEmail) ? null : new CustomerBody { Email = request.CustomerEmail },
                MerchantOrderData = string.IsNullOrEmpty(request.Reference) ? null : new MerchantOrderDataBody { Reference = request.Reference },
                RedirectUrl = request.RedirectUrl,
                ExpirePendingAfter = request.ExpirePendingAfter
            };

            using HttpResponseMessage response = await _http.PostAsJsonAsync("api/orders", body, _json, cancellationToken);
            OrderResponse order = await ReadAsync<OrderResponse>(response, cancellationToken);
            return Map(order);
        }

        public async Task<PaymentOrderResult> GetOrderAsync(string providerOrderId, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            using HttpResponseMessage response = await _http.GetAsync($"api/orders/{Uri.EscapeDataString(providerOrderId)}", cancellationToken);
            OrderResponse order = await ReadAsync<OrderResponse>(response, cancellationToken);
            return Map(order);
        }

        public async Task<PaymentOrderResult> RefundAsync(string providerOrderId, long amountMinor, string description, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var body = new RefundBody { Amount = amountMinor, Description = description };
            using HttpResponseMessage response = await _http.PostAsJsonAsync($"api/orders/{Uri.EscapeDataString(providerOrderId)}/refund", body, _json, cancellationToken);
            OrderResponse refund = await ReadAsync<OrderResponse>(response, cancellationToken);
            return Map(refund);
        }

        public async Task<WebhookRegistrationResult> RegisterWebhookAsync(string url, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var body = new WebhookBody { Url = url, Events = SubscribedEvents };
            using HttpResponseMessage response = await _http.PostAsJsonAsync("api/1.0/webhooks", body, _json, cancellationToken);
            WebhookResponse created = await ReadAsync<WebhookResponse>(response, cancellationToken);
            return new WebhookRegistrationResult
            {
                Id = created.Id,
                Url = created.Url,
                Events = created.Events,
                SigningSecret = created.SigningSecret
            };
        }

        public bool VerifyWebhookSignature(string rawBody, string signatureHeader, string timestampHeader, DateTimeOffset now)
        {
            return VerifySignature(rawBody, signatureHeader, timestampHeader, now, _options.WebhookSigningSecret,
                TimeSpan.FromMinutes(_options.WebhookToleranceMinutes));
        }

        public WebhookEvent ParseWebhook(string rawBody)
        {
            WebhookPayload payload = JsonSerializer.Deserialize<WebhookPayload>(rawBody, _json);
            if (payload == null)
            {
                return null;
            }
            return new WebhookEvent
            {
                EventType = payload.Event,
                ProviderOrderId = payload.OrderId,
                Reference = payload.MerchantOrderExtRef,
                MappedStatus = MapEvent(payload.Event)
            };
        }

        /// <summary>
        /// Verifies a Revolut webhook signature. The signed payload is "v1.{timestamp}.{raw body}" hashed with
        /// HMAC-SHA256 using the webhook signing secret. The header may carry several comma separated
        /// "v1=hex" values during secret rotation; any match is accepted.
        /// </summary>
        public static bool VerifySignature(string rawBody, string signatureHeader, string timestampHeader, DateTimeOffset now, string secret, TimeSpan tolerance)
        {
            if (rawBody == null || string.IsNullOrEmpty(secret) || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(timestampHeader))
            {
                return false;
            }
            if (!long.TryParse(timestampHeader.Trim(), out long unixMs))
            {
                return false;
            }
            DateTimeOffset sent = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            if ((now - sent).Duration() > tolerance)
            {
                return false;
            }

            byte[] expected = ComputeSignatureBytes(rawBody, timestampHeader.Trim(), secret);
            foreach (string part in signatureHeader.Split(','))
            {
                string candidate = part.Trim();
                if (!candidate.StartsWith("v1=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                byte[] provided;
                try
                {
                    provided = Convert.FromHexString(candidate[3..]);
                }
                catch (FormatException)
                {
                    continue;
                }
                if (CryptographicOperations.FixedTimeEquals(provided, expected))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Builds the "v1=hex" signature value for a body and timestamp. Used by tests and tooling.</summary>
        public static string ComputeSignature(string rawBody, string timestamp, string secret)
        {
            return "v1=" + Convert.ToHexString(ComputeSignatureBytes(rawBody, timestamp, secret)).ToLowerInvariant();
        }

        public static PaymentStatus MapState(string state)
        {
            return state?.ToLowerInvariant() switch
            {
                "completed" => PaymentStatus.Completed,
                "authorised" => PaymentStatus.Authorised,
                "cancelled" => PaymentStatus.Cancelled,
                "failed" => PaymentStatus.Failed,
                _ => PaymentStatus.Pending
            };
        }

        public static PaymentStatus? MapEvent(string eventType)
        {
            return eventType?.ToUpperInvariant() switch
            {
                "ORDER_COMPLETED" => PaymentStatus.Completed,
                "ORDER_AUTHORISED" => PaymentStatus.Authorised,
                "ORDER_CANCELLED" => PaymentStatus.Cancelled,
                "ORDER_FAILED" or "ORDER_PAYMENT_FAILED" or "ORDER_PAYMENT_DECLINED" => PaymentStatus.Failed,
                _ => null
            };
        }

        private static byte[] ComputeSignatureBytes(string rawBody, string timestamp, string secret)
        {
            byte[] key = Encoding.UTF8.GetBytes(secret);
            byte[] payload = Encoding.UTF8.GetBytes($"v1.{timestamp}.{rawBody}");
            return HMACSHA256.HashData(key, payload);
        }

        private static PaymentOrderResult Map(OrderResponse order)
        {
            return new PaymentOrderResult
            {
                ProviderOrderId = order.Id,
                Token = order.Token,
                CheckoutUrl = order.CheckoutUrl,
                Status = MapState(order.State),
                RawState = order.State
            };
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new PaymentProviderException("Revolut secret key is not configured (revolut:secretKey).");
            }
        }

        private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Revolut API call to {Url} failed with {Status}: {Body}", response.RequestMessage?.RequestUri, (int)response.StatusCode, content);
                throw new PaymentProviderException($"Revolut returned {(int)response.StatusCode}.");
            }
            T parsed = JsonSerializer.Deserialize<T>(content, _json);
            return parsed ?? throw new PaymentProviderException("Revolut returned an empty response.");
        }

        private sealed class CreateOrderBody
        {
            public long Amount { get; set; }
            public string Currency { get; set; }
            public string Description { get; set; }
            public CustomerBody Customer { get; set; }
            public MerchantOrderDataBody MerchantOrderData { get; set; }
            public string RedirectUrl { get; set; }
            public string ExpirePendingAfter { get; set; }
        }

        private sealed class CustomerBody
        {
            public string Email { get; set; }
        }

        private sealed class MerchantOrderDataBody
        {
            public string Reference { get; set; }
        }

        private sealed class RefundBody
        {
            public long Amount { get; set; }
            public string Description { get; set; }
        }

        private sealed class OrderResponse
        {
            public string Id { get; set; }
            public string Token { get; set; }
            public string State { get; set; }
            public string CheckoutUrl { get; set; }
        }

        private sealed class WebhookBody
        {
            public string Url { get; set; }
            public string[] Events { get; set; }
        }

        private sealed class WebhookResponse
        {
            public string Id { get; set; }
            public string Url { get; set; }
            public string[] Events { get; set; }
            public string SigningSecret { get; set; }
        }

        private sealed class WebhookPayload
        {
            public string Event { get; set; }
            public string OrderId { get; set; }
            public string MerchantOrderExtRef { get; set; }
        }
    }
}
