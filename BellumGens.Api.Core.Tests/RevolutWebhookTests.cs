using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BellumGens.Api.Core.Tests
{
    public class RevolutWebhookTests
    {
        private const string Secret = "wsk_test_secret";

        private static RevolutPaymentProvider CreateProvider(string? webhookSecret = Secret)
        {
            return new RevolutPaymentProvider(new HttpClient(), TestUtils.CreateRevolutOptions(webhookSecret: webhookSecret),
                TestUtils.CreateMockLogger<RevolutPaymentProvider>().Object);
        }

        private static string Timestamp(DateTimeOffset now)
        {
            return now.ToUnixTimeMilliseconds().ToString();
        }

        [Fact]
        public void VerifySignature_ValidSignature_ReturnsTrue()
        {
            var now = DateTimeOffset.UtcNow;
            string body = "{\"event\":\"ORDER_COMPLETED\",\"order_id\":\"abc\"}";
            string timestamp = Timestamp(now);
            string signature = RevolutPaymentProvider.ComputeSignature(body, timestamp, Secret);

            Assert.True(RevolutPaymentProvider.VerifySignature(body, signature, timestamp, now, Secret, TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void VerifySignature_KnownVector_MatchesHmacSha256()
        {
            // HMAC-SHA256("v1.1683650202360.{"a":1}", "secret") computed independently.
            string body = "{\"a\":1}";
            string timestamp = "1683650202360";
            string expected = "v1=" + Convert.ToHexString(
                System.Security.Cryptography.HMACSHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes("secret"),
                    System.Text.Encoding.UTF8.GetBytes("v1.1683650202360." + body))).ToLowerInvariant();

            Assert.Equal(expected, RevolutPaymentProvider.ComputeSignature(body, timestamp, "secret"));
        }

        [Fact]
        public void VerifySignature_TamperedBody_ReturnsFalse()
        {
            var now = DateTimeOffset.UtcNow;
            string timestamp = Timestamp(now);
            string signature = RevolutPaymentProvider.ComputeSignature("{\"amount\":100}", timestamp, Secret);

            Assert.False(RevolutPaymentProvider.VerifySignature("{\"amount\":1}", signature, timestamp, now, Secret, TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void VerifySignature_StaleTimestamp_ReturnsFalse()
        {
            var now = DateTimeOffset.UtcNow;
            string body = "{}";
            string timestamp = Timestamp(now.AddMinutes(-6));
            string signature = RevolutPaymentProvider.ComputeSignature(body, timestamp, Secret);

            Assert.False(RevolutPaymentProvider.VerifySignature(body, signature, timestamp, now, Secret, TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void VerifySignature_RotatedSecrets_AcceptsAnyMatchingValue()
        {
            var now = DateTimeOffset.UtcNow;
            string body = "{}";
            string timestamp = Timestamp(now);
            string old = RevolutPaymentProvider.ComputeSignature(body, timestamp, "old-secret");
            string current = RevolutPaymentProvider.ComputeSignature(body, timestamp, Secret);

            Assert.True(RevolutPaymentProvider.VerifySignature(body, $"{old},{current}", timestamp, now, Secret, TimeSpan.FromMinutes(5)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void VerifySignature_WithoutConfiguredSecret_ReturnsFalse(string? secret)
        {
            var now = DateTimeOffset.UtcNow;
            string timestamp = Timestamp(now);
            string signature = RevolutPaymentProvider.ComputeSignature("{}", timestamp, "anything");

            Assert.False(RevolutPaymentProvider.VerifySignature("{}", signature, timestamp, now, secret!, TimeSpan.FromMinutes(5)));
        }

        [Theory]
        [InlineData("not-hex")]
        [InlineData("v2=abcd")]
        [InlineData("garbage")]
        public void VerifySignature_MalformedHeader_ReturnsFalse(string header)
        {
            var now = DateTimeOffset.UtcNow;

            Assert.False(RevolutPaymentProvider.VerifySignature("{}", header, Timestamp(now), now, Secret, TimeSpan.FromMinutes(5)));
        }

        [Theory]
        [InlineData("ORDER_COMPLETED", PaymentStatus.Completed)]
        [InlineData("ORDER_AUTHORISED", PaymentStatus.Authorised)]
        [InlineData("ORDER_CANCELLED", PaymentStatus.Cancelled)]
        [InlineData("ORDER_FAILED", PaymentStatus.Failed)]
        [InlineData("ORDER_PAYMENT_DECLINED", PaymentStatus.Failed)]
        [InlineData("ORDER_PAYMENT_FAILED", PaymentStatus.Failed)]
        [InlineData("ORDER_PAYMENT_AUTHENTICATED", null)]
        public void MapEvent_MapsKnownEvents(string eventType, PaymentStatus? expected)
        {
            Assert.Equal(expected, RevolutPaymentProvider.MapEvent(eventType));
        }

        [Theory]
        [InlineData("completed", PaymentStatus.Completed)]
        [InlineData("AUTHORISED", PaymentStatus.Authorised)]
        [InlineData("pending", PaymentStatus.Pending)]
        [InlineData("processing", PaymentStatus.Pending)]
        [InlineData("cancelled", PaymentStatus.Cancelled)]
        [InlineData("failed", PaymentStatus.Failed)]
        public void MapState_MapsProviderStates(string state, PaymentStatus expected)
        {
            Assert.Equal(expected, RevolutPaymentProvider.MapState(state));
        }

        [Fact]
        public void ParseWebhook_ReadsSnakeCaseFields()
        {
            var provider = CreateProvider();

            var parsed = provider.ParseWebhook("{\"event\":\"ORDER_COMPLETED\",\"order_id\":\"rev_42\",\"merchant_order_ext_ref\":\"BG-2026-000001\"}");

            Assert.Equal("ORDER_COMPLETED", parsed.EventType);
            Assert.Equal("rev_42", parsed.ProviderOrderId);
            Assert.Equal("BG-2026-000001", parsed.Reference);
            Assert.Equal(PaymentStatus.Completed, parsed.MappedStatus);
        }

        private static ShopWebhooksController CreateController(BellumGensDbContext db, IOrderService orders, RevolutPaymentProvider? provider = null)
        {
            return new ShopWebhooksController(db, provider ?? CreateProvider(), orders, TestUtils.CreateMockLogger<ShopWebhooksController>().Object);
        }

        [Fact]
        public async Task Handle_InvalidSignature_ReturnsUnauthorizedAndChangesNothing()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db, TestUtils.CreateOrderService(db));
            var now = DateTimeOffset.UtcNow;

            var result = await controller.HandleAsync("{\"event\":\"ORDER_COMPLETED\",\"order_id\":\"rev_1\"}", "v1=00", Timestamp(now), now);

            Assert.IsType<UnauthorizedResult>(result);
            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        }

        [Fact]
        public async Task Handle_OrderCompleted_MarksOrderPaid_AndIsIdempotent()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            order.Items.Add(new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Umbrella", UnitPrice = 30m, Quantity = 1, LineTotal = 30m });
            var payment = new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m };
            order.Payments.Add(payment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var email = TestUtils.CreateMockEmailSender();
            var controller = CreateController(db, TestUtils.CreateOrderService(db, email));
            var now = DateTimeOffset.UtcNow;
            string body = "{\"event\":\"ORDER_COMPLETED\",\"order_id\":\"rev_1\",\"merchant_order_ext_ref\":\"" + order.OrderNumber + "\"}";
            string timestamp = Timestamp(now);
            string signature = RevolutPaymentProvider.ComputeSignature(body, timestamp, Secret);

            var first = await controller.HandleAsync(body, signature, timestamp, now);
            var second = await controller.HandleAsync(body, signature, timestamp, now);

            Assert.IsType<OkResult>(first);
            Assert.IsType<OkResult>(second);
            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.Equal(PaymentStatus.Completed, payment.Status);
            Assert.Equal(body, payment.LastPayload);
            email.Verify(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Handle_AuthorisedThenCompleted_EndsPaid()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            var payment = new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m };
            order.Payments.Add(payment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db, TestUtils.CreateOrderService(db));
            var now = DateTimeOffset.UtcNow;
            string timestamp = Timestamp(now);

            string authorised = "{\"event\":\"ORDER_AUTHORISED\",\"order_id\":\"rev_1\"}";
            await controller.HandleAsync(authorised, RevolutPaymentProvider.ComputeSignature(authorised, timestamp, Secret), timestamp, now);
            Assert.Equal(PaymentStatus.Authorised, payment.Status);
            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);

            string completed = "{\"event\":\"ORDER_COMPLETED\",\"order_id\":\"rev_1\"}";
            await controller.HandleAsync(completed, RevolutPaymentProvider.ComputeSignature(completed, timestamp, Secret), timestamp, now);
            Assert.Equal(PaymentStatus.Completed, payment.Status);
            Assert.Equal(OrderStatus.Paid, order.Status);
        }

        [Fact]
        public async Task Handle_UnknownProviderOrder_ReturnsOk()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(db, TestUtils.CreateOrderService(db));
            var now = DateTimeOffset.UtcNow;
            string body = "{\"event\":\"ORDER_COMPLETED\",\"order_id\":\"rev_unknown\"}";
            string timestamp = Timestamp(now);

            var result = await controller.HandleAsync(body, RevolutPaymentProvider.ComputeSignature(body, timestamp, Secret), timestamp, now);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task Handle_MalformedBody_ReturnsBadRequest()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(db, TestUtils.CreateOrderService(db));
            var now = DateTimeOffset.UtcNow;
            string body = "{not json";
            string timestamp = Timestamp(now);

            var result = await controller.HandleAsync(body, RevolutPaymentProvider.ComputeSignature(body, timestamp, Secret), timestamp, now);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Handle_UnmappedEvent_RecordsEventTypeOnly()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            var payment = new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m };
            order.Payments.Add(payment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var orders = new Mock<IOrderService>(MockBehavior.Strict);
            var controller = CreateController(db, orders.Object);
            var now = DateTimeOffset.UtcNow;
            string body = "{\"event\":\"ORDER_PAYMENT_AUTHENTICATED\",\"order_id\":\"rev_1\"}";
            string timestamp = Timestamp(now);

            var result = await controller.HandleAsync(body, RevolutPaymentProvider.ComputeSignature(body, timestamp, Secret), timestamp, now);

            Assert.IsType<OkResult>(result);
            Assert.Equal("ORDER_PAYMENT_AUTHENTICATED", payment.LastEventType);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
        }

        [Fact]
        public void Provider_IsConfigured_OnlyWithSecretKey()
        {
            var configured = new RevolutPaymentProvider(new HttpClient(), TestUtils.CreateRevolutOptions("sk_live"), TestUtils.CreateMockLogger<RevolutPaymentProvider>().Object);
            var unconfigured = new RevolutPaymentProvider(new HttpClient(), TestUtils.CreateRevolutOptions(secretKey: null), TestUtils.CreateMockLogger<RevolutPaymentProvider>().Object);

            Assert.True(configured.IsConfigured);
            Assert.False(unconfigured.IsConfigured);
        }

        [Fact]
        public async Task Provider_CreateOrder_WithoutSecret_Throws()
        {
            var provider = new RevolutPaymentProvider(new HttpClient(), TestUtils.CreateRevolutOptions(secretKey: null), TestUtils.CreateMockLogger<RevolutPaymentProvider>().Object);

            await Assert.ThrowsAsync<PaymentProviderException>(() => provider.CreateOrderAsync(new PaymentOrderRequest { AmountMinor = 100, Currency = "EUR" }));
        }
    }
}
