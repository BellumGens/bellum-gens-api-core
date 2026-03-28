using System;
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
    public class AccountControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly EmailServiceProvider _emailService;
        private readonly Mock<ISteamService> _mockSteamService;
        private readonly Mock<IBattleNetService> _mockBattleNetService;
        private readonly Mock<ILogger<AccountController>> _mockLogger;

        public AccountControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _emailService = TestUtils.CreateMockEmailServiceProvider();
            _mockSteamService = TestUtils.CreateMockSteamService();
            _mockBattleNetService = TestUtils.CreateMockBattleNetService();
            _mockLogger = TestUtils.CreateMockLogger<AccountController>();
        }

        private AccountController CreateController(BellumGensDbContext dbContext)
        {
            return new AccountController(
                _mockSteamService.Object, _mockBattleNetService.Object,
                _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
        }

        [Fact]
        public async Task GetUsername_ReturnsTrue_WhenUsernameExists()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.Users.Add(new ApplicationUser { Id = "user1", UserName = "existinguser" });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);

            var result = await controller.GetUsername("existinguser");

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(true, okResult.Value);
        }

        [Fact]
        public async Task GetUsername_ReturnsFalse_WhenUsernameDoesNotExist()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();

            var controller = CreateController(dbContext);

            var result = await controller.GetUsername("nonexistent");

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(false, okResult.Value);
        }

        [Fact]
        public async Task Subscribe_ValidEmail_ReturnsOk()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);

            var subscriber = new Subscriber { Email = "test@example.com" };
            var result = await controller.Subscribe(subscriber);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            var message = okResult.Value.GetType().GetProperty("message")?.GetValue(okResult.Value)?.ToString();
            Assert.Equal("Subscribed successfully!", message);
        }

        [Fact]
        public async Task Subscribe_InvalidModel_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);
            controller.ModelState.AddModelError("Email", "Email is required");

            var subscriber = new Subscriber { Email = "" };
            var result = await controller.Subscribe(subscriber);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task EarlyBirdCount_ReturnsCount()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.EarlyBirds.Add(new EarlyBird { UserId = "user1", Email = "a@test.com" });
            dbContext.EarlyBirds.Add(new EarlyBird { UserId = "user2", Email = "b@test.com" });
            dbContext.EarlyBirds.Add(new EarlyBird { UserId = "user3", Email = "c@test.com" });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);

            var result = await controller.EarlyBirdCount();

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            var count = (int)okResult.Value.GetType().GetProperty("count")!.GetValue(okResult.Value)!;
            Assert.Equal(3, count);
        }

        [Fact]
        public async Task Unsubscribe_ValidSubscriber_Redirects()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var subKey = Guid.NewGuid();
            dbContext.Subscribers.Add(new Subscriber { Email = "unsub@test.com", Subscribed = true, SubKey = subKey });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);

            var result = await controller.Unsubscribe("unsub@test.com", subKey);

            Assert.IsType<RedirectResult>(result);
        }

        [Fact]
        public async Task Unsubscribe_InvalidSubKey_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.Subscribers.Add(new Subscriber { Email = "unsub@test.com", Subscribed = true, SubKey = Guid.NewGuid() });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);

            var result = await controller.Unsubscribe("unsub@test.com", Guid.NewGuid());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Get_Unauthenticated_ReturnsUnauthorized()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.Get();

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}
