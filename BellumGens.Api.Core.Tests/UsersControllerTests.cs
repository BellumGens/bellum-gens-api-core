using System;
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
using SteamModels;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
	public class UsersControllerTests
	{
		private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
		private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
		private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
		private readonly Mock<IEmailService> _mockEmailService;
		private readonly Mock<ISteamService> _mockSteamService;
		private readonly Mock<IBattleNetService> _mockBattleNetService;
		private readonly Mock<INotificationService> _mockNotificationService;
		private readonly Mock<ILogger<UsersController>> _mockLogger;

		public UsersControllerTests()
		{
			_mockUserManager = TestUtils.CreateMockUserManager();
			_mockRoleManager = TestUtils.CreateMockRoleManager();
			_mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
			_mockEmailService = TestUtils.CreateMockEmailService();
			_mockSteamService = TestUtils.CreateMockSteamService();
			_mockBattleNetService = TestUtils.CreateMockBattleNetService();
			_mockNotificationService = TestUtils.CreateMockNotificationService();
			_mockLogger = TestUtils.CreateMockLogger<UsersController>();
		}

		private UsersController CreateController(BellumGensDbContext dbContext)
		{
			return new UsersController(
				_mockSteamService.Object, _mockBattleNetService.Object,
				_mockNotificationService.Object,
				_mockUserManager.Object, _mockRoleManager.Object,
				_mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
		}

		[Fact]
		public async Task GetAvailability_ReturnsUserAvailability()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "testuser" });
			dbContext.UserAvailabilities.Add(new UserAvailability
			{
				UserId = userId, Day = DayOfWeek.Monday, Available = true
			});
			dbContext.UserAvailabilities.Add(new UserAvailability
			{
				UserId = userId, Day = DayOfWeek.Wednesday, Available = true
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetAvailability(userId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var availability = Assert.IsAssignableFrom<List<UserAvailability>>(okResult.Value);
			Assert.Equal(2, availability.Count);
		}

		[Fact]
		public async Task GetMapPool_ReturnsUserMapPool()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "testuser" });
			dbContext.UserMapPool.Add(new UserMapPool
			{
				UserId = userId, Map = CSGOMap.Dust2, IsPlayed = true
			});
			dbContext.UserMapPool.Add(new UserMapPool
			{
				UserId = userId, Map = CSGOMap.Inferno, IsPlayed = true
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetMapPool(userId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var mapPool = Assert.IsAssignableFrom<List<UserMapPool>>(okResult.Value);
			Assert.Equal(2, mapPool.Count);
		}

		[Fact]
		public async Task GetUserTeams_ReturnsUserTeams()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var teamId = Guid.NewGuid();
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "testuser" });
			dbContext.CSGOTeams.Add(new CSGOTeam
			{
				TeamId = teamId, TeamName = "UserTeam", CustomUrl = "user-team", SteamGroupId = "sg1"
			});
			dbContext.TeamMembers.Add(new TeamMember
			{
				TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = false
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetUserTeams(userId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var teams = Assert.IsAssignableFrom<List<CSGOTeamSummaryViewModel>>(okResult.Value);
			Assert.Single(teams);
			Assert.Equal("UserTeam", teams[0].TeamName);
		}

		[Fact]
		public async Task Get_ReturnsUserStats_ForRegisteredUser()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "reguser1";
			var user = new ApplicationUser { Id = userId, UserName = "registereduser", SteamID = null, BattleNetId = null };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockSteamService.Setup(s => s.GetSteamUserDetails(It.IsAny<string>()))
				.ReturnsAsync(new UserStatsViewModel());

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.Get(userId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var userStats = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.NotNull(userStats);
		}

		[Fact]
		public async Task Get_UserNotFoundInDB_FallsBackToSteamService()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			var steamViewModel = new UserStatsViewModel { SteamUser = new SteamUser() };
			_mockSteamService.Setup(s => s.GetSteamUserDetails("unknownId"))
				.ReturnsAsync(steamViewModel);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.Get("unknownId");

			var okResult = Assert.IsType<OkObjectResult>(result);
			var userStats = Assert.IsType<UserStatsViewModel>(okResult.Value);
			Assert.NotNull(userStats);
			_mockSteamService.Verify(s => s.GetSteamUserDetails("unknownId"), Times.Once);
		}

		[Fact]
		public async Task GetUserGroups_ReturnsSteamUserGroups()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			var steamViewModel = new UserStatsViewModel { SteamUser = new SteamUser() };
			_mockSteamService.Setup(s => s.GetSteamUserDetails("someuser"))
				.ReturnsAsync(steamViewModel);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetUserGroups("someuser");

			var okResult = Assert.IsType<OkObjectResult>(result);
			_mockSteamService.Verify(s => s.GetSteamUserDetails("someuser"), Times.Once);
		}

		[Fact]
		public async Task SetAvailability_AddsNewAvailability()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var availability = new UserAvailability
			{
				UserId = userId, Day = DayOfWeek.Friday, Available = true
			};
			var result = await controller.SetAvailability(availability);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var saved = Assert.IsType<UserAvailability>(okResult.Value);
			Assert.Equal(DayOfWeek.Friday, saved.Day);
			Assert.True(saved.Available);
			Assert.Single(dbContext.UserAvailabilities.Where(a => a.UserId == userId));
		}

		[Fact]
		public async Task SetAvailability_RemovesAvailability_WhenAvailableFalse()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			dbContext.Users.Add(user);
			dbContext.UserAvailabilities.Add(new UserAvailability
			{
				UserId = userId, Day = DayOfWeek.Monday, Available = true
			});
			dbContext.SaveChanges();
			dbContext.ChangeTracker.Clear();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var toRemove = new UserAvailability
			{
				UserId = userId, Day = DayOfWeek.Monday, Available = false
			};
			var result = await controller.SetAvailability(toRemove);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Empty(dbContext.UserAvailabilities.Where(a => a.UserId == userId));
		}

		[Fact]
		public async Task SetAvailability_UpdatesExistingAvailability()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			dbContext.Users.Add(user);
			dbContext.UserAvailabilities.Add(new UserAvailability
			{
				UserId = userId, Day = DayOfWeek.Monday, Available = true,
				From = new DateTimeOffset(new DateTime(2018, 1, 15, 9, 0, 0, DateTimeKind.Utc))
			});
			dbContext.SaveChanges();
			dbContext.ChangeTracker.Clear();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var updated = new UserAvailability
			{
				UserId = userId, Day = DayOfWeek.Monday, Available = true,
				From = new DateTimeOffset(new DateTime(2018, 1, 15, 10, 0, 0, DateTimeKind.Utc))
			};
			var result = await controller.SetAvailability(updated);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Single(dbContext.UserAvailabilities.Where(a => a.UserId == userId));
		}

		[Fact]
		public async Task SetMapPool_AddsNewMapPool()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var mapPool = new UserMapPool { UserId = userId, Map = CSGOMap.Nuke, IsPlayed = true };
			var result = await controller.SetMapPool(mapPool);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var saved = Assert.IsType<UserMapPool>(okResult.Value);
			Assert.Equal(CSGOMap.Nuke, saved.Map);
			Assert.True(saved.IsPlayed);
			Assert.Single(dbContext.UserMapPool.Where(m => m.UserId == userId));
		}

		[Fact]
		public async Task SetMapPool_RemovesMap_WhenIsPlayedFalse()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			dbContext.Users.Add(user);
			dbContext.UserMapPool.Add(new UserMapPool
			{
				UserId = userId, Map = CSGOMap.Dust2, IsPlayed = true
			});
			dbContext.SaveChanges();
			dbContext.ChangeTracker.Clear();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var toRemove = new UserMapPool
			{
				UserId = userId, Map = CSGOMap.Dust2, IsPlayed = false
			};
			var result = await controller.SetMapPool(toRemove);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Empty(dbContext.UserMapPool.Where(m => m.UserId == userId));
		}

		private (UsersController controller, TeamInvite invite) SetupInvite(BellumGensDbContext dbContext, string currentUserId, string invitedUserId = "user1", bool alreadyMember = false)
		{
			var teamId = Guid.NewGuid();
			var currentUser = new ApplicationUser { Id = currentUserId, UserName = currentUserId };
			dbContext.Users.Add(currentUser);
			if (invitedUserId != currentUserId)
			{
				dbContext.Users.Add(new ApplicationUser { Id = invitedUserId, UserName = invitedUserId });
			}
			dbContext.Users.Add(new ApplicationUser { Id = "inviter1", UserName = "inviter" });
			dbContext.CSGOTeams.Add(new CSGOTeam
			{
				TeamId = teamId, TeamName = "Team1", CustomUrl = "team-1", SteamGroupId = "sg1"
			});
			dbContext.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = "inviter1", IsActive = true, IsAdmin = true });
			if (alreadyMember)
			{
				dbContext.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = invitedUserId, IsActive = true });
			}
			var invite = new TeamInvite
			{
				InvitingUserId = "inviter1",
				InvitedUserId = invitedUserId,
				TeamId = teamId
			};
			dbContext.TeamInvites.Add(invite);
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(currentUserId)).ReturnsAsync(currentUser);
			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(currentUserId));
			return (controller, invite);
		}

		private static TeamInvite InvitePayload(TeamInvite invite)
		{
			return new TeamInvite
			{
				InvitingUserId = invite.InvitingUserId,
				InvitedUserId = invite.InvitedUserId,
				TeamId = invite.TeamId
			};
		}

		[Fact]
		public async Task AcceptTeamInvite_InviteNotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var (controller, invite) = SetupInvite(dbContext, "user1");

			var payload = InvitePayload(invite);
			payload.TeamId = Guid.NewGuid();
			var result = await controller.AcceptTeamInvite(payload);

			Assert.IsType<NotFoundResult>(result);
			Assert.Single(dbContext.TeamMembers.Where(m => m.TeamId == invite.TeamId));
		}

		[Fact]
		public async Task AcceptTeamInvite_InviteNotForUser_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var (controller, invite) = SetupInvite(dbContext, "intruder", invitedUserId: "user1");

			var result = await controller.AcceptTeamInvite(InvitePayload(invite));

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.False(dbContext.TeamMembers.Any(m => m.TeamId == invite.TeamId && (m.UserId == "intruder" || m.UserId == "user1")));
			Assert.Equal(NotificationState.NotSeen, dbContext.TeamInvites.Single().State);
		}

		[Fact]
		public async Task AcceptTeamInvite_Success_AddsMemberAcceptsInviteAndNotifiesInviter()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var (controller, invite) = SetupInvite(dbContext, "user1");
			dbContext.BellumGensPushSubscriptions.Add(new BellumGensPushSubscription { UserId = "inviter1", Endpoint = "e", P256dh = "p", Auth = "a" });
			dbContext.SaveChanges();

			var result = await controller.AcceptTeamInvite(InvitePayload(invite));

			var okResult = Assert.IsType<OkObjectResult>(result);
			var entity = Assert.IsType<TeamInvite>(okResult.Value);
			Assert.Equal(invite.Id, entity.Id);
			Assert.Equal(NotificationState.Accepted, dbContext.TeamInvites.Single().State);
			var member = Assert.Single(dbContext.TeamMembers.Where(m => m.TeamId == invite.TeamId && m.UserId == "user1"));
			Assert.True(member.IsActive);
			Assert.False(member.IsAdmin);
			Assert.False(member.IsEditor);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.Is<List<BellumGensPushSubscription>>(s => s.Count == 1 && s[0].UserId == "inviter1"),
				It.Is<TeamInvite>(i => i.Id == invite.Id),
				NotificationState.Accepted), Times.Once);
		}

		[Fact]
		public async Task AcceptTeamInvite_AlreadyMember_DoesNotDuplicateMembership()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var (controller, invite) = SetupInvite(dbContext, "user1", alreadyMember: true);

			var result = await controller.AcceptTeamInvite(InvitePayload(invite));

			Assert.IsType<OkObjectResult>(result);
			Assert.Single(dbContext.TeamMembers.Where(m => m.TeamId == invite.TeamId && m.UserId == "user1"));
			Assert.Equal(NotificationState.Accepted, dbContext.TeamInvites.Single().State);
		}

		[Fact]
		public async Task RejectTeamInvite_InviteNotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var (controller, invite) = SetupInvite(dbContext, "user1");

			var payload = InvitePayload(invite);
			payload.InvitingUserId = "someoneelse";
			var result = await controller.RejectTeamInvite(payload);

			Assert.IsType<NotFoundResult>(result);
			Assert.Equal(NotificationState.NotSeen, dbContext.TeamInvites.Single().State);
		}

		[Fact]
		public async Task RejectTeamInvite_InviteNotForUser_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var (controller, invite) = SetupInvite(dbContext, "intruder", invitedUserId: "user1");

			var result = await controller.RejectTeamInvite(InvitePayload(invite));

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Equal(NotificationState.NotSeen, dbContext.TeamInvites.Single().State);
		}

		[Fact]
		public async Task RejectTeamInvite_Success_RejectsInvite()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var (controller, invite) = SetupInvite(dbContext, "user1");

			var result = await controller.RejectTeamInvite(InvitePayload(invite));

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(invite.Id, Assert.IsType<TeamInvite>(okResult.Value).Id);
			Assert.Equal(NotificationState.Rejected, dbContext.TeamInvites.Single().State);
			Assert.False(dbContext.TeamMembers.Any(m => m.TeamId == invite.TeamId && m.UserId == "user1"));
		}

		[Fact]
		public async Task GetTournaments_ReturnsEmptyList_WhenNoTournaments()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "testuser" });
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetTournaments(userId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var tournaments = Assert.IsAssignableFrom<List<PlayerTournamentViewModel>>(okResult.Value);
			Assert.Empty(tournaments);
		}

		private UsersController CreateControllerForCSGOUser(BellumGensDbContext dbContext, string userId, string steamId)
		{
			var user = new ApplicationUser
			{
				Id = userId, UserName = "csgoplayer", SteamID = steamId,
				CSGODetails = new CSGODetails
				{
					SteamId = steamId,
					PreferredPrimaryRole = PlaystyleRole.Support,
					PreferredSecondaryRole = PlaystyleRole.Lurker
				}
			};
			dbContext.Users.Add(user);
			dbContext.SaveChanges();

			return CreateControllerForUserFromDb(dbContext, userId);
		}

		/// <summary>
		/// Mirrors production: UserManager.FindByIdAsync returns the user entity without any
		/// navigation properties loaded (the app has no lazy loading).
		/// </summary>
		private UsersController CreateControllerForUserFromDb(BellumGensDbContext dbContext, string userId)
		{
			dbContext.ChangeTracker.Clear();
			var loaded = dbContext.Users.Single(u => u.Id == userId);
			Assert.Null(loaded.CSGODetails);

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(loaded);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			return controller;
		}

		private UsersController CreateControllerForBattleNetOnlyUser(BellumGensDbContext dbContext, string userId)
		{
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "sc2player", BattleNetId = "bnet-" + userId });
			dbContext.SaveChanges();

			return CreateControllerForUserFromDb(dbContext, userId);
		}

		[Fact]
		public async Task SetPrimaryRole_ReturnsBadRequest_WhenUserHasNoSteamAccount()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateControllerForBattleNetOnlyUser(dbContext, "user1");

			var result = await controller.SetPrimaryRole(PlaystyleRole.Awper, new Role { Id = PlaystyleRole.Awper, Name = "Awper" });

			var badRequest = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Contains("Steam", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
			Assert.Empty(dbContext.CSGODetails);
		}

		[Fact]
		public async Task SetSecondaryRole_ReturnsBadRequest_WhenUserHasNoSteamAccount()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateControllerForBattleNetOnlyUser(dbContext, "user1");

			var result = await controller.SetSecondaryRole(PlaystyleRole.IGL, new Role { Id = PlaystyleRole.IGL, Name = "IGL" });

			var badRequest = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Contains("Steam", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
			Assert.Empty(dbContext.CSGODetails);
		}

		[Fact]
		public async Task SetPrimaryRole_UpdatesPrimaryRole_AndReturnsRole()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateControllerForCSGOUser(dbContext, "user1", "76561198000000010");

			var role = new Role { Id = PlaystyleRole.Awper, Name = "Awper" };
			var result = await controller.SetPrimaryRole(PlaystyleRole.Awper, role);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Same(role, okResult.Value);

			dbContext.ChangeTracker.Clear();
			var details = await dbContext.CSGODetails.FindAsync(["76561198000000010"], TestContext.Current.CancellationToken);
			Assert.NotNull(details);
			Assert.Equal(PlaystyleRole.Awper, details.PreferredPrimaryRole);
			Assert.Equal(PlaystyleRole.Lurker, details.PreferredSecondaryRole);
		}

		[Fact]
		public async Task SetSecondaryRole_UpdatesSecondaryRole_AndReturnsRole()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateControllerForCSGOUser(dbContext, "user1", "76561198000000011");

			var role = new Role { Id = PlaystyleRole.IGL, Name = "IGL" };
			var result = await controller.SetSecondaryRole(PlaystyleRole.IGL, role);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Same(role, okResult.Value);

			dbContext.ChangeTracker.Clear();
			var details = await dbContext.CSGODetails.FindAsync(["76561198000000011"], TestContext.Current.CancellationToken);
			Assert.NotNull(details);
			Assert.Equal(PlaystyleRole.IGL, details.PreferredSecondaryRole);
			Assert.Equal(PlaystyleRole.Support, details.PreferredPrimaryRole);
		}

		[Fact]
		public async Task SetAvailability_IgnoresUserIdFromPayload_AndSavesForAuthenticatedUser()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var user = new ApplicationUser { Id = "user1", UserName = "testuser" };
			dbContext.Users.AddRange(user, new ApplicationUser { Id = "victim", UserName = "victim" });
			dbContext.UserAvailabilities.Add(new UserAvailability { UserId = "victim", Day = DayOfWeek.Monday, Available = true });
			dbContext.SaveChanges();
			dbContext.ChangeTracker.Clear();
			_mockUserManager.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

			await controller.SetAvailability(new UserAvailability { UserId = "victim", Day = DayOfWeek.Monday, Available = false });
			await controller.SetAvailability(new UserAvailability { UserId = "victim", Day = DayOfWeek.Friday, Available = true });

			Assert.Single(dbContext.UserAvailabilities.Where(a => a.UserId == "victim" && a.Day == DayOfWeek.Monday));
			Assert.False(dbContext.UserAvailabilities.Any(a => a.UserId == "victim" && a.Day == DayOfWeek.Friday));
			Assert.Single(dbContext.UserAvailabilities.Where(a => a.UserId == "user1" && a.Day == DayOfWeek.Friday));
		}

		[Fact]
		public async Task SetMapPool_IgnoresUserIdFromPayload_AndSavesForAuthenticatedUser()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var user = new ApplicationUser { Id = "user1", UserName = "testuser" };
			dbContext.Users.AddRange(user, new ApplicationUser { Id = "victim", UserName = "victim" });
			dbContext.UserMapPool.Add(new UserMapPool { UserId = "victim", Map = CSGOMap.Dust2, IsPlayed = true });
			dbContext.SaveChanges();
			dbContext.ChangeTracker.Clear();
			_mockUserManager.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

			await controller.SetMapPool(new UserMapPool { UserId = "victim", Map = CSGOMap.Dust2, IsPlayed = false });
			await controller.SetMapPool(new UserMapPool { UserId = "victim", Map = CSGOMap.Nuke, IsPlayed = true });

			Assert.Single(dbContext.UserMapPool.Where(m => m.UserId == "victim"));
			Assert.Single(dbContext.UserMapPool.Where(m => m.UserId == "user1" && m.Map == CSGOMap.Nuke));
		}

		[Fact]
		public async Task Get_UnregisteredUser_ReturnsNotFound_WhenSteamLookupFails()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			_mockSteamService.Setup(s => s.GetSteamUserDetails("missing")).ReturnsAsync((UserStatsViewModel)null!);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.Get("missing");

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task Get_RegisteredSteamUser_ReturnsOk_WhenSteamLookupFails()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			dbContext.Users.Add(new ApplicationUser { Id = "steamreg", UserName = "steamreg", SteamID = "76561198000000099" });
			dbContext.SaveChanges();
			_mockSteamService.Setup(s => s.GetSteamUserDetails("76561198000000099")).ReturnsAsync((UserStatsViewModel)null!);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.Get("steamreg");

			Assert.IsType<UserStatsViewModel>(Assert.IsType<OkObjectResult>(result).Value);
		}

		[Fact]
		public async Task AcceptTeamInvite_NotifiesInviter_WithTeamAndInvitedUserLoaded()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var (controller, invite) = SetupInvite(dbContext, "user1");
			dbContext.ChangeTracker.Clear();
			var user = dbContext.Users.Single(u => u.Id == "user1");
			_mockUserManager.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);

			var result = await controller.AcceptTeamInvite(InvitePayload(invite));

			Assert.IsType<OkObjectResult>(result);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.IsAny<List<BellumGensPushSubscription>>(),
				It.Is<TeamInvite>(i => i.Team != null && i.Team.TeamName == "Team1" && i.InvitedUser != null && i.InvitedUser.Id == "user1"),
				NotificationState.Accepted), Times.Once);
		}
	}
}
