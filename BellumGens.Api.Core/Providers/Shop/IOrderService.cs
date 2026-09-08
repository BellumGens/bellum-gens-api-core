using BellumGens.Api.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public interface IOrderService
    {
        /// <summary>Prices a cart without touching stock.</summary>
        Task<QuoteResult> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken = default);

        /// <summary>Validates, prices and saves an order in AwaitingPayment, reserving stock. Throws <see cref="ShopValidationException"/>.</summary>
        Task<ShopOrder> CreateOrderAsync(CreateOrderRequest request, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies a payment state reported by the provider. Idempotent: states never regress. Marks the order paid
        /// and sends the confirmation email on the first Completed. Returns true when something changed.
        /// </summary>
        Task<bool> ApplyPaymentStatusAsync(Payment payment, PaymentStatus status, string eventType, string payload, CancellationToken cancellationToken = default);

        /// <summary>Cancels an unpaid order and releases its stock. No-op for any other status.</summary>
        Task CancelAsync(ShopOrder order, string reason, CancellationToken cancellationToken = default);

        /// <summary>Admin transition (ship, deliver, cancel). Throws <see cref="ShopValidationException"/> on an illegal transition.</summary>
        Task<ShopOrder> UpdateStatusAsync(ShopOrder order, OrderStatusUpdate update, CancellationToken cancellationToken = default);

        /// <summary>Marks a paid order refunded after the provider accepted the refund.</summary>
        Task MarkRefundedAsync(ShopOrder order, CancellationToken cancellationToken = default);
    }

    public class ShopValidationException : Exception
    {
        public ShopValidationException(string problem)
            : this(new[] { problem }, null)
        {
        }

        public ShopValidationException(IEnumerable<string> problems, QuoteResult quote)
            : base(string.Join(" ", problems))
        {
            Problems = problems.ToList();
            Quote = quote;
        }

        public IReadOnlyList<string> Problems { get; }

        /// <summary>The quote that failed validation, when available, so the client can show per-line problems.</summary>
        public QuoteResult Quote { get; }

        public static ShopValidationException FromQuote(QuoteResult quote)
        {
            List<string> problems = quote.Lines
                .Where(l => l.Problem != null)
                .Select(l => $"{l.ProductName ?? "Product"}: {l.Problem}")
                .ToList();
            if (quote.PromoProblem != null)
            {
                problems.Add(quote.PromoProblem);
            }
            if (problems.Count == 0)
            {
                problems.Add("The order could not be validated.");
            }
            return new ShopValidationException(problems, quote);
        }
    }
}
