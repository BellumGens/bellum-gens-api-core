using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BellumGens.Api.Core.Tests
{
    public class ShopAdminControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = TestUtils.CreateMockUserManager();
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager = TestUtils.CreateMockRoleManager();
        private readonly Mock<IStorageService> _storage = TestUtils.CreateMockStorageService();
        private readonly Mock<EmailServiceProvider> _email = TestUtils.CreateMockEmailSender();

        private ShopAdminController CreateController(BellumGensDbContext db, Mock<IPaymentProvider>? payments = null)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "BlobService:Container", "default" },
                { "BlobService:ShopContainer", "shop" }
            }).Build();
            var orders = TestUtils.CreateOrderService(db, _email);
            var controller = new ShopAdminController(_storage.Object, orders, (payments ?? TestUtils.CreateMockPaymentProvider()).Object,
                TestUtils.CreateMemoryCache(), config, _mockUserManager.Object, _mockRoleManager.Object,
                TestUtils.CreateMockSignInManager(_mockUserManager).Object, _email.Object, db,
                TestUtils.CreateMockLogger<ShopAdminController>().Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));
            return controller;
        }

        [Fact]
        public async Task CreateProduct_GeneratesSlugAndVariantNames()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(db);
            var product = new Product
            {
                Name = "BGE Stara Zagora Jersey 2026",
                Type = ProductType.Jersey,
                Brand = Brand.BGEStaraZagora,
                Price = 35m,
                Variants = new List<ProductVariant>
                {
                    new() { Cut = JerseyCut.Male, Size = JerseySize.L, StockQuantity = 10 },
                    new() { Cut = JerseyCut.Female, Size = JerseySize.S, StockQuantity = 4, Name = "  Ladies S  " }
                }
            };

            var result = await controller.CreateProduct(product);

            var saved = Assert.IsType<Product>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.NotEqual(Guid.Empty, saved.Id);
            Assert.Equal("bge-stara-zagora-jersey-2026", saved.Slug);
            Assert.Equal(2, saved.Variants.Count);
            Assert.Contains(saved.Variants, v => v.Name == "Male / L");
            Assert.Contains(saved.Variants, v => v.Name == "Ladies S");
            Assert.All(saved.Variants, v => Assert.Equal(saved.Id, v.ProductId));
        }

        [Fact]
        public async Task CreateProduct_DuplicateSlug_GetsNumericSuffix()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            db.Products.Add(TestUtils.CreateProduct("Umbrella"));
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.CreateProduct(new Product { Name = "Umbrella", Price = 10m });

            Assert.Equal("umbrella-2", Assert.IsType<Product>(Assert.IsType<OkObjectResult>(result).Value).Slug);
        }

        [Fact]
        public async Task UpdateProduct_SyncsVariants_RemovesUnorderedAndDeactivatesOrdered()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var jersey = TestUtils.CreateJersey();
            db.Products.Add(jersey);
            var maleL = jersey.Variants.First(v => v.Cut == JerseyCut.Male);
            var femaleM = jersey.Variants.First(v => v.Cut == JerseyCut.Female);
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            order.Items.Add(new OrderItem { ProductId = jersey.Id, VariantId = maleL.Id, ProductName = jersey.Name, VariantName = maleL.Name, UnitPrice = 30m, Quantity = 1, LineTotal = 30m });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var update = new Product
            {
                Name = "EBL Jersey 2026",
                Slug = "ebl-jersey",
                Price = 32m,
                Type = ProductType.Jersey,
                Brand = Brand.EBLeague,
                Active = true,
                Variants = new List<ProductVariant>
                {
                    new() { Cut = JerseyCut.Male, Size = JerseySize.XL, StockQuantity = 7 } // new; maleL and femaleM omitted
                }
            };
            var result = await controller.UpdateProduct(jersey.Id, update);

            var saved = Assert.IsType<Product>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal("EBL Jersey 2026", saved.Name);
            Assert.Equal("ebl-jersey", saved.Slug);
            Assert.Equal(32m, saved.Price);
            var variants = db.ProductVariants.Where(v => v.ProductId == jersey.Id).ToList();
            Assert.Equal(2, variants.Count);
            Assert.Contains(variants, v => v.Id == maleL.Id && !v.Active);
            Assert.DoesNotContain(variants, v => v.Id == femaleM.Id);
            Assert.Contains(variants, v => v.Name == "Male / XL" && v.StockQuantity == 7 && v.Active);
        }

        [Fact]
        public async Task UpdateProduct_Unknown_ReturnsNotFound()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(db);

            Assert.IsType<NotFoundResult>(await controller.UpdateProduct(Guid.NewGuid(), new Product { Name = "x", Price = 1m }));
        }

        [Fact]
        public async Task DeleteProduct_WithOrders_DeactivatesInsteadOfDeleting()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var jersey = TestUtils.CreateJersey();
            db.Products.Add(jersey);
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            order.Items.Add(new OrderItem { ProductId = jersey.Id, ProductName = jersey.Name, UnitPrice = 30m, Quantity = 1, LineTotal = 30m });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.DeleteProduct(jersey.Id);

            Assert.IsType<OkObjectResult>(result);
            var stored = db.Products.Single();
            Assert.False(stored.Active);
            Assert.All(stored.Variants, v => Assert.False(v.Active));
        }

        [Fact]
        public async Task DeleteProduct_WithoutOrders_Removes()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateJersey();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            await controller.DeleteProduct(product.Id);

            Assert.Empty(db.Products);
            Assert.Empty(db.ProductVariants);
        }

        [Fact]
        public async Task UploadProductImage_StoresBlobUrlInShopContainer()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            _storage.Setup(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>(), "shop"))
                .ReturnsAsync("https://blob.example.com/shop/img.png");
            var controller = CreateController(db);

            var result = await controller.UploadProductImage(product.Id, new ImageUploadRequest { Image = "data:image/png;base64,AAAA" });

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("https://blob.example.com/shop/img.png", product.ImageUrl);
        }

        [Fact]
        public async Task UploadProductImage_RejectsNonDataUrl()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var product = TestUtils.CreateProduct();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            _storage.Setup(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("");
            var controller = CreateController(db);

            var result = await controller.UploadProductImage(product.Id, new ImageUploadRequest { Image = "https://elsewhere.example.com/x.png" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetOrders_FiltersByStatus()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            db.ShopOrders.AddRange(TestUtils.CreateShopOrder(OrderStatus.Paid), TestUtils.CreateShopOrder(OrderStatus.Paid), TestUtils.CreateShopOrder(OrderStatus.Cancelled));
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var all = Assert.IsAssignableFrom<List<ShopOrder>>(Assert.IsType<OkObjectResult>(await controller.GetOrders()).Value);
            var paid = Assert.IsAssignableFrom<List<ShopOrder>>(Assert.IsType<OkObjectResult>(await controller.GetOrders(status: OrderStatus.Paid)).Value);

            Assert.Equal(3, all.Count);
            Assert.Equal(2, paid.Count);
        }

        [Fact]
        public async Task UpdateOrderStatus_PaidToShipped_ReturnsOk()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.UpdateOrderStatus(order.Id, new OrderStatusUpdate { Status = OrderStatus.Shipped, TrackingNumber = "ECONT-1" });

            var saved = Assert.IsType<ShopOrder>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal(OrderStatus.Shipped, saved.Status);
            Assert.Equal("ECONT-1", saved.TrackingNumber);
        }

        [Fact]
        public async Task UpdateOrderStatus_IllegalTransition_ReturnsBadRequest()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.AwaitingPayment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.UpdateOrderStatus(order.Id, new OrderStatusUpdate { Status = OrderStatus.Shipped });

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        }

        [Fact]
        public async Task RefundOrder_CallsProviderForTheCompletedPayment_AndMarksRefunded()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_failed", Amount = 36m, Status = PaymentStatus.Failed });
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_ok", Amount = 36m, Status = PaymentStatus.Completed });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var payments = TestUtils.CreateMockPaymentProvider();
            payments.Setup(p => p.RefundAsync("rev_ok", 3600, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentOrderResult { ProviderOrderId = "refund_1", Status = PaymentStatus.Completed });
            var controller = CreateController(db, payments);

            var result = await controller.RefundOrder(order.Id);

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(OrderStatus.Refunded, order.Status);
            payments.Verify(p => p.RefundAsync("rev_ok", 3600, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RefundOrder_UnpaidOrder_ReturnsBadRequest()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.AwaitingPayment);
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            Assert.IsType<BadRequestObjectResult>(await controller.RefundOrder(order.Id));
        }

        [Fact]
        public async Task RefundOrder_ProviderRejects_Returns502AndKeepsStatus()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            order.Payments.Add(new Payment { OrderId = order.Id, ProviderOrderId = "rev_ok", Amount = 36m, Status = PaymentStatus.Completed });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var payments = TestUtils.CreateMockPaymentProvider();
            payments.Setup(p => p.RefundAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new PaymentProviderException("Revolut returned 422."));
            var controller = CreateController(db, payments);

            var result = await controller.RefundOrder(order.Id);

            Assert.Equal(502, Assert.IsType<ObjectResult>(result).StatusCode);
            Assert.Equal(OrderStatus.Paid, order.Status);
        }

        [Fact]
        public async Task ExportOrders_ReturnsCsvWithHeaderAndRows()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            order.City = "Sofia, Center";
            order.Items.Add(new OrderItem { ProductId = Guid.NewGuid(), ProductName = "Umbrella", VariantName = null, UnitPrice = 30m, Quantity = 1, LineTotal = 30m });
            db.ShopOrders.Add(order);
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.ExportOrders();

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", file.ContentType);
            string csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
            Assert.Contains("OrderNumber,OrderDate,Status", csv);
            Assert.Contains(order.OrderNumber, csv);
            Assert.Contains("\"Sofia, Center\"", csv);
            Assert.Contains("1x Umbrella", csv);
        }

        [Fact]
        public async Task CreatePromo_NormalizesCode_AndRejectsDuplicates()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(db);

            var first = await controller.CreatePromo(new Promo { Code = " summer26 ", Discount = 0.15m });
            var second = await controller.CreatePromo(new Promo { Code = "SUMMER26", Discount = 0.5m });

            Assert.Equal("SUMMER26", Assert.IsType<Promo>(Assert.IsType<OkObjectResult>(first).Value).Code);
            Assert.IsType<ConflictObjectResult>(second);
            Assert.Single(db.PromoCodes);
        }

        [Fact]
        public async Task UpdatePromo_ChangesRulesButNotUsage()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            db.PromoCodes.Add(new Promo { Code = "SAVE10", Discount = 0.1m, TimesUsed = 4 });
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.UpdatePromo("save10", new Promo { Code = "SAVE10", Discount = 0.2m, UsageLimit = 10, Active = false, TimesUsed = 0 });

            var saved = Assert.IsType<Promo>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal(0.2m, saved.Discount);
            Assert.Equal(10, saved.UsageLimit);
            Assert.False(saved.Active);
            Assert.Equal(4, saved.TimesUsed);
        }

        [Fact]
        public async Task DeletePromo_UsedByOrders_DeactivatesInsteadOfDeleting()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            db.PromoCodes.Add(new Promo { Code = "USED", Discount = 0.1m });
            var order = TestUtils.CreateShopOrder(OrderStatus.Paid);
            order.PromoCode = "USED";
            db.ShopOrders.Add(order);
            db.PromoCodes.Add(new Promo { Code = "FRESH", Discount = 0.1m });
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            await controller.DeletePromo("used");
            await controller.DeletePromo("fresh");

            var remaining = Assert.Single(db.PromoCodes);
            Assert.Equal("USED", remaining.Code);
            Assert.False(remaining.Active);
        }

        [Fact]
        public async Task RegisterWebhook_ReturnsSigningSecretFromProvider()
        {
            using var db = TestUtils.CreateInMemoryDbContext();
            var payments = TestUtils.CreateMockPaymentProvider();
            payments.Setup(p => p.RegisterWebhookAsync("https://api.bellumgens.com/api/shop/webhooks/revolut", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookRegistrationResult { Id = "wh_1", SigningSecret = "wsk_new" });
            var controller = CreateController(db, payments);

            var result = await controller.RegisterWebhook(new WebhookRegistrationRequest { Url = "https://api.bellumgens.com/api/shop/webhooks/revolut" });

            Assert.Equal("wsk_new", Assert.IsType<WebhookRegistrationResult>(Assert.IsType<OkObjectResult>(result).Value).SigningSecret);
        }

        [Theory]
        [InlineData("EB League Jersey", "eb-league-jersey")]
        [InlineData("  Pen & Highlighter!  ", "pen-highlighter")]
        [InlineData("BGE---Umbrella", "bge-umbrella")]
        public void Slugify_ProducesUrlSafeSlugs(string input, string expected)
        {
            Assert.Equal(expected, ShopAdminController.Slugify(input));
        }

        [Fact]
        public void Slugify_NonLatinInput_FallsBackToRandomSlug()
        {
            string slug = ShopAdminController.Slugify("Чадър");

            Assert.Equal(8, slug.Length);
            Assert.Matches("^[a-f0-9]+$", slug);
        }
    }
}
