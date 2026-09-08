using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BellumGens.Api.Core.Tests
{
    public class ShopControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly Mock<EmailServiceProvider> _email;

        public ShopControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _email = TestUtils.CreateMockEmailSender();
        }

        private ShopController CreateController(BellumGensDbContext db, Mock<IPaymentProvider>? payments = null, Action<ShopOptions>? configure = null)
        {
            var options = TestUtils.CreateShopOptions(configure);
            var orders = new OrderService(db, options, _email.Object, TestUtils.CreateMockLogger<OrderService>().Object);
            var controller = new ShopController(orders, (payments ?? TestUtils.CreateMockPaymentProvider()).Object, TestUtils.CreateMemoryCache(), options,
                _mockUserManager.Object, _mockRoleManager.Object, _mockSignInManager.Object, _email.Object, db,
                TestUtils.CreateMockLogger<ShopController>().Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
            return controller;
        }

        private static CartLineRequest Line(Product product, int quantity = 1, ProductVariant? variant = null)
        {
            return new CartLineRequest { ProductId = product.Id, VariantId = variant?.Id, Quantity = quantity };
        }

        [Fact]
        public async Task GetProducts_ReturnsActiveProductsWithActiveVariantsOnly()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var jersey = TestUtils.CreateJersey();
            jersey.Variants.Add(new ProductVariant { Id = Guid.NewGuid(), Name = "Retired", Active = false });
            var hidden = TestUtils.CreateProduct("Hidden", active: false);
            db.Products.AddRange(jersey, hidden);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.GetProducts();

            var ok = Assert.IsType<OkObjectResult>(result);
            var products = Assert.IsAssignableFrom<List<Product>>(ok.Value);
            var only = Assert.Single(products);
            Assert.Equal(jersey.Id, only.Id);
            Assert.Equal(2, only.Variants.Count);
            Assert.All(only.Variants, v => Assert.True(v.Active));
        }

        [Fact]
        public async Task GetProducts_FiltersByBrandAndType()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            db.Products.AddRange(
                TestUtils.CreateProduct("BG Umbrella", brand: Brand.BellumGens, type: ProductType.Umbrella),
                TestUtils.CreateProduct("EBL Pen", brand: Brand.EBLeague, type: ProductType.Pen),
                TestUtils.CreateProduct("BGE Pen", brand: Brand.BGEStaraZagora, type: ProductType.Pen));
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var byBrand = Assert.IsAssignableFrom<List<Product>>(Assert.IsType<OkObjectResult>(await controller.GetProducts(brand: Brand.EBLeague)).Value);
            var byType = Assert.IsAssignableFrom<List<Product>>(Assert.IsType<OkObjectResult>(await controller.GetProducts(type: ProductType.Pen)).Value);

            Assert.Equal("EBL Pen", Assert.Single(byBrand).Name);
            Assert.Equal(2, byType.Count);
        }

        [Fact]
        public async Task GetProduct_BySlug_ReturnsProductOrNotFound()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct("Rain Umbrella");
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var found = await controller.GetProduct("RAIN-UMBRELLA");
            var missing = await controller.GetProduct("nope");

            Assert.Equal(product.Id, Assert.IsType<Product>(Assert.IsType<OkObjectResult>(found).Value).Id);
            Assert.IsType<NotFoundResult>(missing);
        }

        [Fact]
        public async Task CheckPromo_UsableCode_ReturnsPublicView()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            db.PromoCodes.Add(new Promo { Code = "SAVE10", Discount = 0.10m, TimesUsed = 3, UsageLimit = 10 });
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.CheckPromo(" save10 ");

            var view = Assert.IsType<PromoView>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal("SAVE10", view.Code);
            Assert.Equal(0.10m, view.Discount);
        }

        [Fact]
        public async Task CheckPromo_ExpiredOrMissing_ReturnsNotFound()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            db.PromoCodes.Add(new Promo { Code = "OLD", Discount = 0.10m, Expiration = DateTimeOffset.Now.AddDays(-1) });
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            Assert.IsType<NotFoundResult>(await controller.CheckPromo("OLD"));
            Assert.IsType<NotFoundResult>(await controller.CheckPromo("MISSING"));
            Assert.IsType<BadRequestObjectResult>(await controller.CheckPromo(""));
        }

        [Fact]
        public async Task Quote_ReturnsServerPricedTotals()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct("Umbrella", 15m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.Quote(new QuoteRequest { Lines = new List<CartLineRequest> { Line(product, 2) } });

            var quote = Assert.IsType<QuoteResult>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal(30m, quote.Subtotal);
            Assert.Equal(6m, quote.ShippingCost);
            Assert.Equal(36m, quote.Total);
        }

        [Fact]
        public async Task CreateOrder_Valid_SavesOrderAndPayment_ReturnsCheckoutUrl()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct("Umbrella", 15m, stock: 5);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var payments = TestUtils.CreateMockPaymentProvider();
            PaymentOrderRequest? sent = null;
            payments.Setup(p => p.CreateOrderAsync(It.IsAny<PaymentOrderRequest>(), It.IsAny<CancellationToken>()))
                .Callback((PaymentOrderRequest r, CancellationToken _) => sent = r)
                .ReturnsAsync(new PaymentOrderResult { ProviderOrderId = "rev_1", Token = "tok_1", CheckoutUrl = "https://checkout.revolut.com/x", Status = PaymentStatus.Pending });
            var controller = CreateController(db, payments);

            var result = await controller.CreateOrder(TestUtils.CreateOrderRequest(Line(product, 2)));

            var response = Assert.IsType<OrderCreatedResponse>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal("https://checkout.revolut.com/x", response.CheckoutUrl);
            Assert.Equal("tok_1", response.PaymentToken);
            Assert.Equal(36m, response.Total);
            var order = Assert.Single(db.ShopOrders);
            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
            var payment = Assert.Single(db.Payments);
            Assert.Equal("rev_1", payment.ProviderOrderId);
            Assert.Equal(order.Id, payment.OrderId);
            Assert.Equal(3, product.StockQuantity);
            Assert.NotNull(sent);
            Assert.Equal(3600, sent!.AmountMinor);
            Assert.Equal("EUR", sent.Currency);
            Assert.Equal(order.OrderNumber, sent.Reference);
            Assert.Equal($"https://bellumgens.com/bg/shop/order/{order.Id}", sent.RedirectUrl);
            Assert.Equal("PT60M", sent.ExpirePendingAfter);
        }

        [Fact]
        public async Task CreateOrder_InvalidModel_ReturnsBadRequest()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(db);
            controller.ModelState.AddModelError("Email", "Required");

            var result = await controller.CreateOrder(new CreateOrderRequest());

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(db.ShopOrders);
        }

        [Fact]
        public async Task CreateOrder_ProviderNotConfigured_Returns503_WithoutCreatingOrder()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct(stock: 1);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var controller = CreateController(db, TestUtils.CreateMockPaymentProvider(configured: false));

            var result = await controller.CreateOrder(TestUtils.CreateOrderRequest(Line(product)));

            Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
            Assert.Empty(db.ShopOrders);
            Assert.Equal(1, product.StockQuantity);
        }

        [Fact]
        public async Task CreateOrder_SoldOut_ReturnsConflictWithQuote()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct(stock: 1);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.CreateOrder(TestUtils.CreateOrderRequest(Line(product, 2)));

            Assert.IsType<ConflictObjectResult>(result);
            Assert.Empty(db.ShopOrders);
            Assert.Equal(1, product.StockQuantity);
        }

        [Fact]
        public async Task CreateOrder_ProviderFailure_CancelsOrderAndReleasesStock_Returns502()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct(stock: 2);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var payments = TestUtils.CreateMockPaymentProvider();
            payments.Setup(p => p.CreateOrderAsync(It.IsAny<PaymentOrderRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new PaymentProviderException("Revolut returned 500."));
            var controller = CreateController(db, payments);

            var result = await controller.CreateOrder(TestUtils.CreateOrderRequest(Line(product, 2)));

            Assert.Equal(502, Assert.IsType<ObjectResult>(result).StatusCode);
            var order = Assert.Single(db.ShopOrders);
            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(2, product.StockQuantity);
            Assert.Empty(db.Payments);
        }

        [Fact]
        public async Task CreateOrder_LoggedInUser_LinksOrderToUser()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct();
            db.Products.Add(product);
            var user = new ApplicationUser { Id = "user1", UserName = "ivan" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            _mockUserManager.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);
            var controller = CreateController(db);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            await controller.CreateOrder(TestUtils.CreateOrderRequest(Line(product)));

            Assert.Equal("user1", Assert.Single(db.ShopOrders).UserId);
        }

        [Fact]
        public async Task GetOrder_UnknownId_ReturnsNotFound()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(db);

            Assert.IsType<NotFoundResult>(await controller.GetOrder(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetOrder_ReturnsStatusView_WithCheckoutUrlWhilePending()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            order.Items.Add(new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Umbrella", UnitPrice = 30m, Quantity = 1, LineTotal = 30m });
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", CheckoutUrl = "https://checkout.revolut.com/x", Amount = 36m });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.GetOrder(order.Id);

            var view = Assert.IsType<OrderStatusView>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal(OrderStatus.AwaitingPayment, view.Status);
            Assert.Equal(PaymentStatus.Pending, view.PaymentStatus);
            Assert.Equal("https://checkout.revolut.com/x", view.CheckoutUrl);
            Assert.False(view.CanRetryPayment);
            Assert.Single(view.Items);
            Assert.Equal(order.OrderNumber, view.OrderNumber);
        }

        [Fact]
        public async Task GetOrder_ExpiredUnpaidOrder_IsCancelledOnRead()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(expiresOn: DateTimeOffset.Now.AddMinutes(-1));
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var view = Assert.IsType<OrderStatusView>(Assert.IsType<OkObjectResult>(await controller.GetOrder(order.Id)).Value);

            Assert.Equal(OrderStatus.Cancelled, view.Status);
            Assert.False(view.CanRetryPayment);
        }

        [Fact]
        public async Task GetOrder_StalePendingPayment_ReconcilesWithProvider()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            var payment = new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m, UpdatedOn = DateTimeOffset.Now.AddMinutes(-2) };
            order.Payments.Add(payment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var payments = TestUtils.CreateMockPaymentProvider();
            payments.Setup(p => p.GetOrderAsync("rev_1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentOrderResult { ProviderOrderId = "rev_1", Status = PaymentStatus.Completed, RawState = "completed" });
            var controller = CreateController(db, payments);

            var view = Assert.IsType<OrderStatusView>(Assert.IsType<OkObjectResult>(await controller.GetOrder(order.Id)).Value);

            Assert.Equal(OrderStatus.Paid, view.Status);
            Assert.Equal(PaymentStatus.Completed, view.PaymentStatus);
            Assert.Null(view.CheckoutUrl);
        }

        [Fact]
        public async Task GetOrder_FreshPendingPayment_DoesNotCallProvider()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m, UpdatedOn = DateTimeOffset.Now });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var payments = TestUtils.CreateMockPaymentProvider();
            var controller = CreateController(db, payments);

            await controller.GetOrder(order.Id);

            payments.Verify(p => p.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RetryPayment_AfterDecline_CreatesNewProviderOrder()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", Amount = 36m, Status = PaymentStatus.Failed, CreatedOn = DateTimeOffset.Now.AddMinutes(-1) });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var payments = TestUtils.CreateMockPaymentProvider();
            var controller = CreateController(db, payments);

            var result = await controller.RetryPayment(order.Id);

            var response = Assert.IsType<OrderCreatedResponse>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.NotNull(response.CheckoutUrl);
            Assert.Equal(2, order.Payments.Count);
            payments.Verify(p => p.CreateOrderAsync(It.IsAny<PaymentOrderRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RetryPayment_WhilePending_ReturnsExistingCheckout()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder();
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_1", CheckoutUrl = "https://checkout.revolut.com/existing", Amount = 36m, UpdatedOn = DateTimeOffset.Now });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var payments = TestUtils.CreateMockPaymentProvider();
            var controller = CreateController(db, payments);

            var response = Assert.IsType<OrderCreatedResponse>(Assert.IsType<OkObjectResult>(await controller.RetryPayment(order.Id)).Value);

            Assert.Equal("https://checkout.revolut.com/existing", response.CheckoutUrl);
            payments.Verify(p => p.CreateOrderAsync(It.IsAny<PaymentOrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RetryPayment_PaidOrder_ReturnsConflict()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            Assert.IsType<ConflictObjectResult>(await controller.RetryPayment(order.Id));
        }

        [Fact]
        public async Task MyOrders_ReturnsOnlyTheCurrentUsersOrders()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", UserName = "ivan" };
            db.Users.Add(user);
            db.ShopOrders.AddRange(TestUtils.CreateShopOrder(userId: "user1"), TestUtils.CreateShopOrder(userId: "user2"), TestUtils.CreateShopOrder());
            await db.SaveChangesAsync();
            _mockUserManager.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);
            var controller = CreateController(db);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.MyOrders();

            var orders = Assert.IsAssignableFrom<List<OrderStatusView>>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Single(orders);
        }

        [Theory]
        [InlineData(36.00, 3600)]
        [InlineData(0.01, 1)]
        [InlineData(19.995, 2000)]
        [InlineData(12.345, 1235)]
        public void ToMinorUnits_RoundsHalfAwayFromZero(decimal amount, long expected)
        {
            Assert.Equal(expected, ShopController.ToMinorUnits(amount));
        }
    }
}
