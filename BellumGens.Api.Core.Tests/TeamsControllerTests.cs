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
using SteamModels;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
	public class TeamsControllerTests
	{
		private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
		private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
		private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
		private readonly Mock<IEmailService> _mockEmailService;
		private readonly Mock<ISteamService> _mockSteamService;
		private readonly Mock<INotificationService> _mockNotificationService;
		private readonly Mock<ILogger<TeamsController>> _mockLogger;

		public TeamsControllerTests()
		{
			_mockUserManager = TestUtils.CreateMockUserManager();
			_mockRoleManager = TestUtils.CreateMockRoleManager();
			_mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
			_mockEmailService = TestUtils.CreateMockEmailService();
			_mockSteamService = TestUtils.CreateMockSteamService();
			_mockNotificationService = TestUtils.CreateMockNotificationService();
			_mockLogger = TestUtils.CreateMockLogger<TeamsController>();
		}

		private TeamsController CreateController(BellumGensDbContext dbContext)
		{
			return new TeamsController(
				_mockSteamService.Object, _mockNotificationService.Object,
				_mockUserManager.Object, _mockRoleManager.Object,
				_mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
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

		// ---- Team membership flow helpers ----

		private const string AdminId = "admin1";
		private const string MemberId = "member1";
		private const string OutsiderId = "outsider1";
		private const string ApplicantId = "applicant1";

		private static Guid SeedTeamWithRoles(BellumGensDbContext dbContext, string customUrl, string steamGroupId)
		{
			var teamId = Guid.NewGuid();
			dbContext.Users.AddRange(
				new ApplicationUser { Id = AdminId, UserName = "admin" },
				new ApplicationUser { Id = MemberId, UserName = "member" },
				new ApplicationUser { Id = OutsiderId, UserName = "outsider" },
				new ApplicationUser { Id = ApplicantId, UserName = "applicant" });
			dbContext.CSGOTeams.Add(new CSGOTeam
			{
				TeamId = teamId, TeamName = "RolesTeam", CustomUrl = customUrl, SteamGroupId = steamGroupId
			});
			dbContext.TeamMembers.Add(new TeamMember
			{
				TeamId = teamId, UserId = AdminId, IsActive = true, IsAdmin = true, IsEditor = true
			});
			dbContext.TeamMembers.Add(new TeamMember
			{
				TeamId = teamId, UserId = MemberId, IsActive = true, IsAdmin = false, IsEditor = false
			});
			dbContext.SaveChanges();
			return teamId;
		}

		private TeamsController CreateControllerAs(BellumGensDbContext dbContext, string userId)
		{
			var user = dbContext.Users.Find(userId)!;
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			return controller;
		}

		private void VerifyNoApplicationNotificationSent()
		{
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.IsAny<List<BellumGensPushSubscription>>(), It.IsAny<TeamApplication>()), Times.Never);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.IsAny<List<BellumGensPushSubscription>>(), It.IsAny<TeamApplication>(), It.IsAny<NotificationState>()), Times.Never);
		}

		// ---- GetSteamGroupMembers ----

		[Fact]
		public async Task GetSteamGroupMembers_ReturnsSummariesFromSteamService()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var summaries = new List<SteamUserSummary> { new SteamUserSummary(), new SteamUserSummary() };
			_mockSteamService.Setup(s => s.GetSteamUsersSummary("steam1,steam2")).ReturnsAsync(summaries);

			var controller = CreateController(dbContext);

			var result = await controller.GetSteamGroupMembers("steam1,steam2");

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Same(summaries, okResult.Value);
			_mockSteamService.Verify(s => s.GetSteamUsersSummary("steam1,steam2"), Times.Once);
		}

		// ---- TeamFromSteamGroup ----

		[Fact]
		public async Task TeamFromSteamGroup_CreatesTeamWithUserAsAdmin_WhenSteamGroupAdmin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "groupowner", SteamID = "76561198000000001" });
			dbContext.SaveChanges();

			_mockSteamService.Setup(s => s.VerifyUserIsGroupAdmin("76561198000000001", "103582791400000001"))
				.ReturnsAsync(true);

			var controller = CreateControllerAs(dbContext, userId);

			var group = new SteamUserGroup
			{
				groupID64 = "103582791400000001", groupName = "Steam Group Team", avatarFull = "https://avatar/full.jpg"
			};
			var result = await controller.TeamFromSteamGroup(group);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var createdTeam = Assert.IsType<CSGOTeam>(okResult.Value);
			Assert.Equal("Steam Group Team", createdTeam.TeamName);
			Assert.Equal("103582791400000001", createdTeam.SteamGroupId);
			Assert.Equal("https://avatar/full.jpg", createdTeam.TeamAvatar);
			Assert.Equal("Steam-Group-Team", createdTeam.CustomUrl);

			var savedTeam = await dbContext.CSGOTeams.SingleAsync(t => t.SteamGroupId == "103582791400000001", TestContext.Current.CancellationToken);
			var membership = await dbContext.TeamMembers.FindAsync([savedTeam.TeamId, userId], TestContext.Current.CancellationToken);
			Assert.NotNull(membership);
			Assert.True(membership.IsAdmin);
			Assert.True(membership.IsEditor);
			Assert.True(membership.IsActive);
			_mockSteamService.Verify(s => s.VerifyUserIsGroupAdmin("76561198000000001", "103582791400000001"), Times.Once);
		}

		[Fact]
		public async Task TeamFromSteamGroup_ReturnsBadRequest_WhenNotSteamGroupAdmin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "notowner", SteamID = "76561198000000002" });
			dbContext.SaveChanges();

			_mockSteamService.Setup(s => s.VerifyUserIsGroupAdmin(It.IsAny<string>(), It.IsAny<string>()))
				.ReturnsAsync(false);

			var controller = CreateControllerAs(dbContext, userId);

			var group = new SteamUserGroup { groupID64 = "103582791400000002", groupName = "Not My Group" };
			var result = await controller.TeamFromSteamGroup(group);

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.CSGOTeams);
			Assert.Empty(dbContext.TeamMembers);
			_mockSteamService.Verify(s => s.VerifyUserIsGroupAdmin("76561198000000002", "103582791400000002"), Times.Once);
		}

		// ---- GetTeamApplications ----

		[Fact]
		public async Task GetTeamApplications_ReturnsTeamApplicationsNewestFirst_WhenAdmin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "apps-team", "sg-apps");
			var otherTeamId = Guid.NewGuid();
			dbContext.CSGOTeams.Add(new CSGOTeam
			{
				TeamId = otherTeamId, TeamName = "OtherTeam", CustomUrl = "other-apps-team", SteamGroupId = "sg-other"
			});
			dbContext.TeamApplications.AddRange(
				new TeamApplication
				{
					ApplicantId = ApplicantId, TeamId = teamId, Message = "older",
					Sent = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
				},
				new TeamApplication
				{
					ApplicantId = OutsiderId, TeamId = teamId, Message = "newer",
					Sent = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)
				},
				new TeamApplication
				{
					ApplicantId = ApplicantId, TeamId = otherTeamId, Message = "other team"
				});
			dbContext.SaveChanges();

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.GetTeamApplications(teamId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var applications = Assert.IsAssignableFrom<List<TeamApplication>>(okResult.Value);
			Assert.Equal(2, applications.Count);
			Assert.All(applications, a => Assert.Equal(teamId, a.TeamId));
			Assert.Equal("newer", applications[0].Message);
			Assert.Equal("outsider", applications[0].UserName);
			Assert.Equal("older", applications[1].Message);
			Assert.Equal("applicant", applications[1].UserName);
		}

		[Theory]
		[InlineData(MemberId)]
		[InlineData(OutsiderId)]
		public async Task GetTeamApplications_ReturnsBadRequest_WhenNotAdmin(string userId)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "apps-team-noadmin", "sg-apps-noadmin");
			dbContext.TeamApplications.Add(new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId });
			dbContext.SaveChanges();

			var controller = CreateControllerAs(dbContext, userId);

			var result = await controller.GetTeamApplications(teamId);

			var badRequest = Assert.IsType<BadRequestObjectResult>(result);
			Assert.IsType<string>(badRequest.Value);
		}

		// ---- ApplyToTeam ----

		[Fact]
		public async Task ApplyToTeam_ReturnsBadRequest_WhenModelStateInvalid()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "apply-invalid", "sg-apply-invalid");

			var controller = CreateControllerAs(dbContext, ApplicantId);
			controller.ModelState.AddModelError("Message", "Invalid");

			var application = new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, Message = "let me in" };
			var result = await controller.ApplyToTeam(application);

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TeamApplications);
			VerifyNoApplicationNotificationSent();
		}

		// ---- ApproveApplication ----

		[Theory]
		[InlineData(MemberId)]
		[InlineData(OutsiderId)]
		public async Task ApproveApplication_ReturnsBadRequest_AndDoesNotAddMember_WhenNotAdmin(string userId)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "approve-noadmin", "sg-approve-noadmin");
			var pending = new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, State = NotificationState.NotSeen };
			dbContext.TeamApplications.Add(pending);
			dbContext.SaveChanges();

			var controller = CreateControllerAs(dbContext, userId);

			var result = await controller.ApproveApplication(
				new TeamApplication { Id = pending.Id, ApplicantId = ApplicantId, TeamId = teamId });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Null(await dbContext.TeamMembers.FindAsync([teamId, ApplicantId], TestContext.Current.CancellationToken));
			var stored = await dbContext.TeamApplications.FindAsync([pending.Id], TestContext.Current.CancellationToken);
			Assert.NotNull(stored);
			Assert.Equal(NotificationState.NotSeen, stored.State);
			VerifyNoApplicationNotificationSent();
		}

		// ---- RejectApplication ----

		[Theory]
		[InlineData(MemberId)]
		[InlineData(OutsiderId)]
		public async Task RejectApplication_ReturnsBadRequest_AndLeavesApplicationPending_WhenNotAdmin(string userId)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "reject-noadmin", "sg-reject-noadmin");
			var pending = new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, State = NotificationState.NotSeen };
			dbContext.TeamApplications.Add(pending);
			dbContext.SaveChanges();

			var controller = CreateControllerAs(dbContext, userId);

			var result = await controller.RejectApplication(
				new TeamApplication { Id = pending.Id, ApplicantId = ApplicantId, TeamId = teamId });

			Assert.IsType<BadRequestObjectResult>(result);
			var stored = await dbContext.TeamApplications.FindAsync([pending.Id], TestContext.Current.CancellationToken);
			Assert.NotNull(stored);
			Assert.Equal(NotificationState.NotSeen, stored.State);
		}

		// ---- InviteToTeam ----

		[Theory]
		[InlineData(MemberId)]
		[InlineData(OutsiderId)]
		public async Task InviteToTeam_ReturnsBadRequest_AndSendsNoInvite_WhenNotAdmin(string userId)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "invite-noadmin", "sg-invite-noadmin");

			var controller = CreateControllerAs(dbContext, userId);

			var result = await controller.InviteToTeam(new InviteModel { TeamId = teamId, UserId = ApplicantId });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TeamInvites);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.IsAny<List<BellumGensPushSubscription>>(), It.IsAny<TeamInvite>()), Times.Never);
		}

		private static void AddPushSubscription(BellumGensDbContext dbContext, string userId)
		{
			dbContext.BellumGensPushSubscriptions.Add(new BellumGensPushSubscription
			{
				UserId = userId, Endpoint = "https://push/" + userId, P256dh = "p-" + userId, Auth = "a-" + userId
			});
			dbContext.SaveChanges();
		}

		[Fact]
		public async Task ApplyToTeam_CreatesApplication_AndNotifiesTeamAdmin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "apply-ok", "sg-apply-ok");
			AddPushSubscription(dbContext, AdminId);

			var controller = CreateControllerAs(dbContext, ApplicantId);

			var result = await controller.ApplyToTeam(new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, Message = "let me in" });

			Assert.IsType<OkObjectResult>(result);
			var stored = Assert.Single(dbContext.TeamApplications);
			Assert.Equal(ApplicantId, stored.ApplicantId);
			Assert.Equal(teamId, stored.TeamId);
			Assert.Equal("let me in", stored.Message);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.Is<List<BellumGensPushSubscription>>(s => s.Count == 1 && s[0].UserId == AdminId),
				It.Is<TeamApplication>(a => a.ApplicantId == ApplicantId)), Times.Once);
		}

		[Fact]
		public async Task ApplyToTeam_UsesAuthenticatedUser_WhenApplicantIdBelongsToSomeoneElse()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "apply-spoof", "sg-apply-spoof");

			var controller = CreateControllerAs(dbContext, ApplicantId);

			var result = await controller.ApplyToTeam(new TeamApplication { ApplicantId = OutsiderId, TeamId = teamId, Message = "spoof" });

			Assert.IsType<OkObjectResult>(result);
			var stored = Assert.Single(dbContext.TeamApplications);
			Assert.Equal(ApplicantId, stored.ApplicantId);
		}

		[Fact]
		public async Task ApplyToTeam_UpdatesExistingApplication_InsteadOfDuplicating()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "apply-again", "sg-apply-again");
			var existing = new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, Message = "first", State = NotificationState.Rejected };
			dbContext.TeamApplications.Add(existing);
			dbContext.SaveChanges();

			var controller = CreateControllerAs(dbContext, ApplicantId);

			var result = await controller.ApplyToTeam(new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, Message = "second" });

			Assert.IsType<OkObjectResult>(result);
			var stored = Assert.Single(dbContext.TeamApplications);
			Assert.Equal(existing.Id, stored.Id);
			Assert.Equal("second", stored.Message);
			Assert.Equal(NotificationState.NotSeen, stored.State);
		}

		[Fact]
		public async Task ApplyToTeam_ReturnsBadRequest_WhenAlreadyMember()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "apply-member", "sg-apply-member");

			var controller = CreateControllerAs(dbContext, MemberId);

			var result = await controller.ApplyToTeam(new TeamApplication { ApplicantId = MemberId, TeamId = teamId, Message = "again" });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TeamApplications);
			VerifyNoApplicationNotificationSent();
		}

		[Fact]
		public async Task ApproveApplication_AddsMember_AcceptsApplication_AndNotifiesApplicant_WhenAdmin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "approve-ok", "sg-approve-ok");
			var pending = new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, State = NotificationState.NotSeen };
			dbContext.TeamApplications.Add(pending);
			dbContext.SaveChanges();
			AddPushSubscription(dbContext, ApplicantId);

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.ApproveApplication(
				new TeamApplication { Id = pending.Id, ApplicantId = ApplicantId, TeamId = teamId });

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(pending.Id, Assert.IsType<TeamApplication>(okResult.Value).Id);
			var member = await dbContext.TeamMembers.FindAsync([teamId, ApplicantId], TestContext.Current.CancellationToken);
			Assert.NotNull(member);
			Assert.True(member.IsActive);
			Assert.False(member.IsAdmin);
			Assert.False(member.IsEditor);
			Assert.Equal(NotificationState.Accepted, dbContext.TeamApplications.Single().State);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.Is<List<BellumGensPushSubscription>>(s => s.Count == 1 && s[0].UserId == ApplicantId),
				It.Is<TeamApplication>(a => a.Id == pending.Id),
				NotificationState.Accepted), Times.Once);
		}

		[Fact]
		public async Task ApproveApplication_ReturnsNotFound_WhenNoApplicationExists()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "approve-missing", "sg-approve-missing");

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.ApproveApplication(new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId });

			Assert.IsType<NotFoundResult>(result);
			Assert.Null(await dbContext.TeamMembers.FindAsync([teamId, ApplicantId], TestContext.Current.CancellationToken));
			VerifyNoApplicationNotificationSent();
		}

		[Fact]
		public async Task ApproveApplication_ReturnsBadRequest_WhenApplicantAlreadyMember()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "approve-dup", "sg-approve-dup");
			var pending = new TeamApplication { ApplicantId = MemberId, TeamId = teamId, State = NotificationState.NotSeen };
			dbContext.TeamApplications.Add(pending);
			dbContext.SaveChanges();

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.ApproveApplication(new TeamApplication { ApplicantId = MemberId, TeamId = teamId });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Single(dbContext.TeamMembers.Where(m => m.TeamId == teamId && m.UserId == MemberId));
			VerifyNoApplicationNotificationSent();
		}

		[Fact]
		public async Task RejectApplication_RejectsApplication_WhenAdmin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "reject-ok", "sg-reject-ok");
			var pending = new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, State = NotificationState.NotSeen };
			dbContext.TeamApplications.Add(pending);
			dbContext.SaveChanges();

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.RejectApplication(
				new TeamApplication { Id = pending.Id, ApplicantId = ApplicantId, TeamId = teamId });

			Assert.IsType<OkObjectResult>(result);
			Assert.Equal(NotificationState.Rejected, dbContext.TeamApplications.Single().State);
			Assert.Null(await dbContext.TeamMembers.FindAsync([teamId, ApplicantId], TestContext.Current.CancellationToken));
		}

		[Fact]
		public async Task RejectApplication_ReturnsNotFound_WhenNoApplicationExists()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "reject-missing", "sg-reject-missing");

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.RejectApplication(new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId });

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task InviteToTeam_CreatesInvite_AndNotifiesInvitedUser_WhenAdmin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "invite-ok", "sg-invite-ok");
			AddPushSubscription(dbContext, OutsiderId);

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.InviteToTeam(new InviteModel { TeamId = teamId, UserId = OutsiderId });

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(OutsiderId, okResult.Value);
			var invite = Assert.Single(dbContext.TeamInvites);
			Assert.Equal(AdminId, invite.InvitingUserId);
			Assert.Equal(OutsiderId, invite.InvitedUserId);
			Assert.Equal(teamId, invite.TeamId);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.Is<List<BellumGensPushSubscription>>(s => s.Count == 1 && s[0].UserId == OutsiderId),
				It.Is<TeamInvite>(i => i.Id == invite.Id)), Times.Once);
		}

		[Fact]
		public async Task InviteToTeam_ResetsExistingInvite_InsteadOfDuplicating()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "invite-again", "sg-invite-again");
			var existing = new TeamInvite { InvitingUserId = AdminId, InvitedUserId = OutsiderId, TeamId = teamId, State = NotificationState.Rejected };
			dbContext.TeamInvites.Add(existing);
			dbContext.SaveChanges();

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.InviteToTeam(new InviteModel { TeamId = teamId, UserId = OutsiderId });

			Assert.IsType<OkObjectResult>(result);
			var invite = Assert.Single(dbContext.TeamInvites);
			Assert.Equal(existing.Id, invite.Id);
			Assert.Equal(NotificationState.NotSeen, invite.State);
		}

		[Fact]
		public async Task ApplyToTeam_NotifiesAdmin_WithTeamAndApplicantLoaded()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "apply-nav", "sg-apply-nav");
			dbContext.ChangeTracker.Clear();

			var controller = CreateControllerAs(dbContext, ApplicantId);

			var result = await controller.ApplyToTeam(new TeamApplication { ApplicantId = ApplicantId, TeamId = teamId, Message = "hi" });

			Assert.IsType<OkObjectResult>(result);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.IsAny<List<BellumGensPushSubscription>>(),
				It.Is<TeamApplication>(a => a.Team != null && a.Team.TeamName == "RolesTeam" && a.User != null && a.User.Id == ApplicantId)), Times.Once);
		}

		[Fact]
		public async Task InviteToTeam_NotifiesInvitedUser_WithTeamLoaded()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "invite-nav", "sg-invite-nav");
			dbContext.ChangeTracker.Clear();

			var controller = CreateControllerAs(dbContext, AdminId);

			var result = await controller.InviteToTeam(new InviteModel { TeamId = teamId, UserId = OutsiderId });

			Assert.IsType<OkObjectResult>(result);
			_mockNotificationService.Verify(n => n.SendNotificationAsync(
				It.IsAny<List<BellumGensPushSubscription>>(),
				It.Is<TeamInvite>(i => i.TeamInfo != null && i.TeamInfo.TeamName == "RolesTeam")), Times.Once);
		}

		[Fact]
		public async Task GetTeamApplications_ReturnsBadRequest_WhenAuthenticatedUserNoLongerExists()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var teamId = SeedTeamWithRoles(dbContext, "deleted-user", "sg-deleted-user");
			_mockUserManager.Setup(m => m.FindByIdAsync("deleted")).ReturnsAsync((ApplicationUser?)null);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("deleted"));

			var result = await controller.GetTeamApplications(teamId);

			Assert.IsType<BadRequestObjectResult>(result);
		}
	}
}
