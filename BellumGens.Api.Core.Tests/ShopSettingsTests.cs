using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BellumGens.Api.Core.Tests
{
	public class ShopSettingsTests
	{
		private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = TestUtils.CreateMockUserManager();
		private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager = TestUtils.CreateMockRoleManager();
		private readonly Mock<IEmailService> _email = TestUtils.CreateMockEmailSender();

		private ShopAdminController CreateAdminController(BellumGensDbContext db, ShopSettingsService settings)
		{
			var controller = new ShopAdminController(TestUtils.CreateMockStorageService().Object, new OrderService(db, TestUtils.CreateShopOptions(), settings,
					_email.Object, TestUtils.CreateMockLogger<OrderService>().Object),
				TestUtils.CreateMockPaymentProvider().Object, TestUtils.CreateMemoryCache(), new ConfigurationBuilder().Build(), settings,
				_mockUserManager.Object, _mockRoleManager.Object, TestUtils.CreateMockSignInManager(_mockUserManager).Object, _email.Object, db,
				TestUtils.CreateMockLogger<ShopAdminController>().Object);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));
			return controller;
		}

		private ShopController CreateShopController(BellumGensDbContext db, ShopSettingsService settings)
		{
			var options = TestUtils.CreateShopOptions();
			var controller = new ShopController(new OrderService(db, options, settings, _email.Object, TestUtils.CreateMockLogger<OrderService>().Object),
				TestUtils.CreateMockPaymentProvider().Object, TestUtils.CreateMemoryCache(), options, settings,
				_mockUserManager.Object, _mockRoleManager.Object, TestUtils.CreateMockSignInManager(_mockUserManager).Object, _email.Object, db,
				TestUtils.CreateMockLogger<ShopController>().Object);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
			return controller;
		}

		private static QuoteRequest Cart(Product product, int quantity)
		{
			return new QuoteRequest { Lines = new List<CartLineRequest> { new() { ProductId = product.Id, Quantity = quantity } } };
		}

		[Fact]
		public async Task Get_WithoutStoredRow_ReturnsDefaults()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var service = TestUtils.CreateShopSettingsService(db);

			var settings = await service.GetAsync(TestContext.Current.CancellationToken);

			Assert.Equal(ShopSettings.DefaultShippingCost, settings.ShippingCost);
			Assert.Equal(ShopSettings.DefaultFreeShippingThreshold, settings.FreeShippingThreshold);
			Assert.Equal(ShopSettings.DefaultMaxQuantityPerLine, settings.MaxQuantityPerLine);
		}

		[Fact]
		public async Task Get_ReturnsACopyThatDoesNotChangeTheCachedSettings()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var service = TestUtils.CreateShopSettingsService(db);

			var first = await service.GetAsync(TestContext.Current.CancellationToken);
			first.ShippingCost = 99m;
			var second = await service.GetAsync(TestContext.Current.CancellationToken);

			Assert.Equal(ShopSettings.DefaultShippingCost, second.ShippingCost);
		}

		[Fact]
		public async Task Quote_UsesStoredShippingCostAndThreshold()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var product = TestUtils.CreateProduct(price: 15m);
			db.Products.Add(product);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var service = TestUtils.CreateOrderService(db, settings: s =>
			{
				s.ShippingCost = 4.5m;
				s.FreeShippingThreshold = 30m;
			});

			var belowThreshold = await service.QuoteAsync(Cart(product, 1), TestContext.Current.CancellationToken);
			var atThreshold = await service.QuoteAsync(Cart(product, 2), TestContext.Current.CancellationToken);

			Assert.Equal(4.5m, belowThreshold.ShippingCost);
			Assert.Equal(19.5m, belowThreshold.Total);
			Assert.True(atThreshold.FreeShipping);
			Assert.Equal(0m, atThreshold.ShippingCost);
		}

		[Fact]
		public async Task Update_TakesEffectOnTheNextQuote()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var product = TestUtils.CreateProduct(price: 15m);
			db.Products.Add(product);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var settings = TestUtils.CreateShopSettingsService(db);
			var orders = new OrderService(db, TestUtils.CreateShopOptions(), settings, _email.Object, TestUtils.CreateMockLogger<OrderService>().Object);
			var before = await orders.QuoteAsync(Cart(product, 1), TestContext.Current.CancellationToken);

			await settings.UpdateAsync(new ShopSettings { ShippingCost = 8m, FreeShippingThreshold = 100m, MaxQuantityPerLine = 3 }, "admin1",
				TestContext.Current.CancellationToken);
			var after = await orders.QuoteAsync(Cart(product, 1), TestContext.Current.CancellationToken);
			var overLimit = await orders.QuoteAsync(Cart(product, 4), TestContext.Current.CancellationToken);

			Assert.Equal(6m, before.ShippingCost);
			Assert.Equal(8m, after.ShippingCost);
			Assert.Equal("Quantity must be between 1 and 3.", overLimit.Lines[0].Problem);
		}

		[Fact]
		public async Task Update_RoundsAmountsAndKeepsASingleRow()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var service = TestUtils.CreateShopSettingsService(db, s => s.ShippingCost = 5m);

			var saved = await service.UpdateAsync(new ShopSettings { ShippingCost = 7.255m, FreeShippingThreshold = 60.001m, MaxQuantityPerLine = 12 }, "admin1",
				TestContext.Current.CancellationToken);

			Assert.Equal(7.26m, saved.ShippingCost);
			Assert.Equal(60m, saved.FreeShippingThreshold);
			Assert.Equal(12, saved.MaxQuantityPerLine);
			Assert.Equal("admin1", saved.UpdatedBy);
			Assert.Single(db.ShopSettings);
		}

		[Fact]
		public void EnsureCreated_TakesLegacyConfigurationValues()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "shop:shippingCost", "7.50" },
				{ "shop:freeShippingThreshold", "80" },
				{ "shop:maxQuantityPerLine", "5" }
			}).Build();

			ShopSettingsService.EnsureCreated(db, configuration);

			var settings = Assert.Single(db.ShopSettings);
			Assert.Equal(7.5m, settings.ShippingCost);
			Assert.Equal(80m, settings.FreeShippingThreshold);
			Assert.Equal(5, settings.MaxQuantityPerLine);
		}

		[Fact]
		public void EnsureCreated_WithoutConfiguration_UsesDefaults()
		{
			using var db = TestUtils.CreateInMemoryDbContext();

			ShopSettingsService.EnsureCreated(db, new ConfigurationBuilder().Build());

			var settings = Assert.Single(db.ShopSettings);
			Assert.Equal(ShopSettings.DefaultShippingCost, settings.ShippingCost);
			Assert.Equal(ShopSettings.DefaultFreeShippingThreshold, settings.FreeShippingThreshold);
			Assert.Equal(ShopSettings.DefaultMaxQuantityPerLine, settings.MaxQuantityPerLine);
		}

		[Fact]
		public void EnsureCreated_KeepsTheStoredRow()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			TestUtils.CreateShopSettingsService(db, s => s.ShippingCost = 3m);
			var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "shop:shippingCost", "7.50" }
			}).Build();

			ShopSettingsService.EnsureCreated(db, configuration);

			Assert.Equal(3m, Assert.Single(db.ShopSettings).ShippingCost);
		}

		[Fact]
		public async Task Admin_UpdateSettings_SavesAndRecordsTheAdmin()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAdminController(db, TestUtils.CreateShopSettingsService(db));

			var result = await controller.UpdateSettings(new ShopSettings { ShippingCost = 5m, FreeShippingThreshold = 75m, MaxQuantityPerLine = 4 });

			var saved = Assert.IsType<ShopSettings>(Assert.IsType<OkObjectResult>(result).Value);
			Assert.Equal(5m, saved.ShippingCost);
			Assert.Equal("admin1", saved.UpdatedBy);
			var stored = Assert.Single(db.ShopSettings);
			Assert.Equal(75m, stored.FreeShippingThreshold);
			Assert.Equal(4, stored.MaxQuantityPerLine);
		}

		[Fact]
		public async Task Admin_UpdateSettings_InvalidModel_ReturnsBadRequestAndKeepsSettings()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAdminController(db, TestUtils.CreateShopSettingsService(db));
			controller.ModelState.AddModelError("ShippingCost", "The field ShippingCost must be between 0 and 1000.");

			var result = await controller.UpdateSettings(new ShopSettings { ShippingCost = -1m });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(db.ShopSettings);
		}

		[Fact]
		public async Task Admin_GetSettings_ReturnsStoredSettings()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAdminController(db, TestUtils.CreateShopSettingsService(db, s => s.FreeShippingThreshold = 40m));

			var result = await controller.GetSettings();

			Assert.Equal(40m, Assert.IsType<ShopSettings>(Assert.IsType<OkObjectResult>(result).Value).FreeShippingThreshold);
		}

		[Fact]
		public async Task Storefront_GetSettings_ReturnsPricingRulesWithCurrency()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var controller = CreateShopController(db, TestUtils.CreateShopSettingsService(db, s =>
			{
				s.ShippingCost = 5m;
				s.MaxQuantityPerLine = 7;
			}));

			var result = await controller.GetSettings();

			var view = Assert.IsType<ShopSettingsView>(Assert.IsType<OkObjectResult>(result).Value);
			Assert.Equal(5m, view.ShippingCost);
			Assert.Equal(ShopSettings.DefaultFreeShippingThreshold, view.FreeShippingThreshold);
			Assert.Equal(7, view.MaxQuantityPerLine);
			Assert.Equal("EUR", view.Currency);
		}
	}
}
