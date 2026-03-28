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
    }
}
