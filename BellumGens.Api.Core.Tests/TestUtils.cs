using System;
using System.Collections.Generic;
using System.Security.Claims;
using BellumGens.Api.Core;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BellumGens.Api.Core.Tests
{
    public static class TestUtils
    {
        public static BellumGensDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<BellumGensDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new BellumGensDbContext(options);
        }

        public static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        public static Mock<RoleManager<IdentityRole>> CreateMockRoleManager()
        {
            var store = new Mock<IRoleStore<IdentityRole>>();
            return new Mock<RoleManager<IdentityRole>>(
                store.Object, null!, null!, null!, null!);
        }

        public static Mock<SignInManager<ApplicationUser>> CreateMockSignInManager(
            Mock<UserManager<ApplicationUser>> userManager)
        {
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            return new Mock<SignInManager<ApplicationUser>>(
                userManager.Object, contextAccessor.Object, claimsFactory.Object, null!, null!, null!, null!);
        }

        public static EmailServiceProvider CreateMockEmailServiceProvider()
        {
            var appConfig = CreateAppConfiguration();
            return new EmailServiceProvider(appConfig);
        }

        public static Mock<ISteamService> CreateMockSteamService()
        {
            return new Mock<ISteamService>();
        }

        public static Mock<IBattleNetService> CreateMockBattleNetService()
        {
            return new Mock<IBattleNetService>();
        }

        public static Mock<INotificationService> CreateMockNotificationService()
        {
            return new Mock<INotificationService>();
        }

        public static Mock<IStorageService> CreateMockStorageService()
        {
            return new Mock<IStorageService>();
        }

        public static Mock<ILogger<T>> CreateMockLogger<T>()
        {
            return new Mock<ILogger<T>>();
        }

        // Shop helpers

        public static IOptions<ShopOptions> CreateShopOptions(Action<ShopOptions>? configure = null)
        {
            var options = new ShopOptions
            {
                Currency = "EUR",
                ShippingCost = 6m,
                FreeShippingThreshold = 50m,
                PaymentExpiryMinutes = 60,
                MaxQuantityPerLine = 10,
                StorefrontUrl = "https://bellumgens.com",
                ReconcileAfterSeconds = 30
            };
            configure?.Invoke(options);
            return Options.Create(options);
        }

        public static IOptions<RevolutOptions> CreateRevolutOptions(string? secretKey = "sk_test", string? webhookSecret = "wsk_test")
        {
            return Options.Create(new RevolutOptions
            {
                BaseUrl = "https://sandbox-merchant.revolut.com",
                SecretKey = secretKey,
                WebhookSigningSecret = webhookSecret
            });
        }

        /// <summary>An email sender that records calls instead of talking to SMTP.</summary>
        public static Mock<EmailServiceProvider> CreateMockEmailSender()
        {
            var mock = new Mock<EmailServiceProvider>(CreateAppConfiguration());
            mock.Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        public static Mock<IPaymentProvider> CreateMockPaymentProvider(bool configured = true)
        {
            var mock = new Mock<IPaymentProvider>();
            mock.SetupGet(p => p.Provider).Returns(PaymentProvider.Revolut);
            mock.SetupGet(p => p.IsConfigured).Returns(configured);
            mock.Setup(p => p.CreateOrderAsync(It.IsAny<PaymentOrderRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentOrderRequest _, CancellationToken _) => new PaymentOrderResult
                {
                    ProviderOrderId = "rev_" + Guid.NewGuid().ToString("N"),
                    Token = "tok_test",
                    CheckoutUrl = "https://checkout.revolut.com/payment-link/test",
                    Status = PaymentStatus.Pending,
                    RawState = "pending"
                });
            return mock;
        }

        public static OrderService CreateOrderService(BellumGensDbContext db, Mock<EmailServiceProvider>? email = null, Action<ShopOptions>? configure = null)
        {
            return new OrderService(db, CreateShopOptions(configure), (email ?? CreateMockEmailSender()).Object, CreateMockLogger<OrderService>().Object);
        }

        public static MemoryCache CreateMemoryCache()
        {
            return new MemoryCache(new MemoryCacheOptions());
        }

        public static Product CreateProduct(string name = "Umbrella", decimal price = 15m, int? stock = null,
            Brand brand = Brand.BellumGens, ProductType type = ProductType.Umbrella, decimal? discountPercentage = null, bool active = true)
        {
            return new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = name.ToLowerInvariant().Replace(' ', '-'),
                Description = name,
                Type = type,
                Brand = brand,
                Price = price,
                DiscountPercentage = discountPercentage,
                StockQuantity = stock,
                Active = active
            };
        }

        /// <summary>A jersey with two variants: Male / L (stock 5) and Female / M (stock 1).</summary>
        public static Product CreateJersey(string name = "EBL Jersey", decimal price = 30m, Brand brand = Brand.EBLeague)
        {
            var product = CreateProduct(name, price, null, brand, ProductType.Jersey);
            product.Variants.Add(new ProductVariant { Id = Guid.NewGuid(), Name = "Male / L", Cut = JerseyCut.Male, Size = JerseySize.L, StockQuantity = 5, SortOrder = 1 });
            product.Variants.Add(new ProductVariant { Id = Guid.NewGuid(), Name = "Female / M", Cut = JerseyCut.Female, Size = JerseySize.M, StockQuantity = 1, SortOrder = 2 });
            return product;
        }

        public static CreateOrderRequest CreateOrderRequest(params CartLineRequest[] lines)
        {
            return new CreateOrderRequest
            {
                Lines = lines.ToList(),
                Email = "customer@example.com",
                FirstName = "Ivan",
                LastName = "Petrov",
                PhoneNumber = "888123456",
                City = "Sofia",
                StreetAddress = "bul. Vitosha 1",
                PostalCode = "1000",
                Country = "BG",
                Language = "bg",
                AcceptedTerms = true
            };
        }

        public static ShopOrder CreateShopOrder(OrderStatus status = OrderStatus.AwaitingPayment, DateTimeOffset? expiresOn = null, string? userId = null)
        {
            return new ShopOrder
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Email = "customer@example.com",
                FirstName = "Ivan",
                LastName = "Petrov",
                PhoneNumber = "888123456",
                City = "Sofia",
                StreetAddress = "bul. Vitosha 1",
                Subtotal = 30m,
                DiscountTotal = 0m,
                ShippingCost = 6m,
                Total = 36m,
                Status = status,
                OrderDate = DateTimeOffset.Now,
                ExpiresOn = expiresOn ?? DateTimeOffset.Now.AddHours(1)
            };
        }

        public static AppConfiguration CreateAppConfiguration()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "steamApiKey", "test" },
                { "battleNet:clientId", "test" },
                { "battleNet:secret", "test" },
                { "twitch:clientId", "test" },
                { "twitch:secret", "test" },
                { "vapid:public", "test" },
                { "vapid:private", "test" },
                { "email:username", "test@test.com" },
                { "email:password", "test" },
                { "bank:name", "test" },
                { "bank:owner", "test" },
                { "bank:bic", "test" },
                { "bank:account", "test" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            return new AppConfiguration(configuration);
        }

        public static ClaimsPrincipal CreateAuthenticatedUser(string userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "testuser")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        public static ClaimsPrincipal CreateUnauthenticatedUser()
        {
            var identity = new ClaimsIdentity();
            return new ClaimsPrincipal(identity);
        }

        public static void SetupControllerContext(ControllerBase controller, ClaimsPrincipal user)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
    }
}
