using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.ComponentModel.DataAnnotations;

namespace BellumGens.Api.Core.Tests
{
	/// <summary>The signed-in customer's address book, and the Bulgaria-only delivery rules.</summary>
	public class ShippingAddressTests
	{
		private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
		private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
		private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
		private readonly Mock<IEmailService> _email;

		public ShippingAddressTests()
		{
			_mockUserManager = TestUtils.CreateMockUserManager();
			_mockRoleManager = TestUtils.CreateMockRoleManager();
			_mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
			_email = TestUtils.CreateMockEmailSender();
		}

		/// <summary>A controller acting for <paramref name="userId"/>, or for a guest when it is null.</summary>
		private ShopController CreateController(BellumGensDbContext db, string? userId = "user1")
		{
			var options = TestUtils.CreateShopOptions();
			var orders = new OrderService(db, options, _email.Object, TestUtils.CreateMockLogger<OrderService>().Object);
			var controller = new ShopController(orders, TestUtils.CreateMockPaymentProvider().Object, TestUtils.CreateMemoryCache(), options,
				_mockUserManager.Object, _mockRoleManager.Object, _mockSignInManager.Object, _email.Object, db,
				TestUtils.CreateMockLogger<ShopController>().Object);
			if (userId == null)
			{
				TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
			}
			else
			{
				var user = new ApplicationUser { Id = userId, UserName = userId };
				if (!db.Users.Any(u => u.Id == userId))
				{
					db.Users.Add(user);
					db.SaveChanges();
				}
				_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(db.Users.First(u => u.Id == userId));
				TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			}
			return controller;
		}

		private static ShippingAddressRequest Address(string street = "bul. Vitosha 1", string? postalCode = "1000")
		{
			return new ShippingAddressRequest
			{
				FirstName = "Ivan",
				LastName = "Petrov",
				PhoneNumber = "+359 888 123 456",
				City = "Sofia",
				StreetAddress = street,
				PostalCode = postalCode
			};
		}

		private static bool IsValid(object model)
		{
			return Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true);
		}

		[Fact]
		public async Task SaveAddress_AddsItOnce_AndMarksAnAddressSavedAgainAsJustUsed()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var controller = CreateController(db);

			var first = Assert.IsType<ShippingAddress>(Assert.IsType<OkObjectResult>(await controller.SaveAddress(Address())).Value);
			var firstUsed = first.LastUsedOn;
			await Task.Delay(5, TestContext.Current.CancellationToken);
			var again = Assert.IsType<ShippingAddress>(Assert.IsType<OkObjectResult>(await controller.SaveAddress(Address(street: " BUL. VITOSHA 1 "))).Value);

			Assert.Equal(first.Id, again.Id);
			Assert.True(again.LastUsedOn > firstUsed);
			var saved = Assert.Single(db.ShippingAddresses);
			Assert.Equal("user1", saved.UserId);
			Assert.Equal("BG", saved.Country);
		}

		[Fact]
		public async Task SaveAddress_RefusesANewAddressOnceTheBookIsFull()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var controller = CreateController(db);
			for (int i = 0; i < ShopOptions.MaxSavedAddresses; i++)
			{
				Assert.IsType<OkObjectResult>(await controller.SaveAddress(Address(street: $"Street {i}")));
			}

			Assert.IsType<ConflictObjectResult>(await controller.SaveAddress(Address(street: "One too many")));
			// One already in the book can still be marked as used.
			Assert.IsType<OkObjectResult>(await controller.SaveAddress(Address(street: "Street 0")));
			Assert.Equal(ShopOptions.MaxSavedAddresses, db.ShippingAddresses.Count());
		}

		[Fact]
		public async Task GetAddresses_ListsOnlyTheCustomersOwn_MostRecentlyUsedFirst()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var now = DateTimeOffset.Now;
			db.ShippingAddresses.AddRange(
				new ShippingAddress { UserId = "user1", PostalCode = "1000", FirstName = "I", LastName = "P", PhoneNumber = "1", City = "Sofia", StreetAddress = "Older", LastUsedOn = now.AddDays(-2) },
				new ShippingAddress { UserId = "user1", PostalCode = "1000", FirstName = "I", LastName = "P", PhoneNumber = "1", City = "Plovdiv", StreetAddress = "Newer", LastUsedOn = now },
				new ShippingAddress { UserId = "user2", PostalCode = "1000", FirstName = "X", LastName = "Y", PhoneNumber = "2", City = "Varna", StreetAddress = "Someone else's", LastUsedOn = now });
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var controller = CreateController(db);

			var addresses = Assert.IsType<List<ShippingAddress>>(Assert.IsType<OkObjectResult>(await controller.GetAddresses()).Value);

			Assert.Equal(new[] { "Newer", "Older" }, addresses.Select(a => a.StreetAddress));
		}

		[Fact]
		public async Task DeleteAddress_RemovesTheCustomersOwn_AndReportsAnotherCustomersAsMissing()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var own = new ShippingAddress { UserId = "user1", PostalCode = "1000", FirstName = "I", LastName = "P", PhoneNumber = "1", City = "Sofia", StreetAddress = "Mine" };
			var other = new ShippingAddress { UserId = "user2", PostalCode = "1000", FirstName = "X", LastName = "Y", PhoneNumber = "2", City = "Varna", StreetAddress = "Theirs" };
			db.ShippingAddresses.AddRange(own, other);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var controller = CreateController(db);

			Assert.IsType<NotFoundResult>(await controller.DeleteAddress(other.Id));
			Assert.IsType<NoContentResult>(await controller.DeleteAddress(own.Id));

			Assert.Equal("Theirs", Assert.Single(db.ShippingAddresses).StreetAddress);
		}

		[Fact]
		public async Task CreateOrder_SignedInCustomerAskingToSaveTheAddress_SavesIt()
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var product = TestUtils.CreateProduct("Umbrella", 15m);
			db.Products.Add(product);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var controller = CreateController(db);
			var request = TestUtils.CreateOrderRequest(new CartLineRequest { ProductId = product.Id, Quantity = 1 });
			request.PaymentMethod = PaymentMethod.CashOnDelivery;
			request.SaveAddress = true;

			Assert.IsType<OkObjectResult>(await controller.CreateOrder(request));

			var saved = Assert.Single(db.ShippingAddresses);
			Assert.Equal("user1", saved.UserId);
			Assert.Equal("bul. Vitosha 1", saved.StreetAddress);
			Assert.Equal("1000", saved.PostalCode);
		}

		[Theory]
		[InlineData(null, true)]
		[InlineData("user1", false)]
		public async Task CreateOrder_SavesNoAddressForAGuestOrWhenNotAsked(string? userId, bool saveAddress)
		{
			using var db = TestUtils.CreateInMemoryDbContext();
			var product = TestUtils.CreateProduct("Umbrella", 15m);
			db.Products.Add(product);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var controller = CreateController(db, userId);
			var request = TestUtils.CreateOrderRequest(new CartLineRequest { ProductId = product.Id, Quantity = 1 });
			request.PaymentMethod = PaymentMethod.CashOnDelivery;
			request.SaveAddress = saveAddress;

			Assert.IsType<OkObjectResult>(await controller.CreateOrder(request));

			Assert.Empty(db.ShippingAddresses);
		}

		[Theory]
		[InlineData("BG", "1000", true)]
		[InlineData("bg", " 4000 ", true)]
		[InlineData("", "6000", true)]
		[InlineData("RO", "1000", false)]
		[InlineData("BG", "100", false)]
		[InlineData("BG", "10000", false)]
		[InlineData("BG", "SW1A", false)]
		// Several Bulgarian towns share a name; the postal code tells them apart.
		[InlineData("BG", null, false)]
		[InlineData("BG", "  ", false)]
		public void DeliveryAddresses_AreInBulgariaWithAPostalCode(string country, string? postalCode, bool valid)
		{
			var address = Address(postalCode: postalCode);
			address.Country = country;
			var order = TestUtils.CreateOrderRequest(new CartLineRequest { ProductId = Guid.NewGuid() });
			order.Country = country;
			order.PostalCode = postalCode;

			Assert.Equal(valid, IsValid(address));
			Assert.Equal(valid, IsValid(order));
		}

		[Fact]
		public void EventPickup_IgnoresTheDeliveryCountryAndPostalCode()
		{
			var order = TestUtils.CreateOrderRequest(new CartLineRequest { ProductId = Guid.NewGuid() });
			order.DeliveryMethod = DeliveryMethod.EventPickup;
			order.Country = "RO";
			order.PostalCode = null;

			Assert.True(IsValid(order));
		}
	}
}
