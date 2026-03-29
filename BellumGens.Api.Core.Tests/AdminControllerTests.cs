using System.Collections.Generic;
using System.Linq;
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
    public class AdminControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly EmailServiceProvider _emailService;
        private readonly Mock<ILogger<AdminController>> _mockLogger;

        public AdminControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _emailService = TestUtils.CreateMockEmailServiceProvider();
            _mockLogger = TestUtils.CreateMockLogger<AdminController>();
        }

        private AdminController CreateController(BellumGensDbContext dbContext)
        {
            return new AdminController(
                _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
        }

        [Fact]
        public async Task GetRoles_ReturnsRolesList()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();

            dbContext.Roles.Add(new IdentityRole { Id = "1", Name = "admin", NormalizedName = "ADMIN" });
            dbContext.Roles.Add(new IdentityRole { Id = "2", Name = "event-admin", NormalizedName = "EVENT-ADMIN" });
            dbContext.SaveChanges();

            _mockRoleManager.Setup(r => r.Roles)
                .Returns(dbContext.Roles);

            var controller = CreateController(dbContext);

            List<string> result = await controller.GetRoles();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains("admin", result);
            Assert.Contains("event-admin", result);
        }

        [Fact]
        public async Task GetUsers_AsAdmin_ReturnsUserList()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "admin1";
            var user = new ApplicationUser { Id = userId, UserName = "adminuser" };
            dbContext.Users.Add(user);
            dbContext.Users.Add(new ApplicationUser { Id = "user2", UserName = "regularuser" });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin")).ReturnsAsync(true);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetUsers();

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetPromoCodes_AsAdmin_ReturnsPromoList()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "admin1";
            var user = new ApplicationUser { Id = userId, UserName = "adminuser" };
            dbContext.Users.Add(user);
            dbContext.PromoCodes.Add(new Promo { Code = "PROMO1", Discount = 0.10m });
            dbContext.PromoCodes.Add(new Promo { Code = "PROMO2", Discount = 0.20m });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin")).ReturnsAsync(true);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetPromoCodes();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var promos = Assert.IsAssignableFrom<List<Promo>>(okResult.Value);
            Assert.Equal(2, promos.Count);
        }

        [Fact]
        public async Task Get_AdminUser_ReturnsTrue()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "admin1";
            var user = new ApplicationUser { Id = userId, UserName = "adminuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin")).ReturnsAsync(true);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.Get();

            Assert.True(result);
        }

        [Fact]
        public async Task Get_NonAdminUser_ReturnsFalse()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "regularuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin")).ReturnsAsync(false);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.Get();

            Assert.False(result);
        }

        [Fact]
        public async Task GetUserIsTournamentAdmin_EventAdmin_ReturnsTrue()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "admin1";
            var user = new ApplicationUser { Id = userId, UserName = "eventadmin" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "event-admin")).ReturnsAsync(true);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetUserIsTournamentAdmin();

            Assert.True(result);
        }

        [Fact]
        public async Task GetUserIsTournamentAdmin_NonEventAdmin_ReturnsFalse()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "regularuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "event-admin")).ReturnsAsync(false);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetUserIsTournamentAdmin();

            Assert.False(result);
        }

        [Fact]
        public async Task CreateRole_Success_ReturnsOk()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();

            _mockRoleManager.Setup(r => r.CreateAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Success);

            var controller = CreateController(dbContext);

            var result = await controller.CreateRole("newrole");

            Assert.IsType<OkResult>(result);
            _mockRoleManager.Verify(r => r.CreateAsync(It.Is<IdentityRole>(role => role.Name == "newrole")), Times.Once);
        }

        [Fact]
        public async Task CreateRole_Failure_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();

            _mockRoleManager.Setup(r => r.CreateAsync(It.IsAny<IdentityRole>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Error" }));

            var controller = CreateController(dbContext);

            var result = await controller.CreateRole("badrole");

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task AddUserToRole_Success_ReturnsOk()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.AddToRoleAsync(user, "admin")).ReturnsAsync(IdentityResult.Success);

            var controller = CreateController(dbContext);

            var result = await controller.AddUserToRole(userId, "admin");

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Ok", okResult.Value);
        }

        [Fact]
        public async Task AddUserToRole_Failure_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.AddToRoleAsync(user, "admin"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Error" }));

            var controller = CreateController(dbContext);

            var result = await controller.AddUserToRole(userId, "admin");

            Assert.IsType<BadRequestResult>(result);
        }
    }
}
