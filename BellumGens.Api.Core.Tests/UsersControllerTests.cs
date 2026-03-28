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
        private readonly EmailServiceProvider _emailService;
        private readonly Mock<ISteamService> _mockSteamService;
        private readonly Mock<IBattleNetService> _mockBattleNetService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<UsersController>> _mockLogger;

        public UsersControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _emailService = TestUtils.CreateMockEmailServiceProvider();
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
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
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

        [Fact]
        public async Task AcceptTeamInvite_InviteNotFound_ReturnsNotFound()
        {
            // TeamInvite has a single key (Id) but the controller calls
            // FindAsync(InvitingUserId, InvitedUserId, TeamId) with 3 values.
            // This is a pre-existing bug that causes an ArgumentException.
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var invite = new TeamInvite
            {
                InvitingUserId = "inviter1",
                InvitedUserId = userId,
                TeamId = Guid.NewGuid()
            };

            await Assert.ThrowsAsync<ArgumentException>(() => controller.AcceptTeamInvite(invite));
        }

        [Fact]
        public async Task AcceptTeamInvite_InviteNotForUser_ThrowsDueToKeyMismatch()
        {
            // TeamInvite entity uses single key (Id) but controller passes 3 values to FindAsync.
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var invite = new TeamInvite
            {
                InvitingUserId = "inviter1",
                InvitedUserId = "other1",
                TeamId = Guid.NewGuid()
            };

            await Assert.ThrowsAsync<ArgumentException>(() => controller.AcceptTeamInvite(invite));
        }

        [Fact]
        public async Task AcceptTeamInvite_Success_ThrowsDueToKeyMismatch()
        {
            // TeamInvite entity uses single key (Id) but controller passes 3 values to FindAsync.
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var invitingUserId = "inviter1";
            var teamId = Guid.NewGuid();
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var inviter = new ApplicationUser { Id = invitingUserId, UserName = "inviter" };
            dbContext.Users.AddRange(user, inviter);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team1", CustomUrl = "team-1", SteamGroupId = "sg1"
            });
            var inviteEntity = new TeamInvite
            {
                InvitingUserId = invitingUserId,
                InvitedUserId = userId,
                TeamId = teamId
            };
            dbContext.TeamInvites.Add(inviteEntity);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var invite = new TeamInvite
            {
                Id = inviteEntity.Id,
                InvitingUserId = invitingUserId,
                InvitedUserId = userId,
                TeamId = teamId
            };

            await Assert.ThrowsAsync<ArgumentException>(() => controller.AcceptTeamInvite(invite));
        }

        [Fact]
        public async Task RejectTeamInvite_InviteNotFound_ReturnsNotFound()
        {
            // TeamInvite has single key (Id) but controller passes 3 values to FindAsync.
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var invite = new TeamInvite
            {
                InvitingUserId = "inviter1",
                InvitedUserId = userId,
                TeamId = Guid.NewGuid()
            };

            await Assert.ThrowsAsync<ArgumentException>(() => controller.RejectTeamInvite(invite));
        }

        [Fact]
        public async Task RejectTeamInvite_Success_ThrowsDueToKeyMismatch()
        {
            // TeamInvite has single key (Id) but controller passes 3 values to FindAsync.
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var invitingUserId = "inviter1";
            var teamId = Guid.NewGuid();
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var inviter = new ApplicationUser { Id = invitingUserId, UserName = "inviter" };
            dbContext.Users.AddRange(user, inviter);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team1", CustomUrl = "team-1", SteamGroupId = "sg1"
            });
            var inviteEntity = new TeamInvite
            {
                InvitingUserId = invitingUserId,
                InvitedUserId = userId,
                TeamId = teamId
            };
            dbContext.TeamInvites.Add(inviteEntity);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var invite = new TeamInvite
            {
                Id = inviteEntity.Id,
                InvitingUserId = invitingUserId,
                InvitedUserId = userId,
                TeamId = teamId
            };

            await Assert.ThrowsAsync<ArgumentException>(() => controller.RejectTeamInvite(invite));
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
    }
}
