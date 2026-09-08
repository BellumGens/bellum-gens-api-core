using BellumGens.Api.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    /// <summary>
    /// Periodically cancels orders whose payment window has passed and releases their stock. Before cancelling,
    /// a pending payment is checked once with the provider in case the webhook never arrived.
    /// </summary>
    public class PaymentExpirySweeper : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan _startupDelay = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaymentExpirySweeper> _logger;

        public PaymentExpirySweeper(IServiceScopeFactory scopeFactory, ILogger<PaymentExpirySweeper> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(_startupDelay, stoppingToken);
                using PeriodicTimer timer = new(Interval);
                do
                {
                    try
                    {
                        using IServiceScope scope = _scopeFactory.CreateScope();
                        int cancelled = await SweepAsync(
                            scope.ServiceProvider.GetRequiredService<BellumGensDbContext>(),
                            scope.ServiceProvider.GetRequiredService<IOrderService>(),
                            scope.ServiceProvider.GetRequiredService<IPaymentProvider>(),
                            DateTimeOffset.Now,
                            _logger,
                            stoppingToken);
                        if (cancelled > 0)
                        {
                            _logger.LogInformation("Cancelled {Count} expired unpaid shop orders.", cancelled);
                        }
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        _logger.LogError(e, "Shop payment sweep failed.");
                    }
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down.
            }
        }

        /// <summary>Runs one sweep. Returns the number of orders cancelled.</summary>
        public static async Task<int> SweepAsync(BellumGensDbContext db, IOrderService orders, IPaymentProvider provider, DateTimeOffset now, ILogger logger, CancellationToken cancellationToken)
        {
            List<ShopOrder> expired = await db.ShopOrders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .Where(o => o.Status == OrderStatus.AwaitingPayment && o.ExpiresOn < now)
                .OrderBy(o => o.ExpiresOn)
                .Take(100)
                .ToListAsync(cancellationToken);

            int cancelled = 0;
            foreach (ShopOrder order in expired)
            {
                Payment latest = order.Payments.OrderByDescending(p => p.CreatedOn).FirstOrDefault();
                bool pending = latest != null && (latest.Status == PaymentStatus.Pending || latest.Status == PaymentStatus.Authorised);
                if (pending && provider.IsConfigured && !string.IsNullOrEmpty(latest.ProviderOrderId))
                {
                    try
                    {
                        PaymentOrderResult remote = await provider.GetOrderAsync(latest.ProviderOrderId, cancellationToken);
                        if (remote.Status == PaymentStatus.Completed)
                        {
                            await orders.ApplyPaymentStatusAsync(latest, PaymentStatus.Completed, "SWEEP:" + remote.RawState, null, cancellationToken);
                            continue;
                        }
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // Leave the order for the next sweep rather than cancel something the customer may have paid.
                        logger?.LogWarning(e, "Could not check payment {PaymentId} before expiring order {OrderNumber}.", latest.Id, order.OrderNumber);
                        continue;
                    }
                }

                await orders.CancelAsync(order, "Payment window expired.", cancellationToken);
                cancelled++;
            }
            return cancelled;
        }
    }
}
