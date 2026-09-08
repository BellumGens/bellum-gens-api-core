using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Moq;

namespace BellumGens.Api.Core.Tests
{
    public class OrderServiceTests
    {
        private static CartLineRequest Line(Product product, int quantity = 1, ProductVariant? variant = null)
        {
            return new CartLineRequest { ProductId = product.Id, VariantId = variant?.Id, Quantity = quantity };
        }

        [Fact]
        public async Task Quote_AppliesPromoAndCourierShipping()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var umbrella = TestUtils.CreateProduct("Umbrella", 15m);
            var pen = TestUtils.CreateProduct("Pen", 10m, type: ProductType.Pen);
            db.Products.AddRange(umbrella, pen);
            db.PromoCodes.Add(new Promo { Code = "SAVE10", Discount = 0.10m });
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest
            {
                Lines = new List<CartLineRequest> { Line(umbrella, 2), Line(pen) },
                PromoCode = "save10"
            });

            Assert.True(quote.IsValid);
            Assert.Equal(40m, quote.Subtotal);
            Assert.True(quote.PromoApplied);
            Assert.Equal("SAVE10", quote.PromoCode);
            Assert.Equal(4m, quote.DiscountTotal);
            Assert.Equal(6m, quote.ShippingCost);
            Assert.False(quote.FreeShipping);
            Assert.Equal(42m, quote.Total);
        }

        [Fact]
        public async Task Quote_FreeShippingAtExactThreshold()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct("Umbrella", 25m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest { Lines = new List<CartLineRequest> { Line(product, 2) } });

            Assert.Equal(50m, quote.Subtotal);
            Assert.True(quote.FreeShipping);
            Assert.Equal(0m, quote.ShippingCost);
            Assert.Equal(50m, quote.Total);
        }

        [Fact]
        public async Task Quote_EventPickup_HasNoShipping()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct("Umbrella", 15m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest
            {
                Lines = new List<CartLineRequest> { Line(product) },
                DeliveryMethod = DeliveryMethod.EventPickup
            });

            Assert.Equal(0m, quote.ShippingCost);
            Assert.Equal(15m, quote.Total);
        }

        [Fact]
        public async Task Quote_ProductDiscountRoundsToCents()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct("Pin", 19.99m, type: ProductType.Pin, discountPercentage: 15m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest { Lines = new List<CartLineRequest> { Line(product, 3) } });

            Assert.Equal(16.99m, quote.Lines[0].UnitPrice);
            Assert.Equal(50.97m, quote.Lines[0].LineTotal);
            Assert.Equal(50.97m, quote.Subtotal);
        }

        [Theory]
        [InlineData("expired")]
        [InlineData("inactive")]
        [InlineData("used-up")]
        public async Task Quote_UnusablePromo_ReportsProblemAndDoesNotDiscount(string kind)
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct("Umbrella", 20m);
            db.Products.Add(product);
            var promo = new Promo { Code = "PROMO", Discount = 0.5m };
            switch (kind)
            {
                case "expired": promo.Expiration = DateTimeOffset.Now.AddDays(-1); break;
                case "inactive": promo.Active = false; break;
                case "used-up": promo.UsageLimit = 1; promo.TimesUsed = 1; break;
            }
            db.PromoCodes.Add(promo);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest { Lines = new List<CartLineRequest> { Line(product) }, PromoCode = "PROMO" });

            Assert.True(quote.IsValid);
            Assert.False(quote.PromoApplied);
            Assert.Equal("Promo code has expired.", quote.PromoProblem);
            Assert.Equal(0m, quote.DiscountTotal);
        }

        [Fact]
        public async Task Quote_UnknownPromo_ReportsInvalid()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest { Lines = new List<CartLineRequest> { Line(product) }, PromoCode = "NOPE" });

            Assert.Equal("Promo code is invalid.", quote.PromoProblem);
        }

        [Fact]
        public async Task Quote_PromoBelowMinimumOrder_ReportsProblem()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct("Umbrella", 20m);
            db.Products.Add(product);
            db.PromoCodes.Add(new Promo { Code = "BIG", Discount = 0.2m, MinimumOrderTotal = 100m });
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest { Lines = new List<CartLineRequest> { Line(product) }, PromoCode = "BIG" });

            Assert.False(quote.PromoApplied);
            Assert.Contains("minimum order", quote.PromoProblem);
        }

        [Fact]
        public async Task Quote_BrandRestrictedPromo_DiscountsOnlyMatchingLines()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var eblJersey = TestUtils.CreateJersey("EBL Jersey", 30m, Brand.EBLeague);
            var bgUmbrella = TestUtils.CreateProduct("Umbrella", 20m, brand: Brand.BellumGens);
            db.Products.AddRange(eblJersey, bgUmbrella);
            db.PromoCodes.Add(new Promo { Code = "EBL", Discount = 0.5m, Brand = Brand.EBLeague });
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);
            var maleL = eblJersey.Variants.First(v => v.Cut == JerseyCut.Male);

            var quote = await service.QuoteAsync(new QuoteRequest
            {
                Lines = new List<CartLineRequest> { Line(eblJersey, 1, maleL), Line(bgUmbrella) },
                PromoCode = "EBL"
            });

            Assert.True(quote.PromoApplied);
            Assert.Equal(15m, quote.DiscountTotal);
            Assert.Equal(50m, quote.Subtotal);
            Assert.Equal(35m + 6m, quote.Total);
        }

        [Fact]
        public async Task Quote_ProductWithVariants_RequiresVariant()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var jersey = TestUtils.CreateJersey();
            db.Products.Add(jersey);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest { Lines = new List<CartLineRequest> { Line(jersey) } });

            Assert.False(quote.IsValid);
            Assert.Equal("Please choose an option for this product.", quote.Lines[0].Problem);
        }

        [Fact]
        public async Task Quote_StockIsCheckedAcrossLinesForTheSameVariant()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var jersey = TestUtils.CreateJersey();
            db.Products.Add(jersey);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);
            var maleL = jersey.Variants.First(v => v.Cut == JerseyCut.Male);

            var quote = await service.QuoteAsync(new QuoteRequest
            {
                Lines = new List<CartLineRequest> { Line(jersey, 3, maleL), Line(jersey, 3, maleL) }
            });

            Assert.False(quote.IsValid);
            Assert.All(quote.Lines, l => Assert.Equal("Only 5 left in stock.", l.Problem));
        }

        [Fact]
        public async Task Quote_InactiveProduct_ReportsUnavailable()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct(active: false);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var quote = await service.QuoteAsync(new QuoteRequest { Lines = new List<CartLineRequest> { Line(product) } });

            Assert.False(quote.IsValid);
            Assert.Equal("This product is no longer available.", quote.Lines[0].Problem);
        }

        [Fact]
        public async Task Quote_QuantityAboveMaximum_ReportsProblem()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db, configure: o => o.MaxQuantityPerLine = 2);

            var quote = await service.QuoteAsync(new QuoteRequest { Lines = new List<CartLineRequest> { Line(product, 3) } });

            Assert.Equal("Quantity must be between 1 and 2.", quote.Lines[0].Problem);
        }

        [Fact]
        public async Task CreateOrder_ReservesStockAndSnapshotsLines()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var jersey = TestUtils.CreateJersey("EBL Jersey", 30m);
            var umbrella = TestUtils.CreateProduct("Umbrella", 15m, stock: 3);
            db.Products.AddRange(jersey, umbrella);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);
            var femaleM = jersey.Variants.First(v => v.Cut == JerseyCut.Female);

            var order = await service.CreateOrderAsync(TestUtils.CreateOrderRequest(Line(jersey, 1, femaleM), Line(umbrella, 2)), "user1");

            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
            Assert.Equal("user1", order.UserId);
            Assert.Equal(60m, order.Subtotal);
            Assert.Equal(0m, order.ShippingCost);
            Assert.Equal(60m, order.Total);
            Assert.Equal("EUR", order.Currency);
            Assert.True(order.ExpiresOn > DateTimeOffset.Now.AddMinutes(50));
            Assert.Equal(2, order.Items.Count);
            var jerseyLine = order.Items.Single(i => i.ProductId == jersey.Id);
            Assert.Equal("EBL Jersey", jerseyLine.ProductName);
            Assert.Equal("Female / M", jerseyLine.VariantName);
            Assert.Equal(30m, jerseyLine.UnitPrice);
            Assert.Equal(0, femaleM.StockQuantity);
            Assert.Equal(1, umbrella.StockQuantity);
            Assert.StartsWith($"BG-{DateTime.Now.Year}-", order.OrderNumber);
            Assert.Single(db.ShopOrders);
        }

        [Fact]
        public async Task CreateOrder_UnusablePromo_Throws()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);
            var request = TestUtils.CreateOrderRequest(Line(product));
            request.PromoCode = "MISSING";

            var exception = await Assert.ThrowsAsync<ShopValidationException>(() => service.CreateOrderAsync(request, null));

            Assert.Contains("Promo code is invalid.", exception.Problems);
            Assert.Empty(db.ShopOrders);
        }

        [Fact]
        public async Task CreateOrder_SoldOut_ThrowsWithQuote()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct(stock: 0);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            var exception = await Assert.ThrowsAsync<ShopValidationException>(() => service.CreateOrderAsync(TestUtils.CreateOrderRequest(Line(product)), null));

            Assert.NotNull(exception.Quote);
            Assert.Equal("Sold out.", exception.Quote!.Lines[0].Problem);
            Assert.Equal(0, product.StockQuantity);
        }

        [Fact]
        public async Task ApplyPaymentStatus_Completed_MarksPaidOnce_SendsOneEmail_CountsPromo()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var promo = new Promo { Code = "SAVE10", Discount = 0.1m };
            db.PromoCodes.Add(promo);
            var order = TestUtils.CreateShopOrder();
            order.PromoCode = "SAVE10";
            order.Items.Add(new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Umbrella", UnitPrice = 30m, Quantity = 1, LineTotal = 30m });
            var payment = new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m };
            order.Payments.Add(payment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var email = TestUtils.CreateMockEmailSender();
            var service = TestUtils.CreateOrderService(db, email);

            bool first = await service.ApplyPaymentStatusAsync(payment, PaymentStatus.Completed, "ORDER_COMPLETED", "{}");
            bool second = await service.ApplyPaymentStatusAsync(payment, PaymentStatus.Completed, "ORDER_COMPLETED", "{}");

            Assert.True(first);
            Assert.False(second);
            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.NotNull(order.PaidOn);
            Assert.Equal(PaymentStatus.Completed, payment.Status);
            Assert.Equal("ORDER_COMPLETED", payment.LastEventType);
            Assert.Equal(1, promo.TimesUsed);
            email.Verify(m => m.SendEmailAsync("customer@example.com", It.Is<string>(s => s.Contains(order.OrderNumber)), It.Is<string>(b => b.Contains("Umbrella"))), Times.Once);
        }

        [Fact]
        public async Task ApplyPaymentStatus_Failed_KeepsOrderAwaitingPayment()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            var payment = new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m };
            order.Payments.Add(payment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var email = TestUtils.CreateMockEmailSender();
            var service = TestUtils.CreateOrderService(db, email);

            await service.ApplyPaymentStatusAsync(payment, PaymentStatus.Failed, "ORDER_PAYMENT_DECLINED", null);

            Assert.Equal(PaymentStatus.Failed, payment.Status);
            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
            email.Verify(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ApplyPaymentStatus_NeverRegressesACompletedPayment()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            var payment = new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m, Status = PaymentStatus.Completed };
            order.Payments.Add(payment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            bool changed = await service.ApplyPaymentStatusAsync(payment, PaymentStatus.Failed, "ORDER_PAYMENT_FAILED", null);

            Assert.False(changed);
            Assert.Equal(PaymentStatus.Completed, payment.Status);
            Assert.Equal(OrderStatus.Paid, order.Status);
        }

        [Fact]
        public async Task Cancel_ReleasesReservedStock()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var jersey = TestUtils.CreateJersey();
            var umbrella = TestUtils.CreateProduct("Umbrella", 15m, stock: 2);
            db.Products.AddRange(jersey, umbrella);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);
            var maleL = jersey.Variants.First(v => v.Cut == JerseyCut.Male);
            var order = await service.CreateOrderAsync(TestUtils.CreateOrderRequest(Line(jersey, 2, maleL), Line(umbrella, 2)), null);
            Assert.Equal(3, maleL.StockQuantity);
            Assert.Equal(0, umbrella.StockQuantity);

            await service.CancelAsync(order, "Test");

            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(5, maleL.StockQuantity);
            Assert.Equal(2, umbrella.StockQuantity);
        }

        [Fact]
        public async Task Cancel_IgnoresPaidOrders()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            await service.CancelAsync(order, "Test");

            Assert.Equal(OrderStatus.Paid, order.Status);
        }

        [Fact]
        public async Task UpdateStatus_PaidToShipped_SetsShippedOnAndEmailsTracking()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            order.Language = "en";
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var email = TestUtils.CreateMockEmailSender();
            var service = TestUtils.CreateOrderService(db, email);

            await service.UpdateStatusAsync(order, new OrderStatusUpdate { Status = OrderStatus.Shipped, TrackingNumber = "SPD123" });

            Assert.Equal(OrderStatus.Shipped, order.Status);
            Assert.NotNull(order.ShippedOn);
            Assert.Equal("SPD123", order.TrackingNumber);
            email.Verify(m => m.SendEmailAsync("customer@example.com", It.Is<string>(s => s.Contains("shipped")), It.Is<string>(b => b.Contains("SPD123"))), Times.Once);
        }

        [Theory]
        [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Shipped)]
        [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Delivered, OrderStatus.Paid)]
        [InlineData(OrderStatus.Paid, OrderStatus.Refunded)]
        public async Task UpdateStatus_IllegalTransition_Throws(OrderStatus from, OrderStatus to)
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(from);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);

            await Assert.ThrowsAsync<ShopValidationException>(() => service.UpdateStatusAsync(order, new OrderStatusUpdate { Status = to }));

            Assert.Equal(from, order.Status);
        }

        [Fact]
        public async Task UpdateStatus_AwaitingPaymentToCancelled_ReleasesStock()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var umbrella = TestUtils.CreateProduct("Umbrella", 15m, stock: 1);
            db.Products.Add(umbrella);
            await db.SaveChangesAsync();
            var service = TestUtils.CreateOrderService(db);
            var order = await service.CreateOrderAsync(TestUtils.CreateOrderRequest(Line(umbrella)), null);
            Assert.Equal(0, umbrella.StockQuantity);

            await service.UpdateStatusAsync(order, new OrderStatusUpdate { Status = OrderStatus.Cancelled, Note = "Customer asked" });

            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal("Customer asked", order.AdminNote);
            Assert.Equal(1, umbrella.StockQuantity);
        }

        [Fact]
        public void EmailTemplates_UseOrderLanguage()
        {
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            order.Items.Add(new OrderItem { ProductName = "Umbrella", Quantity = 2, UnitPrice = 15m, LineTotal = 30m });
            var options = TestUtils.CreateShopOptions().Value;

            order.Language = "bg";
            var bg = ShopEmailTemplates.OrderConfirmation(order, options);
            order.Language = "en";
            var en = ShopEmailTemplates.OrderConfirmation(order, options);

            Assert.Contains("Поръчка", bg.Subject);
            Assert.Contains("/bg/shop/order/", bg.Body);
            Assert.Contains("Order", en.Subject);
            Assert.Contains("/en/shop/order/", en.Body);
            Assert.Contains("2 × Umbrella", en.Body);
            Assert.Contains("36.00 EUR", en.Body);
        }
    }
}
