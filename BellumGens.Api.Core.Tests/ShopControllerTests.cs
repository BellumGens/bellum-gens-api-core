using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    public class ShopControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly EmailServiceProvider _emailService;
        private readonly Mock<ILogger<ShopController>> _mockLogger;

        public ShopControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _emailService = TestUtils.CreateMockEmailServiceProvider();
            _mockLogger = TestUtils.CreateMockLogger<ShopController>();
        }

        private ShopController CreateController(BellumGensDbContext dbContext)
        {
            return new ShopController(
                _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
        }

        [Fact]
        public async Task CheckPromo_ReturnsPromo_WhenExists()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.PromoCodes.Add(new Promo { Code = "SAVE10", Discount = 0.10m });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);

            var result = await controller.CheckPromo("save10");

            var okResult = Assert.IsType<OkObjectResult>(result);
            var promo = Assert.IsType<Promo>(okResult.Value);
            Assert.Equal("SAVE10", promo.Code);
            Assert.Equal(0.10m, promo.Discount);
        }

        [Fact]
        public async Task SubmitOrder_ValidOrder_SavesAndReturnsOk()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);

            var order = new JerseyOrder
            {
                Email = "test@example.com",
                FirstName = "John",
                LastName = "Doe",
                PhoneNumber = "1234567890",
                City = "Sofia",
                StreetAddress = "123 Main St",
                Jerseys = new List<JerseyDetails>
                {
                    new JerseyDetails { Cut = JerseyCut.Male, Size = JerseySize.M }
                }
            };

            var result = await controller.SubmitOrder(order);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task SubmitOrder_InvalidModel_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);
            controller.ModelState.AddModelError("Email", "Required");

            var order = new JerseyOrder();

            var result = await controller.SubmitOrder(order);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetOrders_NonAdmin_ReturnsUnauthorized()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin"))
                .ReturnsAsync(false);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetOrders();

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task DeleteOrder_OrderNotFound_ReturnsNotFound()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "admin1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId))
                .ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin"))
                .ReturnsAsync(true);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.DeleteOrder(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetOrders_AsAdmin_ReturnsOrders()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "admin1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            var orderId = Guid.NewGuid();
            dbContext.JerseyOrders.Add(new JerseyOrder
            {
                Id = orderId, Email = "test@example.com", FirstName = "John",
                LastName = "Doe", PhoneNumber = "123", City = "Sofia",
                StreetAddress = "123 Main St"
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin")).ReturnsAsync(true);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetOrders();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var orders = Assert.IsAssignableFrom<List<JerseyOrder>>(okResult.Value);
            Assert.Single(orders);
        }

        [Fact]
        public async Task DeleteOrder_AsAdmin_DeletesOrder()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "admin1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            var orderId = Guid.NewGuid();
            dbContext.JerseyOrders.Add(new JerseyOrder
            {
                Id = orderId, Email = "test@example.com", FirstName = "John",
                LastName = "Doe", PhoneNumber = "123", City = "Sofia",
                StreetAddress = "123 Main St"
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin")).ReturnsAsync(true);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.DeleteOrder(orderId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(orderId, (Guid)okResult.Value);
            Assert.Empty(dbContext.JerseyOrders);
        }

        [Fact]
        public async Task CheckPromo_NonExistent_ReturnsOkNull()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);

            var result = await controller.CheckPromo("NOTEXIST");

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Null(okResult.Value);
        }
    }
}
