using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BellumGens.Api.Controllers;
using BellumGens.Api.Core;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StarCraft2Models;
using SteamModels;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace BellumGens.Api.Core.Tests
{
	public class AccountControllerTests
	{
		private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
		private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
		private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
		private readonly Mock<IEmailService> _mockEmailService;
		private readonly Mock<ISteamService> _mockSteamService;
		private readonly Mock<IBattleNetService> _mockBattleNetService;
		private readonly Mock<ILogger<AccountController>> _mockLogger;

		public AccountControllerTests()
		{
			_mockUserManager = TestUtils.CreateMockUserManager();
			_mockRoleManager = TestUtils.CreateMockRoleManager();
			_mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
			_mockEmailService = TestUtils.CreateMockEmailService();
			_mockSteamService = TestUtils.CreateMockSteamService();
			_mockBattleNetService = TestUtils.CreateMockBattleNetService();
			_mockLogger = TestUtils.CreateMockLogger<AccountController>();
		}

		private AccountController CreateController(BellumGensDbContext dbContext, IEmailService? emailService = null)
		{
			return new AccountController(
				_mockSteamService.Object, _mockBattleNetService.Object,
				_mockUserManager.Object, _mockRoleManager.Object,
				_mockSignInManager.Object, emailService ?? _mockEmailService.Object, dbContext, _mockLogger.Object);
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
			_mockUserManager.Setup(m => m.FindByIdAsync("nonexistent")).ReturnsAsync((ApplicationUser?)null);

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
		private static IEmailService CreateUnsendableEmailService()
		{
			var emailService = new Mock<IEmailService>();
			emailService.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
				.ThrowsAsync(new System.Net.Mail.SmtpException("SMTP unavailable"));
			return emailService.Object;
		}

		private static Mock<IUrlHelper> SetupUrlHelper(AccountController controller)
		{
			var mockUrl = new Mock<IUrlHelper>();
			mockUrl.SetupGet(u => u.ActionContext).Returns(controller.ControllerContext);
			mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://api.example.com/api/Account/Action");
			mockUrl.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("https://api.example.com/api/Account/Route");
			controller.Url = mockUrl.Object;
			return mockUrl;
		}

		private static object? GetPropertyValue(object? value, string name)
		{
			return value?.GetType().GetProperty(name)?.GetValue(value);
		}

		private static ExternalLoginInfo CreateExternalLoginInfo(string provider, string providerKey, string name = "extuser", string email = "ext@example.com")
		{
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Name, name),
				new Claim(ClaimTypes.Email, email)
			};
			var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, provider));
			return new ExternalLoginInfo(principal, provider, providerKey, provider);
		}

		private static string SwapScheme(string origin)
		{
			return origin.StartsWith("https://") ? "http://" + origin.Substring(8) : "https://" + origin.Substring(7);
		}

		public static TheoryData<string> AllowedReturnUrls()
		{
			var data = new TheoryData<string>();
			foreach (string origin in CORSConfig.validOrigins)
			{
				data.Add(origin);
				data.Add(origin + "/team/some-team");
			}
			return data;
		}

		public static TheoryData<string> DisallowedReturnUrls()
		{
			return new TheoryData<string>
			{
				"",
				"https://evil.com",
				"http://evil.com/login",
				"//evil.com",
				"javascript:alert(1)",
				"evil.com/" + CORSConfig.returnOrigin,
				"https://evil.com/?next=" + CORSConfig.returnOrigin,
				CORSConfig.returnOrigin.Substring(0, CORSConfig.returnOrigin.Length - 1),
				SwapScheme(CORSConfig.returnOrigin),
				CORSConfig.returnOrigin + ".evil.com",
				CORSConfig.returnOrigin + ".evil.com/players",
				CORSConfig.returnOrigin + "@evil.com",
				CORSConfig.returnOrigin + "0",
				"/players"
			};
		}

		[Theory]
		[MemberData(nameof(AllowedReturnUrls))]
		public async Task ExternalCallback_Error_AllowedReturnUrl_RedirectsToReturnUrl(string returnUrl)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback("access_denied", returnUrl);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(returnUrl + "/unauthorized", redirectResult.Url);
			_mockSignInManager.Verify(m => m.GetExternalLoginInfoAsync(It.IsAny<string>()), Times.Never);
		}

		[Theory]
		[MemberData(nameof(DisallowedReturnUrls))]
		public async Task ExternalCallback_Error_DisallowedReturnUrl_RedirectsToDefaultOrigin(string returnUrl)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback("access_denied", returnUrl);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
			Assert.DoesNotContain("evil", redirectResult.Url);
			_mockSignInManager.Verify(m => m.GetExternalLoginInfoAsync(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task ExternalCallback_Error_DefaultReturnUrl_RedirectsToDefaultOrigin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback("access_denied");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
		}

		[Fact]
		public async Task ExternalCallback_NewTwitchUser_RegistersAndRedirectsToRegister()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var info = CreateExternalLoginInfo("Twitch", "twitch-123", "twitchuser", "twitch@example.com");
			ApplicationUser? createdUser = null;

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Twitch", "twitch-123")).ReturnsAsync((ApplicationUser?)null);
			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>()))
				.Callback<ApplicationUser>(u => createdUser = u)
				.ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()))
				.ReturnsAsync(IdentityResult.Success);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync("Twitch", "twitch-123", true, true))
				.ReturnsAsync(IdentitySignInResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/players");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/register", redirectResult.Url);
			Assert.NotNull(createdUser);
			Assert.Equal("twitchuser", createdUser.UserName);
			Assert.Equal("twitch@example.com", createdUser.Email);
			Assert.True(createdUser.EmailConfirmed);
			Assert.Equal("twitch-123", createdUser.TwitchId);
			_mockUserManager.Verify(m => m.AddLoginAsync(createdUser,
				It.Is<UserLoginInfo>(l => l.LoginProvider == "Twitch" && l.ProviderKey == "twitch-123")), Times.Once);
		}

		[Fact]
		public async Task ExternalCallback_NewSteamUser_RegistersWithResolvedSteamId()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var providerKey = "https://steamcommunity.com/openid/id/76561198000000009";
			var info = CreateExternalLoginInfo("Steam", providerKey, "steamuser");
			ApplicationUser? createdUser = null;

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Steam", providerKey)).ReturnsAsync((ApplicationUser?)null);
			_mockSteamService.Setup(s => s.SteamUserId(providerKey)).Returns("76561198000000009");
			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>()))
				.Callback<ApplicationUser>(u => createdUser = u)
				.ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()))
				.ReturnsAsync(IdentityResult.Success);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync("Steam", providerKey, true, true))
				.ReturnsAsync(IdentitySignInResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/register", redirectResult.Url);
			Assert.NotNull(createdUser);
			Assert.Equal("76561198000000009", createdUser.SteamID);
			Assert.Equal("76561198000000009", createdUser.CSGODetails.SteamId);
			Assert.True(createdUser.EmailConfirmed);
		}

		[Fact]
		public async Task ExternalCallback_NewBattleNetUser_RegistersWithStarCraft2Details()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var info = CreateExternalLoginInfo("BattleNet", "bnet-555", "Gamer#1234");
			ApplicationUser? createdUser = null;

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("BattleNet", "bnet-555")).ReturnsAsync((ApplicationUser?)null);
			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>()))
				.Callback<ApplicationUser>(u => createdUser = u)
				.ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()))
				.ReturnsAsync(IdentityResult.Success);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync("BattleNet", "bnet-555", true, true))
				.ReturnsAsync(IdentitySignInResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/register", redirectResult.Url);
			Assert.NotNull(createdUser);
			Assert.Equal("bnet-555", createdUser.BattleNetId);
			Assert.Equal("bnet-555", createdUser.StarCraft2Details.BattleNetId);
			Assert.Equal("Gamer#1234", createdUser.StarCraft2Details.BattleNetBattleTag);
		}

		[Fact]
		public async Task ExternalCallback_RegisterFails_AllowedReturnUrl_RedirectsToReturnUrlUnauthorized()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var info = CreateExternalLoginInfo("Twitch", "twitch-dup");
			var returnUrl = CORSConfig.returnOrigin + "/players";

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Twitch", "twitch-dup")).ReturnsAsync((ApplicationUser?)null);
			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>()))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Username taken" }));

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: returnUrl);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(returnUrl + "/unauthorized", redirectResult.Url);
			_mockUserManager.Verify(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()), Times.Never);
			_mockSignInManager.Verify(m => m.ExternalLoginSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
		}

		[Fact]
		public async Task ExternalCallback_RegisterFails_DisallowedReturnUrl_RedirectsToDefaultOrigin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var info = CreateExternalLoginInfo("Twitch", "twitch-dup");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Twitch", "twitch-dup")).ReturnsAsync((ApplicationUser?)null);
			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>()))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Username taken" }));

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: "https://evil.com/phish");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
		}

		[Fact]
		public async Task ExternalCallback_RegisterAddLoginFails_RedirectsToUnauthorized()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var info = CreateExternalLoginInfo("Twitch", "twitch-456");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Twitch", "twitch-456")).ReturnsAsync((ApplicationUser?)null);
			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Login already associated" }));

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
			_mockSignInManager.Verify(m => m.ExternalLoginSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
		}

		[Fact]
		public async Task ExternalCallback_ExistingConfirmedUser_SignsInAndRedirectsToReturnPath()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var user = new ApplicationUser { Id = "ext-existing-1", UserName = "existing", EmailConfirmed = true, TwitchId = "twitch-1" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("Twitch", "twitch-1");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Twitch", "twitch-1")).ReturnsAsync(user);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync("Twitch", "twitch-1", true, true))
				.ReturnsAsync(IdentitySignInResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/team/my-team");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/team/my-team", redirectResult.Url);
			_mockUserManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
		}

		[Theory]
		[MemberData(nameof(DisallowedReturnUrls))]
		public async Task ExternalCallback_SignInSucceeds_DisallowedReturnUrl_RedirectsToDefaultOrigin(string returnUrl)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var user = new ApplicationUser { Id = "ext-redirect-1", UserName = "redirect", EmailConfirmed = true, TwitchId = "twitch-r" };
			var info = CreateExternalLoginInfo("Twitch", "twitch-r");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Twitch", "twitch-r")).ReturnsAsync(user);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync("Twitch", "twitch-r", true, true))
				.ReturnsAsync(IdentitySignInResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: returnUrl);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/", redirectResult.Url);
		}

		[Theory]
		[MemberData(nameof(DisallowedReturnUrls))]
		public async Task ExternalCallback_AddLogin_DisallowedReturnUrl_RedirectsToDefaultOrigin(string returnUrl)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var info = CreateExternalLoginInfo("Twitch", "twitch-add");
			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			var user = new ApplicationUser { Id = "add-login-user", UserName = "addlogin" };
			_mockUserManager.Setup(m => m.FindByIdAsync("add-login-user")).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddLoginAsync(user, It.IsAny<UserLoginInfo>()))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "taken" }));

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("add-login-user"));

			var result = await controller.ExternalCallback(returnUrl: returnUrl, userId: "add-login-user");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized/taken", redirectResult.Url);
		}

		[Fact]
		public async Task ExternalCallback_Error_NullReturnUrl_RedirectsToDefaultOrigin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback("access_denied", null!);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
		}

		[Fact]
		public async Task ExternalCallback_NoExternalLoginInfo_RedirectsToUnauthorized()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync((ExternalLoginInfo?)null);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/players");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
			_mockSignInManager.Verify(m => m.ExternalLoginSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
		}

		[Fact]
		public async Task ExternalCallback_EmptyReturnUrl_RedirectsToDefaultOrigin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var user = new ApplicationUser { Id = "ext-existing-2", UserName = "existing2", EmailConfirmed = true };
			var info = CreateExternalLoginInfo("Twitch", "twitch-2");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Twitch", "twitch-2")).ReturnsAsync(user);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync("Twitch", "twitch-2", true, true))
				.ReturnsAsync(IdentitySignInResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback();

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/", redirectResult.Url);
		}

		[Fact]
		public async Task ExternalCallback_SignInFails_RedirectsToUnauthorized()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var user = new ApplicationUser { Id = "ext-locked-1", UserName = "locked", EmailConfirmed = true };
			var info = CreateExternalLoginInfo("Twitch", "twitch-locked");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Twitch", "twitch-locked")).ReturnsAsync(user);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync("Twitch", "twitch-locked", true, true))
				.ReturnsAsync(IdentitySignInResult.LockedOut);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/players");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
		}

		[Fact]
		public async Task ExternalCallback_ExistingUnconfirmedBattleNetUser_ConfirmsAndLinksBattleNet()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "ext-bnet-1";
			var user = new ApplicationUser { Id = userId, UserName = "bnetuser", EmailConfirmed = false };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("BattleNet", "bnet-9", "Hero#9999");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("BattleNet", "bnet-9")).ReturnsAsync(user);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync("BattleNet", "bnet-9", true, true))
				.ReturnsAsync(IdentitySignInResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/players");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/players", redirectResult.Url);
			var savedUser = dbContext.Users.AsNoTracking().Single(u => u.Id == userId);
			Assert.True(savedUser.EmailConfirmed);
			Assert.Equal("bnet-9", savedUser.BattleNetId);
			var sc2 = dbContext.StarCraft2Details.AsNoTracking().Single(d => d.BattleNetId == "bnet-9");
			Assert.Equal("Hero#9999", sc2.BattleNetBattleTag);
		}

		[Fact]
		public async Task ExternalCallback_AddTwitchLogin_UpdatesTwitchIdAndRedirects()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "addlogin-twitch-1";
			var user = new ApplicationUser { Id = userId, UserName = "linker" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("Twitch", "twitch-42");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddLoginAsync(user, It.IsAny<UserLoginInfo>())).ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: userId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/profile", redirectResult.Url);
			Assert.Equal("twitch-42", dbContext.Users.AsNoTracking().Single(u => u.Id == userId).TwitchId);
			_mockUserManager.Verify(m => m.AddLoginAsync(user,
				It.Is<UserLoginInfo>(l => l.LoginProvider == "Twitch" && l.ProviderKey == "twitch-42")), Times.Once);
			_mockUserManager.Verify(m => m.FindByLoginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
			_mockSignInManager.Verify(m => m.ExternalLoginSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
		}

		[Fact]
		public async Task ExternalCallback_AddBattleNetLogin_CreatesStarCraft2Details()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "addlogin-bnet-1";
			var user = new ApplicationUser { Id = userId, UserName = "linker" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("BattleNet", "bnet-77", "Linker#7777");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddLoginAsync(user, It.IsAny<UserLoginInfo>())).ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: userId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/profile", redirectResult.Url);
			Assert.Equal("bnet-77", dbContext.Users.AsNoTracking().Single(u => u.Id == userId).BattleNetId);
			var sc2 = dbContext.StarCraft2Details.AsNoTracking().Single(d => d.BattleNetId == "bnet-77");
			Assert.Equal("Linker#7777", sc2.BattleNetBattleTag);
		}

		[Fact]
		public async Task ExternalCallback_AddSteamLogin_AlreadyLinked_DoesNotChangeSteamId()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "addlogin-steam-1";
			var steamId = "76561198000000001";
			var providerKey = "https://steamcommunity.com/openid/id/" + steamId;
			dbContext.CSGODetails.Add(new CSGODetails { SteamId = steamId });
			var user = new ApplicationUser { Id = userId, UserName = "steamlinker", SteamID = steamId };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("Steam", providerKey);

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddLoginAsync(user, It.IsAny<UserLoginInfo>())).ReturnsAsync(IdentityResult.Success);
			_mockSteamService.Setup(s => s.SteamUserId(providerKey)).Returns(steamId);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: userId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/profile", redirectResult.Url);
			Assert.Equal(steamId, dbContext.Users.AsNoTracking().Single(u => u.Id == userId).SteamID);
			_mockSteamService.Verify(s => s.SteamUserId(providerKey), Times.Once);
		}

		[Fact]
		public async Task ExternalCallback_AddLoginFails_RedirectsToUnauthorizedWithError()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "addlogin-fail-1";
			var user = new ApplicationUser { Id = userId, UserName = "linker" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("Twitch", "twitch-taken");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddLoginAsync(user, It.IsAny<UserLoginInfo>()))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "LoginAlreadyAssociated" }));

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: userId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized/LoginAlreadyAssociated", redirectResult.Url);
			Assert.Null(dbContext.Users.AsNoTracking().Single(u => u.Id == userId).TwitchId);
		}

		[Fact]
		public async Task ExternalCallback_AddLogin_Unauthenticated_DoesNotLinkLogin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var victimId = "addlogin-victim-1";
			var victim = new ApplicationUser { Id = victimId, UserName = "victim" };
			dbContext.Users.Add(victim);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("Twitch", "attacker-twitch");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(victimId)).ReturnsAsync(victim);
			_mockUserManager.Setup(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>())).ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: victimId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
			Assert.Null(dbContext.Users.AsNoTracking().Single(u => u.Id == victimId).TwitchId);
			_mockUserManager.Verify(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()), Times.Never);
		}

		[Fact]
		public async Task ExternalCallback_AddLogin_DifferentSignedInUser_DoesNotLinkLogin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var victimId = "addlogin-victim-2";
			var attackerId = "addlogin-attacker-2";
			var victim = new ApplicationUser { Id = victimId, UserName = "victim" };
			var attacker = new ApplicationUser { Id = attackerId, UserName = "attacker" };
			dbContext.Users.AddRange(victim, attacker);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("Twitch", "attacker-twitch");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(victimId)).ReturnsAsync(victim);
			_mockUserManager.Setup(m => m.FindByIdAsync(attackerId)).ReturnsAsync(attacker);
			_mockUserManager.Setup(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>())).ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(attackerId));

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: victimId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/unauthorized", redirectResult.Url);
			Assert.Null(dbContext.Users.AsNoTracking().Single(u => u.Id == victimId).TwitchId);
			_mockUserManager.Verify(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()), Times.Never);
		}

		[Fact]
		public async Task ExternalCallback_AddSteamLogin_UserWithoutCSGODetails_CreatesDetails()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "addlogin-steam-new-1";
			var steamId = "76561198000000021";
			var providerKey = "https://steamcommunity.com/openid/id/" + steamId;
			var user = new ApplicationUser { Id = userId, UserName = "nosteam" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("Steam", providerKey);

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddLoginAsync(user, It.IsAny<UserLoginInfo>())).ReturnsAsync(IdentityResult.Success);
			_mockSteamService.Setup(s => s.SteamUserId(providerKey)).Returns(steamId);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: userId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/profile", redirectResult.Url);
			Assert.Equal(steamId, dbContext.Users.AsNoTracking().Single(u => u.Id == userId).SteamID);
			Assert.Single(dbContext.CSGODetails.AsNoTracking().Where(d => d.SteamId == steamId));
		}

		[Fact]
		public async Task ExternalCallback_AddSteamLogin_NewSteamIdWithUnloadedDetails_ReplacesDetails()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "addlogin-steam-change-1";
			var oldSteamId = "76561198000000031";
			var newSteamId = "76561198000000032";
			var providerKey = "https://steamcommunity.com/openid/id/" + newSteamId;
			dbContext.CSGODetails.Add(new CSGODetails { SteamId = oldSteamId, AvatarFull = "https://avatars/old.jpg" });
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "steamchanger", SteamID = oldSteamId });
			dbContext.SaveChanges();
			dbContext.ChangeTracker.Clear();
			var user = dbContext.Users.Single(u => u.Id == userId);
			Assert.Null(user.CSGODetails);
			var info = CreateExternalLoginInfo("Steam", providerKey);

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddLoginAsync(user, It.IsAny<UserLoginInfo>())).ReturnsAsync(IdentityResult.Success);
			_mockSteamService.Setup(s => s.SteamUserId(providerKey)).Returns(newSteamId);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: userId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/profile", redirectResult.Url);
			Assert.Equal(newSteamId, dbContext.Users.AsNoTracking().Single(u => u.Id == userId).SteamID);
			Assert.Single(dbContext.CSGODetails.AsNoTracking().Where(d => d.SteamId == newSteamId));
			Assert.Empty(dbContext.CSGODetails.AsNoTracking().Where(d => d.SteamId == oldSteamId));
		}

		[Fact]
		public async Task ExternalCallback_AddSteamLogin_NewSteamIdWithLoadedDetails_DoesNotModifyDetailsKey()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "addlogin-steam-change-2";
			var oldSteamId = "76561198000000041";
			var newSteamId = "76561198000000042";
			var providerKey = "https://steamcommunity.com/openid/id/" + newSteamId;
			var oldDetails = new CSGODetails { SteamId = oldSteamId };
			var user = new ApplicationUser { Id = userId, UserName = "steamchanger2", SteamID = oldSteamId, CSGODetails = oldDetails };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo("Steam", providerKey);

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddLoginAsync(user, It.IsAny<UserLoginInfo>())).ReturnsAsync(IdentityResult.Success);
			_mockSteamService.Setup(s => s.SteamUserId(providerKey)).Returns(newSteamId);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/profile", userId: userId);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/profile", redirectResult.Url);
			Assert.Equal(oldSteamId, oldDetails.SteamId);
			Assert.Equal(newSteamId, user.CSGODetails.SteamId);
			Assert.Equal(newSteamId, dbContext.Users.AsNoTracking().Single(u => u.Id == userId).SteamID);
			Assert.Single(dbContext.CSGODetails.AsNoTracking().Where(d => d.SteamId == newSteamId));
			Assert.Empty(dbContext.CSGODetails.AsNoTracking().Where(d => d.SteamId == oldSteamId));
		}

		[Fact]
		public async Task ExternalCallback_UnknownProvider_FailsWithoutCreatingUser()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var info = CreateExternalLoginInfo("Facebook", "fb-1");

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync("Facebook", "fb-1")).ReturnsAsync((ApplicationUser?)null);
			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/players");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/players/unauthorized", redirectResult.Url);
			_mockUserManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
			_mockUserManager.Verify(m => m.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()), Times.Never);
			_mockSignInManager.Verify(m => m.ExternalLoginSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
		}

		[Theory]
		[InlineData("Twitch")]
		[InlineData("Steam")]
		public async Task ExternalCallback_ExistingUnconfirmedUser_PersistsEmailConfirmed(string provider)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "ext-unconfirmed-" + provider;
			var providerKey = provider + "-key-1";
			var user = new ApplicationUser { Id = userId, UserName = "unconfirmed", Email = "unconfirmed@example.com", EmailConfirmed = false };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var info = CreateExternalLoginInfo(provider, providerKey);

			_mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(info);
			_mockUserManager.Setup(m => m.FindByLoginAsync(provider, providerKey)).ReturnsAsync(user);
			_mockSignInManager.Setup(m => m.ExternalLoginSignInAsync(provider, providerKey, true, true))
				.ReturnsAsync(IdentitySignInResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ExternalCallback(returnUrl: CORSConfig.returnOrigin + "/players");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/players", redirectResult.Url);
			Assert.True(dbContext.Users.AsNoTracking().Single(u => u.Id == userId).EmailConfirmed);
		}

		[Fact]
		public async Task GetExternalLogin_Unauthenticated_ChallengesProviderWithoutUserId()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var properties = new AuthenticationProperties();
			UrlActionContext? actionContext = null;

			_mockSignInManager.Setup(m => m.ConfigureExternalAuthenticationProperties("Steam", "/api/Account/ExternalCallback", null))
				.Returns(properties);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
			var mockUrl = SetupUrlHelper(controller);
			mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>()))
				.Callback<UrlActionContext>(c => actionContext = c)
				.Returns("/api/Account/ExternalCallback");

			var result = await controller.GetExternalLogin("Steam", CORSConfig.returnOrigin);

			var challengeResult = Assert.IsType<ChallengeResult>(result);
			Assert.Equal(new[] { "Steam" }, challengeResult.AuthenticationSchemes);
			Assert.Same(properties, challengeResult.Properties);
			Assert.NotNull(actionContext);
			Assert.Equal("ExternalCallback", actionContext.Action);
			Assert.Equal("Account", actionContext.Controller);
			Assert.Equal(CORSConfig.returnOrigin, GetPropertyValue(actionContext.Values, "returnUrl"));
			Assert.Null(GetPropertyValue(actionContext.Values, "userId"));
			_mockUserManager.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task GetExternalLogin_Authenticated_IncludesUserIdInCallback()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "extlogin-user-1";
			var user = new ApplicationUser { Id = userId, UserName = "extlogin" };
			var properties = new AuthenticationProperties();
			UrlActionContext? actionContext = null;

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockSignInManager.Setup(m => m.ConfigureExternalAuthenticationProperties("Twitch", It.IsAny<string>(), null))
				.Returns(properties);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			var mockUrl = SetupUrlHelper(controller);
			mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>()))
				.Callback<UrlActionContext>(c => actionContext = c)
				.Returns("/api/Account/ExternalCallback");

			var result = await controller.GetExternalLogin("Twitch");

			var challengeResult = Assert.IsType<ChallengeResult>(result);
			Assert.Equal(new[] { "Twitch" }, challengeResult.AuthenticationSchemes);
			Assert.Same(properties, challengeResult.Properties);
			Assert.NotNull(actionContext);
			Assert.Equal(userId, GetPropertyValue(actionContext.Values, "userId"));
		}

		[Fact]
		public async Task GetExternalLogins_WithoutState_ReturnsLoginPerScheme()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var handlerType = new Mock<IAuthenticationHandler>().Object.GetType();
			var schemes = new List<AuthenticationScheme>
			{
				new AuthenticationScheme("Steam", "Steam Login", handlerType),
				new AuthenticationScheme("Twitch", "Twitch Login", handlerType)
			};
			var routeContexts = new List<UrlRouteContext>();

			_mockSignInManager.Setup(m => m.GetExternalAuthenticationSchemesAsync()).ReturnsAsync(schemes);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
			controller.HttpContext.Request.Scheme = "https";
			controller.HttpContext.Request.Host = new HostString("api.example.com");
			controller.HttpContext.Request.Path = "/api/Account/ExternalLogins";
			var mockUrl = SetupUrlHelper(controller);
			mockUrl.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>()))
				.Callback<UrlRouteContext>(c => routeContexts.Add(c))
				.Returns<UrlRouteContext>(c => "https://api.example.com/login/" + GetPropertyValue(c.Values, "provider"));

			var result = (await controller.GetExternalLogins("/callback")).ToList();

			Assert.Equal(2, result.Count);
			Assert.Equal("Steam Login", result[0].Name);
			Assert.Equal("https://api.example.com/login/Steam", result[0].Url);
			Assert.Null(result[0].State);
			Assert.Equal("Twitch Login", result[1].Name);
			Assert.Equal("https://api.example.com/login/Twitch", result[1].Url);
			Assert.Null(result[1].State);
			Assert.Equal(2, routeContexts.Count);
			Assert.All(routeContexts, c =>
			{
				Assert.Equal("ExternalLogin", c.RouteName);
				Assert.Equal("https", c.Protocol);
				Assert.Equal("token", GetPropertyValue(c.Values, "response_type"));
				Assert.Equal("https://api.example.com/callback", GetPropertyValue(c.Values, "redirect_uri"));
				Assert.Null(GetPropertyValue(c.Values, "state"));
			});
		}

		[Fact]
		public async Task GetExternalLogins_WithState_GeneratesSharedUrlSafeState()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var handlerType = new Mock<IAuthenticationHandler>().Object.GetType();
			var schemes = new List<AuthenticationScheme>
			{
				new AuthenticationScheme("Steam", "Steam", handlerType),
				new AuthenticationScheme("BattleNet", "BattleNet", handlerType)
			};
			var routeContexts = new List<UrlRouteContext>();

			_mockSignInManager.Setup(m => m.GetExternalAuthenticationSchemesAsync()).ReturnsAsync(schemes);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
			controller.HttpContext.Request.Scheme = "https";
			controller.HttpContext.Request.Host = new HostString("api.example.com");
			var mockUrl = SetupUrlHelper(controller);
			mockUrl.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>()))
				.Callback<UrlRouteContext>(c => routeContexts.Add(c))
				.Returns("https://api.example.com/login");

			var first = (await controller.GetExternalLogins("/", "CustomRoute", true)).ToList();
			var second = (await controller.GetExternalLogins("/", "CustomRoute", true)).ToList();

			Assert.Equal(2, first.Count);
			var state = first[0].State;
			Assert.NotNull(state);
			Assert.Equal(43, state.Length);
			Assert.Matches("^[A-Za-z0-9_-]+$", state);
			Assert.Equal(state, first[1].State);
			Assert.NotEqual(state, second[0].State);
			Assert.All(routeContexts, c => Assert.Equal("CustomRoute", c.RouteName));
			Assert.Equal(state, GetPropertyValue(routeContexts[0].Values, "state"));
		}

		[Fact]
		public async Task GetExternalLogins_NoSchemes_ReturnsEmpty()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			_mockSignInManager.Setup(m => m.GetExternalAuthenticationSchemesAsync()).ReturnsAsync(new List<AuthenticationScheme>());

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetExternalLogins("/", generateState: true);

			Assert.Empty(result);
		}

		[Fact]
		public async Task Login_InvalidCredentials_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			_mockSignInManager.Setup(m => m.PasswordSignInAsync("baduser", "wrongpass", false, true))
				.ReturnsAsync(IdentitySignInResult.Failed);

			var controller = CreateController(dbContext);

			var result = await controller.Login(new LoginBindingModel { UserName = "baduser", Password = "wrongpass" });

			Assert.IsType<BadRequestObjectResult>(result);
			_mockUserManager.Verify(m => m.FindByNameAsync(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task Login_NotAllowed_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			_mockSignInManager.Setup(m => m.PasswordSignInAsync("unverified", "Password123!", true, true))
				.ReturnsAsync(IdentitySignInResult.NotAllowed);

			var controller = CreateController(dbContext);

			var result = await controller.Login(new LoginBindingModel { UserName = "unverified", Password = "Password123!", RememberMe = true });

			Assert.IsType<BadRequestObjectResult>(result);
			_mockUserManager.Verify(m => m.FindByNameAsync(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task Login_FailedAttempt_CountsTowardsLockout()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			_mockSignInManager.Setup(m => m.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
				.ReturnsAsync(IdentitySignInResult.Failed);

			var controller = CreateController(dbContext);

			var result = await controller.Login(new LoginBindingModel { UserName = "baduser", Password = "wrongpass" });

			var badRequest = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Equal("Invalid username or password, or email not verified.", badRequest.Value);
			_mockSignInManager.Verify(m => m.PasswordSignInAsync("baduser", "wrongpass", false, true), Times.Once);
		}

		[Fact]
		public async Task Login_LockedOut_ReturnsLockedOutMessage()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			_mockSignInManager.Setup(m => m.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
				.ReturnsAsync(IdentitySignInResult.LockedOut);

			var controller = CreateController(dbContext);

			var result = await controller.Login(new LoginBindingModel { UserName = "lockeduser", Password = "Password123!" });

			var badRequest = Assert.IsType<BadRequestObjectResult>(result);
			var message = Assert.IsType<string>(badRequest.Value);
			Assert.Contains("temporarily locked", message);
			_mockUserManager.Verify(m => m.FindByNameAsync(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task Login_ValidCredentials_ReturnsUserWithExternalLogins()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var user = new ApplicationUser { Id = "login-user-1", UserName = "loginuser", Email = "login@example.com" };

			_mockSignInManager.Setup(m => m.PasswordSignInAsync("loginuser", "Password123!", true, true))
				.ReturnsAsync(IdentitySignInResult.Success);
			_mockUserManager.Setup(m => m.FindByNameAsync("loginuser")).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>
			{
				new UserLoginInfo("Twitch", "twitch-1", "Twitch"),
				new UserLoginInfo("BattleNet", "bnet-1", "BattleNet")
			});

			var controller = CreateController(dbContext);

			var result = await controller.Login(new LoginBindingModel { UserName = "loginuser", Password = "Password123!", RememberMe = true });

			var okResult = Assert.IsType<OkObjectResult>(result);
			var model = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.Equal("login-user-1", model.Id);
			Assert.Equal("loginuser", model.Username);
			Assert.Equal("login@example.com", model.Email);
			Assert.Equal(new List<string> { "Twitch", "BattleNet" }, model.ExternalLogins);
			_mockSteamService.Verify(s => s.GetSteamUserDetails(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task Login_SteamUserWithCachedAvatar_DoesNotCallSteam()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var details = new CSGODetails { SteamId = "76561198000000002", AvatarFull = "https://avatars/full.jpg" };
			var user = new ApplicationUser { Id = "login-steam-1", UserName = "steamlogin", SteamID = "76561198000000002" };
			dbContext.CSGODetails.Add(details);
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockSignInManager.Setup(m => m.PasswordSignInAsync("steamlogin", "Password123!", false, true))
				.ReturnsAsync(IdentitySignInResult.Success);
			_mockUserManager.Setup(m => m.FindByNameAsync("steamlogin")).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());

			var controller = CreateController(dbContext);

			var result = await controller.Login(new LoginBindingModel { UserName = "steamlogin", Password = "Password123!" });

			var okResult = Assert.IsType<OkObjectResult>(result);
			var model = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.Same(details, model.CSGODetails);
			Assert.Empty(model.ExternalLogins);
			_mockSteamService.Verify(s => s.GetSteamUserDetails(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task Login_SteamUserWithUnloadedDetails_LoadsDetailsInsteadOfCrashing()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			const string steamId = "76561198000000010";
			dbContext.CSGODetails.Add(new CSGODetails { SteamId = steamId, AvatarFull = "https://avatars/full.jpg" });
			dbContext.Users.Add(new ApplicationUser { Id = "login-steam-unloaded", UserName = "unloaded", SteamID = steamId });
			dbContext.SaveChanges();
			dbContext.ChangeTracker.Clear();
			var user = dbContext.Users.Single(u => u.Id == "login-steam-unloaded");
			Assert.Null(user.CSGODetails);

			_mockSignInManager.Setup(m => m.PasswordSignInAsync("unloaded", "Password123!", false, true))
				.ReturnsAsync(IdentitySignInResult.Success);
			_mockUserManager.Setup(m => m.FindByNameAsync("unloaded")).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());

			var controller = CreateController(dbContext);

			var result = await controller.Login(new LoginBindingModel { UserName = "unloaded", Password = "Password123!" });

			Assert.IsType<OkObjectResult>(result);
			_mockSteamService.Verify(s => s.GetSteamUserDetails(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task Login_SteamUserWithoutAvatar_FetchesDetailsBySteamId()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			const string steamId = "76561198000000011";
			dbContext.CSGODetails.Add(new CSGODetails { SteamId = steamId });
			var user = new ApplicationUser { Id = "login-steam-noavatar", UserName = "noavatar", SteamID = steamId };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockSignInManager.Setup(m => m.PasswordSignInAsync("noavatar", "Password123!", false, true))
				.ReturnsAsync(IdentitySignInResult.Success);
			_mockUserManager.Setup(m => m.FindByNameAsync("noavatar")).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());

			var controller = CreateController(dbContext);

			var result = await controller.Login(new LoginBindingModel { UserName = "noavatar", Password = "Password123!" });

			Assert.IsType<OkObjectResult>(result);
			_mockSteamService.Verify(s => s.GetSteamUserDetails(steamId), Times.Once);
			_mockSteamService.Verify(s => s.GetSteamUserDetails(user.Id), Times.Never);
		}

		[Fact]
		public async Task Get_SteamUserWithCachedAvatar_DoesNotCallSteam()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "get-steam-cached";
			var steamId = "76561198000000003";
			dbContext.CSGODetails.Add(new CSGODetails { SteamId = steamId, AvatarFull = "https://avatars/cached.jpg" });
			var user = new ApplicationUser { Id = userId, UserName = "cachedsteam", SteamID = steamId };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo> { new UserLoginInfo("Steam", "key", "Steam") });

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.Get();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var model = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.Equal("https://avatars/cached.jpg", model.CSGODetails.AvatarFull);
			Assert.Equal(new List<string> { "Steam" }, model.ExternalLogins);
			_mockSteamService.Verify(s => s.GetSteamUserDetails(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task Get_SteamUserWithoutAvatar_RefreshesDetailsFromSteam()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "get-steam-refresh";
			var steamId = "76561198000000004";
			dbContext.CSGODetails.Add(new CSGODetails { SteamId = steamId });
			var user = new ApplicationUser { Id = userId, UserName = "refreshsteam", SteamID = steamId };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var steamModel = new UserStatsViewModel
			{
				SteamUser = new SteamUser { steamID = "refreshsteam", avatarFull = "https://avatars/fresh.jpg" },
				UserStatsException = true
			};

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());
			_mockSteamService.Setup(s => s.GetSteamUserDetails(steamId)).ReturnsAsync(steamModel);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.Get();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var model = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.Same(steamModel, model);
			Assert.Equal(userId, model.Id);
			Assert.Empty(model.ExternalLogins);
			var savedDetails = dbContext.CSGODetails.AsNoTracking().Single(d => d.SteamId == steamId);
			Assert.Equal("https://avatars/fresh.jpg", savedDetails.AvatarFull);
			Assert.True(savedDetails.SteamPrivate);
			_mockSteamService.Verify(s => s.GetSteamUserDetails(steamId), Times.Once);
		}

		[Fact]
		public async Task Get_SteamServiceThrows_StillReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "get-steam-throws";
			var steamId = "76561198000000005";
			dbContext.CSGODetails.Add(new CSGODetails { SteamId = steamId });
			var user = new ApplicationUser { Id = userId, UserName = "throwsteam", SteamID = steamId };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());
			_mockSteamService.Setup(s => s.GetSteamUserDetails(steamId)).ThrowsAsync(new InvalidOperationException("Steam down"));

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.Get();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var model = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.Equal(userId, model.Id);
			Assert.Equal(steamId, model.SteamId);
			_mockSteamService.Verify(s => s.GetSteamUserDetails(steamId), Times.Once);
		}

		[Fact]
		public async Task Get_BattleNetUserWithoutAvatar_RefreshesStarCraft2Details()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "get-bnet-refresh";
			dbContext.StarCraft2Details.Add(new StarCraft2Details { BattleNetId = "bnet-100", BattleNetBattleTag = "Player#100" });
			var user = new ApplicationUser { Id = userId, UserName = "bnetrefresh", BattleNetId = "bnet-100" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			var player = new Player { avatarUrl = "https://sc2/avatar.png", profileUrl = "https://sc2/profile", regionId = 2, realmId = 1 };

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());
			_mockBattleNetService.Setup(s => s.GetStarCraft2Player("bnet-100", It.IsAny<string>())).ReturnsAsync(player);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.Get();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var model = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.Same(player, model.SC2Player);
			var savedDetails = dbContext.StarCraft2Details.AsNoTracking().Single(d => d.BattleNetId == "bnet-100");
			Assert.Equal("https://sc2/avatar.png", savedDetails.AvatarUrl);
			Assert.Equal("https://sc2/profile", savedDetails.ProfileUrl);
			Assert.Equal(2, savedDetails.RegionId);
			Assert.Equal(1, savedDetails.RealmId);
		}

		[Fact]
		public async Task Get_BattleNetUserWithCachedAvatar_DoesNotCallBattleNet()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "get-bnet-cached";
			dbContext.StarCraft2Details.Add(new StarCraft2Details { BattleNetId = "bnet-200", AvatarUrl = "https://sc2/cached.png" });
			var user = new ApplicationUser { Id = userId, UserName = "bnetcached", BattleNetId = "bnet-200" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.Get();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var model = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.Equal("https://sc2/cached.png", model.AvatarUrl);
			Assert.Null(model.SC2Player);
			_mockBattleNetService.Verify(s => s.GetStarCraft2Player(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_InvalidModel_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
			controller.ModelState.AddModelError("Password", "The Password must be at least 8 characters long.");

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "user", Email = "user@example.com", Password = "short" });

			Assert.IsType<BadRequestObjectResult>(result);
			_mockUserManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
			_mockUserManager.Verify(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_Unauthenticated_CreateFails_ReturnsBadRequestWithErrors()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			ApplicationUser? createdUser = null;

			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>()))
				.Callback<ApplicationUser>(u => createdUser = u)
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Username 'taken' is already taken." }));

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "taken", Email = "taken@example.com", Password = "Password123!" });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Username 'taken' is already taken.");
			Assert.NotNull(createdUser);
			Assert.Equal("taken", createdUser.UserName);
			Assert.Equal("taken@example.com", createdUser.Email);
			_mockUserManager.Verify(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_Unauthenticated_CreateFailsWithoutErrors_ReturnsEmptyBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Failed());

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "user", Email = "user@example.com", Password = "Password123!" });

			Assert.IsType<BadRequestResult>(result);
		}

		[Fact]
		public async Task SetPassword_Unauthenticated_AddPasswordFails_DeletesUserAndReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			ApplicationUser? createdUser = null;

			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>()))
				.Callback<ApplicationUser>(u => createdUser = u)
				.ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), "weakpassword"))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Passwords must have at least one digit." }));
			_mockUserManager.Setup(m => m.DeleteAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "newuser", Email = "new@example.com", Password = "weakpassword" });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Passwords must have at least one digit.");
			Assert.NotNull(createdUser);
			_mockUserManager.Verify(m => m.DeleteAsync(createdUser), Times.Once);
			_mockUserManager.Verify(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_Authenticated_AddPasswordFails_ReturnsBadRequestWithErrors()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "setpw-fail-1";
			var user = new ApplicationUser { Id = userId, UserName = "haspassword", Email = "has@example.com" };

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddPasswordAsync(user, "Password123!"))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "User already has a password set." }));

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "haspassword", Email = "has@example.com", Password = "Password123!" });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "User already has a password set.");
			_mockUserManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
			_mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_Authenticated_SameDetails_ReturnsOkWithoutUpdate()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "setpw-same-1";
			var user = new ApplicationUser { Id = userId, UserName = "sameuser", Email = "same@example.com" };

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddPasswordAsync(user, "Password123!")).ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "sameuser", Email = "same@example.com", Password = "Password123!" });

			Assert.IsType<OkResult>(result);
			_mockUserManager.Verify(m => m.AddPasswordAsync(user, "Password123!"), Times.Once);
			_mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
			_mockUserManager.Verify(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_Authenticated_NewEmail_UpdatesUsernameAndRequestsEmailChangeConfirmation()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "setpw-email-1";
			var user = new ApplicationUser { Id = userId, UserName = "oldname", Email = "old@example.com", EmailConfirmed = true };
			string? updatedUserName = null;
			string? updatedEmail = null;

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddPasswordAsync(user, "Password123!")).ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.UpdateAsync(user))
				.Callback<ApplicationUser>(u => { updatedUserName = u.UserName; updatedEmail = u.Email; })
				.ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.GenerateChangeEmailTokenAsync(user, "new@example.com")).ReturnsAsync("change-token");

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			var mockUrl = SetupUrlHelper(controller);

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "newname", Email = "new@example.com", Password = "Password123!" });

			Assert.IsType<OkResult>(result);
			Assert.Equal("newname", updatedUserName);
			Assert.Equal("old@example.com", updatedEmail);
			Assert.Equal("old@example.com", user.Email);
			Assert.True(user.EmailConfirmed);
			_mockUserManager.Verify(m => m.GenerateChangeEmailTokenAsync(user, "new@example.com"), Times.Once);
			_mockUserManager.Verify(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
			mockUrl.Verify(u => u.Action(It.Is<UrlActionContext>(c =>
				c.Action == "ConfirmEmailChange" && c.Controller == "Account" &&
				(string?)GetPropertyValue(c.Values, "code") == "change-token" &&
				(string?)GetPropertyValue(c.Values, "email") == "new@example.com" &&
				(string?)GetPropertyValue(c.Values, "userId") == userId)), Times.Once);
		}

		[Fact]
		public async Task SetPassword_Authenticated_NewUsernameOnly_PersistsUsername()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "setpw-username-1";
			var user = new ApplicationUser { Id = userId, UserName = "oldname", Email = "same@example.com" };
			string? updatedUserName = null;

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddPasswordAsync(user, "Password123!")).ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.UpdateAsync(user))
				.Callback<ApplicationUser>(u => updatedUserName = u.UserName)
				.ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "newname", Email = "same@example.com", Password = "Password123!" });

			Assert.IsType<OkResult>(result);
			Assert.Equal("newname", updatedUserName);
			_mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
			_mockUserManager.Verify(m => m.GenerateChangeEmailTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_Authenticated_UsernameUpdateFails_ReturnsBadRequestWithErrors()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "setpw-username-fail-1";
			var user = new ApplicationUser { Id = userId, UserName = "oldname", Email = "old@example.com" };

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.AddPasswordAsync(user, "Password123!")).ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.UpdateAsync(user))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Username 'taken' is already taken." }));

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "taken", Email = "new@example.com", Password = "Password123!" });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Username 'taken' is already taken.");
			Assert.Equal("old@example.com", user.Email);
			_mockUserManager.Verify(m => m.GenerateChangeEmailTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
			_mockUserManager.Verify(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_Unauthenticated_ConfirmationEmailFails_StillReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			ApplicationUser? createdUser = null;

			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>()))
				.Callback<ApplicationUser>(u => createdUser = u)
				.ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), "Password123!"))
				.ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
				.ReturnsAsync("confirm-token");

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
			var mockUrl = SetupUrlHelper(controller);

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "newuser", Email = "new@example.com", Password = "Password123!" });

			Assert.IsType<OkResult>(result);
			Assert.NotNull(createdUser);
			_mockUserManager.Verify(m => m.GenerateEmailConfirmationTokenAsync(createdUser), Times.Once);
			mockUrl.Verify(u => u.RouteUrl(It.Is<UrlRouteContext>(c =>
				c.RouteName == "ConfirmEmail" &&
				(string?)GetPropertyValue(c.Values, "code") == "confirm-token" &&
				(string?)GetPropertyValue(c.Values, "userId") == createdUser.Id)), Times.Once);
			_mockUserManager.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
		}

		[Fact]
		public async Task UpdateUserInfo_SameEmail_UpdatesSearchVisibilityWithoutConfirmation()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "prefs-user-1";
			var user = new ApplicationUser { Id = userId, UserName = "prefsuser", Email = "same@example.com", SearchVisible = true };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.UpdateUserInfo(new UserPreferencesViewModel { email = "same@example.com", searchVisible = false });

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(false, GetPropertyValue(okResult.Value, "newEmail"));
			Assert.Equal("same@example.com", GetPropertyValue(okResult.Value, "email"));
			var savedUser = dbContext.Users.AsNoTracking().Single(u => u.Id == userId);
			Assert.False(savedUser.SearchVisible);
			Assert.Equal("same@example.com", savedUser.Email);
			_mockUserManager.Verify(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData(null)]
		public async Task UpdateUserInfo_EmptyEmail_KeepsStoredEmailAndDoesNotRequestConfirmation(string? email)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "prefs-user-2";
			var user = new ApplicationUser { Id = userId, UserName = "prefsuser2", Email = "keep@example.com", EmailConfirmed = true, SearchVisible = false };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.UpdateUserInfo(new UserPreferencesViewModel { email = email!, searchVisible = true });

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(false, GetPropertyValue(okResult.Value, "newEmail"));
			Assert.Equal("keep@example.com", GetPropertyValue(okResult.Value, "email"));
			var savedUser = dbContext.Users.AsNoTracking().Single(u => u.Id == userId);
			Assert.True(savedUser.SearchVisible);
			Assert.Equal("keep@example.com", savedUser.Email);
			Assert.True(savedUser.EmailConfirmed);
			_mockUserManager.Verify(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
			_mockUserManager.Verify(m => m.GenerateChangeEmailTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task UpdateUserInfo_NewEmail_ConfirmationEmailFails_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "prefs-user-3";
			var user = new ApplicationUser { Id = userId, UserName = "prefsuser3", Email = "old@example.com", EmailConfirmed = false };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GenerateChangeEmailTokenAsync(user, "new@example.com")).ReturnsAsync("change-token");

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			var mockUrl = SetupUrlHelper(controller);

			var result = await controller.UpdateUserInfo(new UserPreferencesViewModel { email = "new@example.com", searchVisible = true });

			Assert.IsType<BadRequestObjectResult>(result);
			_mockUserManager.Verify(m => m.GenerateChangeEmailTokenAsync(user, "new@example.com"), Times.Once);
			mockUrl.Verify(u => u.Action(It.Is<UrlActionContext>(c =>
				c.Action == "ConfirmEmailChange" && (string?)GetPropertyValue(c.Values, "userId") == userId)), Times.Once);
			Assert.Equal("old@example.com", dbContext.Users.AsNoTracking().Single(u => u.Id == userId).Email);
		}

		[Fact]
		public async Task UpdateUserInfo_ConfirmedUserNewEmail_KeepsCurrentEmailAndRequestsChangeConfirmation()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "prefs-user-4";
			var user = new ApplicationUser { Id = userId, UserName = "prefsuser4", Email = "old@example.com", EmailConfirmed = true, SearchVisible = true };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GenerateChangeEmailTokenAsync(user, "attacker@example.com")).ReturnsAsync("change-token");

			var controller = CreateController(dbContext, CreateUnsendableEmailService());
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			var mockUrl = SetupUrlHelper(controller);

			await controller.UpdateUserInfo(new UserPreferencesViewModel { email = "attacker@example.com", searchVisible = false });

			var savedUser = dbContext.Users.AsNoTracking().Single(u => u.Id == userId);
			Assert.Equal("old@example.com", savedUser.Email);
			Assert.True(savedUser.EmailConfirmed);
			Assert.False(savedUser.SearchVisible);
			_mockUserManager.Verify(m => m.GenerateChangeEmailTokenAsync(user, "attacker@example.com"), Times.Once);
			mockUrl.Verify(u => u.Action(It.Is<UrlActionContext>(c =>
				c.Action == "ConfirmEmailChange" && c.Controller == "Account" &&
				(string?)GetPropertyValue(c.Values, "userId") == userId &&
				(string?)GetPropertyValue(c.Values, "email") == "attacker@example.com" &&
				(string?)GetPropertyValue(c.Values, "code") == "change-token")), Times.Once);
			_mockUserManager.Verify(m => m.ChangeEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Theory]
		[InlineData(null, "new@example.com", "code")]
		[InlineData("user", null, "code")]
		[InlineData("user", "new@example.com", null)]
		public async Task ConfirmEmailChange_MissingParameters_RedirectsToError(string? userId, string? email, string? code)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateController(dbContext);

			var result = await controller.ConfirmEmailChange(userId!, email!, code!);

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/emailconfirm/error", redirectResult.Url);
			_mockUserManager.Verify(m => m.ChangeEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task ConfirmEmailChange_UserNotFound_RedirectsToError()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			_mockUserManager.Setup(m => m.FindByIdAsync("nonexistent")).ReturnsAsync((ApplicationUser?)null);

			var controller = CreateController(dbContext);

			var result = await controller.ConfirmEmailChange("nonexistent", "new@example.com", "code");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/emailconfirm/error", redirectResult.Url);
		}

		[Fact]
		public async Task ConfirmEmailChange_ValidToken_ChangesEmailAndRedirects()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "change-email-user-1";
			var user = new ApplicationUser { Id = userId, UserName = "changer", Email = "old@example.com" };

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.ChangeEmailAsync(user, "new@example.com", "change-token"))
				.ReturnsAsync(IdentityResult.Success);

			var controller = CreateController(dbContext);

			var result = await controller.ConfirmEmailChange(userId, "new@example.com", "change-token");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/emailconfirm", redirectResult.Url);
			_mockUserManager.Verify(m => m.ChangeEmailAsync(user, "new@example.com", "change-token"), Times.Once);
		}

		[Fact]
		public async Task ConfirmEmailChange_InvalidToken_RedirectsToError()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "change-email-user-2";
			var user = new ApplicationUser { Id = userId, UserName = "changer2", Email = "old@example.com" };

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.ChangeEmailAsync(user, "new@example.com", "bad-token"))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));

			var controller = CreateController(dbContext);

			var result = await controller.ConfirmEmailChange(userId, "new@example.com", "bad-token");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Equal(CORSConfig.returnOrigin + "/emailconfirm/error", redirectResult.Url);
		}

		[Fact]
		public async Task EarlyBird_AfterDeadline_ReturnsBadRequestAndDoesNotSignUp()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "earlybird-user-1";

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.EarlyBird(new EarlyBird { Email = "early@example.com" });

			var badRequest = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Equal("Early bird signup period has ended.", badRequest.Value);
			Assert.Empty(dbContext.EarlyBirds);
			_mockUserManager.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task SetPassword_Unauthenticated_Success_EmailsConfirmationLinkToNewUser()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			_mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), "Password123!")).ReturnsAsync(IdentityResult.Success);
			_mockUserManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("confirm-token");

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());
			SetupUrlHelper(controller);

			var result = await controller.SetPassword(new RegisterBindingModel { UserName = "newuser", Email = "new@example.com", Password = "Password123!" });

			Assert.IsType<OkResult>(result);
			_mockEmailService.Verify(e => e.SendEmailAsync(
				"new@example.com", It.IsAny<string>(), It.Is<string>(body => body.Contains("https://api.example.com/api/Account/Route"))), Times.Once);
		}

		[Fact]
		public async Task UpdateUserInfo_NewEmail_EmailsChangeConfirmationToNewAddressOnly()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "prefs-email-sent";
			var user = new ApplicationUser { Id = userId, UserName = "prefsemail", Email = "old@example.com", EmailConfirmed = true };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.GenerateChangeEmailTokenAsync(user, "new@example.com")).ReturnsAsync("change-token");

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			SetupUrlHelper(controller);

			var result = await controller.UpdateUserInfo(new UserPreferencesViewModel { email = "new@example.com", searchVisible = true });

			Assert.IsType<OkObjectResult>(result);
			_mockEmailService.Verify(e => e.SendEmailAsync(
				"new@example.com", It.IsAny<string>(), It.Is<string>(body => body.Contains("https://api.example.com/api/Account/Action"))), Times.Once);
			_mockEmailService.Verify(e => e.SendEmailAsync("old@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
			Assert.Equal("old@example.com", dbContext.Users.AsNoTracking().Single(u => u.Id == userId).Email);
		}
	}
}
