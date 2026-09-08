using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.Extensions.Logging;
using Moq;

namespace BellumGens.Api.Core.Tests
{
    public class PaymentExpirySweeperTests
    {
        private static readonly ILogger Logger = TestUtils.CreateMockLogger<PaymentExpirySweeper>().Object;

        [Fact]
        public async Task Sweep_CancelsExpiredUnpaidOrders_AndReleasesStock()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var umbrella = TestUtils.CreateProduct("Umbrella", 15m, stock: 0);
            db.Products.Add(umbrella);
            var expired = TestUtils.CreateShopOrder(expiresOn: DateTimeOffset.Now.AddMinutes(-5));
            expired.Items.Add(new OrderItem { ProductId = umbrella.Id, ProductName = "Umbrella", UnitPrice = 15m, Quantity = 1, LineTotal = 15m });
            expired.Payments.Add(new Payment { OrderId = expired.Id, ProviderOrderId = "rev_1", Amount = 21m, Status = PaymentStatus.Failed });
            db.ShopOrders.Add(expired);
            await db.SaveChangesAsync();
            var provider = TestUtils.CreateMockPaymentProvider();

            int cancelled = await PaymentExpirySweeper.SweepAsync(db, TestUtils.CreateOrderService(db), provider.Object, DateTimeOffset.Now, Logger, CancellationToken.None);

            Assert.Equal(1, cancelled);
            Assert.Equal(OrderStatus.Cancelled, expired.Status);
            Assert.Equal(1, umbrella.StockQuantity);
            provider.Verify(p => p.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Sweep_LeavesUnexpiredAndPaidOrdersAlone()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var fresh = TestUtils.CreateShopOrder(expiresOn: DateTimeOffset.Now.AddMinutes(30));
            var paid = TestUtils.CreateShopOrder(OrderStatus.Paid, DateTimeOffset.Now.AddMinutes(-30));
            db.ShopOrders.AddRange(fresh, paid);
            await db.SaveChangesAsync();

            int cancelled = await PaymentExpirySweeper.SweepAsync(db, TestUtils.CreateOrderService(db), TestUtils.CreateMockPaymentProvider().Object, DateTimeOffset.Now, Logger, CancellationToken.None);

            Assert.Equal(0, cancelled);
            Assert.Equal(OrderStatus.AwaitingPayment, fresh.Status);
            Assert.Equal(OrderStatus.Paid, paid.Status);
        }

        [Fact]
        public async Task Sweep_PendingPaymentCompletedAtProvider_MarksOrderPaidInsteadOfCancelling()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(expiresOn: DateTimeOffset.Now.AddMinutes(-5));
            var payment = new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m, Status = PaymentStatus.Pending };
            order.Payments.Add(payment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var provider = TestUtils.CreateMockPaymentProvider();
            provider.Setup(p => p.GetOrderAsync("rev_1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentOrderResult { ProviderOrderId = "rev_1", Status = PaymentStatus.Completed, RawState = "completed" });

            int cancelled = await PaymentExpirySweeper.SweepAsync(db, TestUtils.CreateOrderService(db), provider.Object, DateTimeOffset.Now, Logger, CancellationToken.None);

            Assert.Equal(0, cancelled);
            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.Equal(PaymentStatus.Completed, payment.Status);
        }

        [Fact]
        public async Task Sweep_PendingPaymentStillPendingAtProvider_Cancels()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(expiresOn: DateTimeOffset.Now.AddMinutes(-5));
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m, Status = PaymentStatus.Pending });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var provider = TestUtils.CreateMockPaymentProvider();
            provider.Setup(p => p.GetOrderAsync("rev_1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentOrderResult { ProviderOrderId = "rev_1", Status = PaymentStatus.Pending, RawState = "pending" });

            int cancelled = await PaymentExpirySweeper.SweepAsync(db, TestUtils.CreateOrderService(db), provider.Object, DateTimeOffset.Now, Logger, CancellationToken.None);

            Assert.Equal(1, cancelled);
            Assert.Equal(OrderStatus.Cancelled, order.Status);
        }

        [Fact]
        public async Task Sweep_ProviderError_SkipsOrderForNextSweep()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(expiresOn: DateTimeOffset.Now.AddMinutes(-5));
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m, Status = PaymentStatus.Pending });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var provider = TestUtils.CreateMockPaymentProvider();
            provider.Setup(p => p.GetOrderAsync("rev_1", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new PaymentProviderException("Revolut returned 500."));

            int cancelled = await PaymentExpirySweeper.SweepAsync(db, TestUtils.CreateOrderService(db), provider.Object, DateTimeOffset.Now, Logger, CancellationToken.None);

            Assert.Equal(0, cancelled);
            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        }
    }
}
