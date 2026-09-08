using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
    /// <summary>Receives payment provider webhooks. Anonymous by design; every request is authenticated by its signature.</summary>
    [ApiController]
    [Route("api/shop/webhooks")]
    [AllowAnonymous]
    public class ShopWebhooksController : ControllerBase
    {
        private readonly BellumGensDbContext _dbContext;
        private readonly IPaymentProvider _provider;
        private readonly IOrderService _orders;
        private readonly ILogger<ShopWebhooksController> _logger;

        public ShopWebhooksController(BellumGensDbContext dbContext, IPaymentProvider provider, IOrderService orders, ILogger<ShopWebhooksController> logger)
        {
            _dbContext = dbContext;
            _provider = provider;
            _orders = orders;
            _logger = logger;
        }

        [HttpPost("revolut")]
        public async Task<IActionResult> Revolut(CancellationToken cancellationToken)
        {
            string body;
            using (StreamReader reader = new(Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(cancellationToken);
            }
            string signature = Request.Headers[RevolutPaymentProvider.SignatureHeader];
            string timestamp = Request.Headers[RevolutPaymentProvider.TimestampHeader];
            return await HandleAsync(body, signature, timestamp, DateTimeOffset.Now, cancellationToken);
        }

        /// <summary>Verifies and applies one webhook delivery. Split out so it can be exercised without an HTTP context.</summary>
        public async Task<IActionResult> HandleAsync(string rawBody, string signatureHeader, string timestampHeader, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            if (!_provider.VerifyWebhookSignature(rawBody, signatureHeader, timestampHeader, now))
            {
                _logger.LogWarning("Rejected a {Provider} webhook with an invalid signature.", _provider.Provider);
                return Unauthorized();
            }

            WebhookEvent webhook;
            try
            {
                webhook = _provider.ParseWebhook(rawBody);
            }
            catch (JsonException e)
            {
                _logger.LogWarning(e, "Rejected a {Provider} webhook with a malformed body.", _provider.Provider);
                return BadRequest();
            }
            if (webhook == null || string.IsNullOrEmpty(webhook.ProviderOrderId))
            {
                return Ok();
            }

            Payment payment = await _dbContext.Payments
                .Include(p => p.Order)
                .ThenInclude(o => o.Items)
                .FirstOrDefaultAsync(p => p.ProviderOrderId == webhook.ProviderOrderId, cancellationToken);
            if (payment == null)
            {
                _logger.LogInformation("Ignored {Provider} webhook {Event} for unknown order {ProviderOrderId}.", _provider.Provider, webhook.EventType, webhook.ProviderOrderId);
                return Ok();
            }

            if (webhook.MappedStatus == null)
            {
                payment.LastEventType = webhook.EventType;
                payment.UpdatedOn = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Ok();
            }

            bool changed = await _orders.ApplyPaymentStatusAsync(payment, webhook.MappedStatus.Value, webhook.EventType, rawBody, cancellationToken);
            _logger.LogInformation("{Provider} webhook {Event} for order {OrderNumber}: {Result}.",
                _provider.Provider, webhook.EventType, payment.Order?.OrderNumber, changed ? "applied" : "already applied");
            return Ok();
        }
    }
}
