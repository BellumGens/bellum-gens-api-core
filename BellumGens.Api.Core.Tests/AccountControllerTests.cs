using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BellumGens.Api.Controllers;
using BellumGens.Api.Core;
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

        [Fact]
        public async Task Get_Authenticated_NoSteamOrBattleNet_ReturnsOk()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "auth-user-1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser", SteamID = null, BattleNetId = null };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.Get();

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetUserNotifications_ReturnsTeamInvites()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "notif-user-1";
            var inviterId = "inviter-user-1";
            var user = new ApplicationUser { Id = userId, UserName = "notifuser" };
            var inviter = new ApplicationUser { Id = inviterId, UserName = "inviter" };
            var team = new CSGOTeam { TeamId = Guid.NewGuid(), TeamName = "TestTeam", CustomUrl = "test-team" };
            dbContext.Users.Add(user);
            dbContext.Users.Add(inviter);
            dbContext.CSGOTeams.Add(team);
            dbContext.SaveChanges();

            dbContext.TeamInvites.Add(new TeamInvite
            {
                InvitedUserId = userId,
                InvitingUserId = inviterId,
                TeamId = team.TeamId
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetUserNotifications();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var invites = Assert.IsAssignableFrom<List<TeamInvite>>(okResult.Value);
            Assert.Single(invites);
        }

        [Fact]
        public async Task GetUserTeams_ReturnsAdminTeams()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "teams-user-1";
            var user = new ApplicationUser { Id = userId, UserName = "teamadmin" };
            var team = new CSGOTeam { TeamId = Guid.NewGuid(), TeamName = "AdminTeam", CustomUrl = "admin-team" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(team);
            dbContext.SaveChanges();

            dbContext.TeamMembers.Add(new TeamMember { TeamId = team.TeamId, UserId = userId, IsAdmin = true, IsActive = true });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetUserTeams();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var teams = Assert.IsAssignableFrom<List<CSGOTeamSummaryViewModel>>(okResult.Value);
            Assert.Single(teams);
        }

        [Fact]
        public async Task Logout_ReturnsOk()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "logout-user-1";

            _mockSignInManager.Setup(m => m.SignOutAsync()).Returns(Task.CompletedTask);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.Logout();

            Assert.IsType<OkResult>(result);
            _mockSignInManager.Verify(m => m.SignOutAsync(), Times.Once);
        }

        [Fact]
        public async Task ConfirmEmail_NullUserId_RedirectsToError()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);

            var result = await controller.ConfirmEmail(null, "somecode");

            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains("/emailconfirm/error", redirectResult.Url);
        }

        [Fact]
        public async Task ConfirmEmail_NullCode_RedirectsToError()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);

            var result = await controller.ConfirmEmail("someuser", null);

            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains("/emailconfirm/error", redirectResult.Url);
        }

        [Fact]
        public async Task ConfirmEmail_UserNotFound_RedirectsToError()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            _mockUserManager.Setup(m => m.FindByIdAsync("nonexistent")).ReturnsAsync((ApplicationUser)null);

            var controller = CreateController(dbContext);

            var result = await controller.ConfirmEmail("nonexistent", "somecode");

            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains("/emailconfirm/error", redirectResult.Url);
        }

        [Fact]
        public async Task ConfirmEmail_ValidUser_SuccessRedirects()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "confirm-user-1";
            var user = new ApplicationUser { Id = userId, UserName = "confirmuser" };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.ConfirmEmailAsync(user, "validcode"))
                .ReturnsAsync(IdentityResult.Success);

            var controller = CreateController(dbContext);

            var result = await controller.ConfirmEmail(userId, "validcode");

            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.EndsWith("/emailconfirm", redirectResult.Url);
            Assert.DoesNotContain("error", redirectResult.Url);
        }

        [Fact]
        public async Task ConfirmEmail_ValidUser_FailedConfirmation_RedirectsToError()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "confirm-fail-user";
            var user = new ApplicationUser { Id = userId, UserName = "failuser" };

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.ConfirmEmailAsync(user, "badcode"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

            var controller = CreateController(dbContext);

            var result = await controller.ConfirmEmail(userId, "badcode");

            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains("/emailconfirm/error", redirectResult.Url);
        }

        [Fact]
        public async Task Delete_MismatchedUser_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "delete-user-1";
            var user = new ApplicationUser { Id = userId, UserName = "deleteuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.Delete("different-user-id");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Delete_Success_RemovesUserAndAssociatedData()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "delete-success-1";
            var user = new ApplicationUser { Id = userId, UserName = "deletesuccessuser", SteamID = "steam123", BattleNetId = "bnet123" };
            dbContext.Users.Add(user);
            dbContext.CSGODetails.Add(new CSGODetails { SteamId = "steam123" });
            dbContext.StarCraft2Details.Add(new StarCraft2Details { BattleNetId = "bnet123" });
            dbContext.SaveChanges();

            dbContext.BellumGensPushSubscriptions.Add(new BellumGensPushSubscription
            {
                UserId = userId, Endpoint = "https://example.com", P256dh = "key1", Auth = "auth1"
            });
            var team = new CSGOTeam { TeamId = Guid.NewGuid(), TeamName = "Team", CustomUrl = "team-url" };
            dbContext.CSGOTeams.Add(team);
            dbContext.SaveChanges();

            var inviterId = "delete-inviter-1";
            var inviter = new ApplicationUser { Id = inviterId, UserName = "inviter" };
            dbContext.Users.Add(inviter);
            dbContext.SaveChanges();

            dbContext.TeamInvites.Add(new TeamInvite
            {
                InvitedUserId = userId,
                InvitingUserId = inviterId,
                TeamId = team.TeamId
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockSignInManager.Setup(m => m.SignOutAsync()).Returns(Task.CompletedTask);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.Delete(userId);

            Assert.IsType<OkResult>(result);
            Assert.Empty(dbContext.BellumGensPushSubscriptions.Where(s => s.UserId == userId));
            Assert.Empty(dbContext.TeamInvites.Where(i => i.InvitedUserId == userId));
            Assert.Null(dbContext.Users.Find(userId));
        }
    }
}
