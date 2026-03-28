using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    public class TeamsControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly EmailServiceProvider _emailService;
        private readonly Mock<ISteamService> _mockSteamService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<TeamsController>> _mockLogger;

        public TeamsControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _emailService = TestUtils.CreateMockEmailServiceProvider();
            _mockSteamService = TestUtils.CreateMockSteamService();
            _mockNotificationService = TestUtils.CreateMockNotificationService();
            _mockLogger = TestUtils.CreateMockLogger<TeamsController>();
        }

        private TeamsController CreateController(BellumGensDbContext dbContext)
        {
            return new TeamsController(
                _mockSteamService.Object, _mockNotificationService.Object,
                _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
        }

        [Fact]
        public async Task Get_ReturnsTeam_ByCustomUrl()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamName = "TestTeam", CustomUrl = "test-team", Visible = true, SteamGroupId = "sg1"
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);

            var result = await controller.Get("test-team");

            Assert.NotNull(result);
            Assert.Equal("TestTeam", result.TeamName);
        }

        [Fact]
        public async Task Get_ReturnsNull_WhenTeamNotFound()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);

            var result = await controller.Get("nonexistent");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetTeamMembers_ReturnsMembers_ForTeam()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var user = new ApplicationUser { Id = "user1", UserName = "Player1" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "TestTeam", CustomUrl = "test-team", SteamGroupId = "sg1"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = "user1", IsActive = true, IsAdmin = true
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);

            var result = await controller.GetTeamMembers(teamId);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("user1", result[0].UserId);
        }

        [Fact]
        public async Task GetTeamAvailability_ReturnsAvailability_ForTeam()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "TestTeam", CustomUrl = "test-avail", SteamGroupId = "sg1"
            });
            dbContext.TeamAvailabilities.Add(new TeamAvailability
            {
                TeamId = teamId, Day = DayOfWeek.Monday, Available = true
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.GetTeamAvailability(teamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var availability = Assert.IsAssignableFrom<List<TeamAvailability>>(okResult.Value);
            Assert.Single(availability);
            Assert.Equal(DayOfWeek.Monday, availability[0].Day);
        }

        [Fact]
        public async Task NewTeam_CreatesTeam_WithAuthenticatedUserAsAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "teamcreator" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var team = new CSGOTeam { TeamName = "NewTeam", Visible = true };
            var result = await controller.NewTeam(team);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var createdTeam = Assert.IsType<CSGOTeam>(okResult.Value);
            Assert.Equal("NewTeam", createdTeam.TeamName);
            Assert.Single(createdTeam.Members);
        }

        [Fact]
        public async Task GetTournaments_ReturnsEmptyList()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "NoTourneyTeam", CustomUrl = "no-tourney", SteamGroupId = "sg1"
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.GetTournaments(teamId.ToString());

            var okResult = Assert.IsType<OkObjectResult>(result);
            var tournaments = Assert.IsAssignableFrom<List<TeamTournamentViewModel>>(okResult.Value);
            Assert.Empty(tournaments);
        }

        [Fact]
        public async Task GetTeamMapPool_ReturnsBadRequest_WhenNotMember()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "nonmember" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "MapPoolTeam", CustomUrl = "mappool-team", SteamGroupId = "sg1"
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetTeamMapPool(teamId);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTeamMapPool_ReturnsMapPool_WhenMember()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "member" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "MapPoolTeam", CustomUrl = "mappool-team2", SteamGroupId = "sg2"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = false
            });
            dbContext.TeamMapPools.Add(new TeamMapPool { TeamId = teamId, Map = CSGOMap.Dust2, IsPlayed = true });
            dbContext.TeamMapPools.Add(new TeamMapPool { TeamId = teamId, Map = CSGOMap.Inferno, IsPlayed = true });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetTeamMapPool(teamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var mapPool = Assert.IsAssignableFrom<List<TeamMapPool>>(okResult.Value);
            Assert.Equal(2, mapPool.Count);
        }

        [Fact]
        public async Task GetTeamAvailability_ReturnsEmptyList_WhenNoAvailability()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "NoAvailTeam", CustomUrl = "no-avail", SteamGroupId = "sg3"
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.GetTeamAvailability(teamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var availability = Assert.IsAssignableFrom<List<TeamAvailability>>(okResult.Value);
            Assert.Empty(availability);
        }

        [Fact]
        public async Task NewTeam_GeneratesUniqueCustomUrl()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "teamcreator" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var team = new CSGOTeam { TeamName = "Unique URL Team", Visible = true };
            var result = await controller.NewTeam(team);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var createdTeam = Assert.IsType<CSGOTeam>(okResult.Value);
            Assert.False(string.IsNullOrEmpty(createdTeam.CustomUrl));
        }

        [Fact]
        public async Task GetIsTeamAdmin_ReturnsTrue_WhenAdminWithGuid()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "AdminTeam", CustomUrl = "admin-team", SteamGroupId = "sg1"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetIsTeamAdmin(teamId.ToString());

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(true, okResult.Value);
        }

        [Fact]
        public async Task GetIsTeamAdmin_ReturnsTrue_WhenAdminWithCustomUrl()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "AdminTeam2", CustomUrl = "admin-team-url", SteamGroupId = "sg2"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetIsTeamAdmin("admin-team-url");

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(true, okResult.Value);
        }

        [Fact]
        public async Task GetIsTeamMember_ReturnsTrue_WhenMember()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "member" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "MemberTeam", CustomUrl = "member-team", SteamGroupId = "sg3"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = false
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetIsTeamMember(teamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(true, okResult.Value);
        }

        [Fact]
        public async Task GetIsTeamEditor_ReturnsTrue_WhenEditor()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "editor" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "EditorTeam", CustomUrl = "editor-team", SteamGroupId = "sg4"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = false, IsEditor = true
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetIsTeamEditor(teamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(true, okResult.Value);
        }

        [Fact]
        public async Task UpdateTeam_ReturnsOk_WhenAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            var existingTeam = new CSGOTeam
            {
                TeamId = teamId, TeamName = "OldName", CustomUrl = "update-team", SteamGroupId = "sg5", Visible = true
            };
            dbContext.CSGOTeams.Add(existingTeam);
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            });
            dbContext.SaveChanges();
            dbContext.Entry(existingTeam).State = EntityState.Detached;

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var updatedTeam = new CSGOTeam
            {
                TeamId = teamId, TeamName = "NewName", CustomUrl = "update-team", SteamGroupId = "sg5", Visible = true
            };
            var result = await controller.UpdateTeam(updatedTeam);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var team = Assert.IsType<CSGOTeam>(okResult.Value);
            Assert.Equal("NewName", team.TeamName);
        }

        [Fact]
        public async Task UpdateTeam_ReturnsBadRequest_WhenNotAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "nonadmin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team", CustomUrl = "noadmin-team", SteamGroupId = "sg6"
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var updatedTeam = new CSGOTeam
            {
                TeamId = teamId, TeamName = "Updated", CustomUrl = "noadmin-team", SteamGroupId = "sg6"
            };
            var result = await controller.UpdateTeam(updatedTeam);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateTeamMember_ReturnsOk_WhenAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var adminId = "admin1";
            var memberId = "member1";
            var admin = new ApplicationUser { Id = adminId, UserName = "admin" };
            var member = new ApplicationUser { Id = memberId, UserName = "member" };
            dbContext.Users.AddRange(admin, member);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team", CustomUrl = "update-member", SteamGroupId = "sg7"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = adminId, IsActive = true, IsAdmin = true
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = memberId, IsActive = true, IsAdmin = false, IsEditor = false
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(adminId)).ReturnsAsync(admin);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(adminId));

            var updatedMember = new TeamMember
            {
                TeamId = teamId, UserId = memberId, IsActive = true, IsAdmin = false, IsEditor = true
            };
            var result = await controller.UpdateTeamMember(updatedMember);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task UpdateTeamMember_ReturnsBadRequest_WhenNotAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "nonadmin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team", CustomUrl = "no-update-member", SteamGroupId = "sg8"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = false
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var updatedMember = new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            };
            var result = await controller.UpdateTeamMember(updatedMember);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RemoveTeamMember_ReturnsOk_WhenAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var adminId = "admin1";
            var memberId = "member1";
            var admin = new ApplicationUser { Id = adminId, UserName = "admin" };
            var member = new ApplicationUser { Id = memberId, UserName = "member" };
            dbContext.Users.AddRange(admin, member);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team", CustomUrl = "remove-member", SteamGroupId = "sg9"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = adminId, IsActive = true, IsAdmin = true
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = memberId, IsActive = true, IsAdmin = false
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(adminId)).ReturnsAsync(admin);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(adminId));

            var result = await controller.RemoveTeamMember(teamId, memberId);

            Assert.IsType<OkResult>(result);
            Assert.Null(await dbContext.TeamMembers.FindAsync(teamId, memberId));
        }

        [Fact]
        public async Task RemoveTeamMember_ReturnsBadRequest_WhenNotAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "nonadmin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team", CustomUrl = "no-remove", SteamGroupId = "sg10"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = false
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.RemoveTeamMember(teamId, "other-user");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task AbandonTeam_RemovesTeam_WhenSoleMember()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "sole" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "SoleTeam", CustomUrl = "sole-team", SteamGroupId = "sg11"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.AbandonTeam(teamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Null(await dbContext.CSGOTeams.FindAsync(teamId));
        }

        [Fact]
        public async Task AbandonTeam_RemovesSelf_PromotesNewAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var otherId = "user2";
            var user = new ApplicationUser { Id = userId, UserName = "leaving" };
            var other = new ApplicationUser { Id = otherId, UserName = "staying" };
            dbContext.Users.AddRange(user, other);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "MultiTeam", CustomUrl = "multi-team", SteamGroupId = "sg12"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = otherId, IsActive = true, IsAdmin = false
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.AbandonTeam(teamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(await dbContext.CSGOTeams.FindAsync(teamId));
            Assert.Null(await dbContext.TeamMembers.FindAsync(teamId, userId));
            var remaining = await dbContext.TeamMembers.FindAsync(teamId, otherId);
            Assert.NotNull(remaining);
            Assert.True(remaining.IsAdmin);
        }

        [Fact]
        public async Task SetTeamAvailability_AddsAvailability_WhenAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "AvailTeam", CustomUrl = "avail-team", SteamGroupId = "sg13"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var day = new TeamAvailability
            {
                TeamId = teamId, Day = DayOfWeek.Monday, Available = true
            };
            var result = await controller.SetTeamAvailability(day);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var saved = Assert.IsType<TeamAvailability>(okResult.Value);
            Assert.True(saved.Available);
        }

        [Fact]
        public async Task SetTeamAvailability_RemovesAvailability_WhenAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "AvailTeam2", CustomUrl = "avail-team2", SteamGroupId = "sg14"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            });
            var existing = new TeamAvailability
            {
                TeamId = teamId, Day = DayOfWeek.Tuesday, Available = true
            };
            dbContext.TeamAvailabilities.Add(existing);
            dbContext.SaveChanges();
            dbContext.Entry(existing).State = EntityState.Detached;

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var day = new TeamAvailability
            {
                TeamId = teamId, Day = DayOfWeek.Tuesday, Available = false
            };
            var result = await controller.SetTeamAvailability(day);

            var okResult = Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task SetTeamMapPool_ReturnsOk_WhenAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "admin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "MapTeam", CustomUrl = "map-team", SteamGroupId = "sg15"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = true
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var maps = new List<TeamMapPool>
            {
                new TeamMapPool { TeamId = teamId, Map = CSGOMap.Dust2, IsPlayed = true },
                new TeamMapPool { TeamId = teamId, Map = CSGOMap.Inferno, IsPlayed = true },
                new TeamMapPool { TeamId = teamId, Map = CSGOMap.Mirage, IsPlayed = false }
            };
            var result = await controller.SetTeamMapPool(maps);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var pool = await dbContext.TeamMapPools.Where(m => m.TeamId == teamId).ToListAsync();
            Assert.Equal(2, pool.Count);
        }

        [Fact]
        public async Task SetTeamMapPool_ReturnsBadRequest_WhenNotAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var teamId = Guid.NewGuid();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "nonadmin" };
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "MapTeam2", CustomUrl = "map-team2", SteamGroupId = "sg16"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = false
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var maps = new List<TeamMapPool>
            {
                new TeamMapPool { TeamId = teamId, Map = CSGOMap.Dust2, IsPlayed = true }
            };
            var result = await controller.SetTeamMapPool(maps);

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
