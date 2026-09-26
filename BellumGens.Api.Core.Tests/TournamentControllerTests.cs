using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BellumGens.Api.Controllers;
using BellumGens.Api.Core;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
	public class TournamentControllerTests
	{
		private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
		private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
		private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
		private readonly Mock<IEmailService> _mockEmailService;
		private readonly AppConfiguration _appConfig;
		private readonly Mock<INotificationService> _mockNotificationService;
		private readonly Mock<ILogger<AccountController>> _mockLogger;

		public TournamentControllerTests()
		{
			_mockUserManager = TestUtils.CreateMockUserManager();
			_mockRoleManager = TestUtils.CreateMockRoleManager();
			_mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
			_mockEmailService = TestUtils.CreateMockEmailService();
			_appConfig = TestUtils.CreateAppConfiguration();
			_mockNotificationService = TestUtils.CreateMockNotificationService();
			_mockLogger = TestUtils.CreateMockLogger<AccountController>();
		}

		private TournamentController CreateController(BellumGensDbContext dbContext)
		{
			return new TournamentController(
				_appConfig, _mockNotificationService.Object,
				_mockUserManager.Object, _mockRoleManager.Object,
				_mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
		}

		[Fact]
		public async Task GetActiveTournament_ReturnsActiveTournament()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			dbContext.Tournaments.Add(new Tournament { Name = "ActiveTourney", Active = true });
			dbContext.Tournaments.Add(new Tournament { Name = "InactiveTourney", Active = false });
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetActiveTournament();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var tournament = Assert.IsType<Tournament>(okResult.Value);
			Assert.Equal("ActiveTourney", tournament.Name);
			Assert.True(tournament.Active);
		}

		[Fact]
		public async Task Get_ReturnsTournament_ById()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "TestTourney" });
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.Get(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var tournament = Assert.IsType<Tournament>(okResult.Value);
			Assert.Equal("TestTourney", tournament.Name);
		}

		[Fact]
		public async Task GetActiveTournament_ReturnsNull_WhenNoneActive()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			dbContext.Tournaments.Add(new Tournament { Name = "InactiveTourney", Active = false });
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetActiveTournament();

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Null(okResult.Value);
		}

		[Fact]
		public async Task GetTournaments_ReturnsAllTournaments()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			dbContext.Tournaments.Add(new Tournament { Name = "Tourney1", Active = true });
			dbContext.Tournaments.Add(new Tournament { Name = "Tourney2", Active = false });
			dbContext.Tournaments.Add(new Tournament { Name = "Tourney3", Active = false });
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetTournaments();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var tournaments = Assert.IsAssignableFrom<List<Tournament>>(okResult.Value);
			Assert.Equal(3, tournaments.Count);
		}

		[Fact]
		public async Task GetTotalRegistrationsCount_ReturnsCountByGame()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "CountTourney", Active = true });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = Guid.NewGuid(),
				Email = "a@b.com", UserId = "u1"
			});
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = Guid.NewGuid(),
				Email = "b@b.com", UserId = "u2"
			});
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				Email = "c@b.com", UserId = "u3"
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetTotalRegistrationsCount(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var counts = Assert.IsAssignableFrom<List<RegistrationCountViewModel>>(okResult.Value);
			Assert.Equal(2, counts.Count);
			Assert.Equal(2, counts.First(c => c.game == Game.CSGO).count);
			Assert.Equal(1, counts.First(c => c.game == Game.StarCraft2).count);
		}

		[Fact]
		public async Task GetCSGORegistrations_ReturnsCSGORegistrations()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var teamId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "CSGOTourney", Active = true });
			dbContext.CSGOTeams.Add(new CSGOTeam { TeamId = teamId, TeamName = "Team1", CustomUrl = "team-1", SteamGroupId = "sg1" });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = teamId,
				Email = "a@b.com", UserId = "u1"
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetCSGORegistrations(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var registrations = Assert.IsAssignableFrom<List<TournamentCSGOParticipant>>(okResult.Value);
			Assert.Single(registrations);
		}

		[Fact]
		public async Task GetSC2Registrations_ReturnsSC2Registrations()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var userId = "sc2player";
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "SC2Tourney", Active = true });
			dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "sc2user" });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				Email = "a@b.com", UserId = userId
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetSC2sRegistrations(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var registrations = Assert.IsAssignableFrom<List<TournamentSC2Participant>>(okResult.Value);
			Assert.Single(registrations);
		}

		[Fact]
		public async Task GetCSGOGroups_ReturnsGroups()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "GroupTourney", Active = true });
			dbContext.TournamentCSGOGroups.Add(new TournamentCSGOGroup
			{
				Name = "Group A", TournamentId = tournamentId
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetCSGOGroups(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var groups = Assert.IsAssignableFrom<List<TournamentCSGOGroup>>(okResult.Value);
			Assert.Single(groups);
			Assert.Equal("Group A", groups[0].Name);
		}

		[Fact]
		public async Task GetSC2Groups_ReturnsGroups()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "SC2GroupTourney", Active = true });
			dbContext.TournamentSC2Groups.Add(new TournamentSC2Group
			{
				Name = "Group B", TournamentId = tournamentId
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetSC2Groups(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var groups = Assert.IsAssignableFrom<List<TournamentSC2Group>>(okResult.Value);
			Assert.Single(groups);
			Assert.Equal("Group B", groups[0].Name);
		}

		[Fact]
		public async Task GetRegistrationForTournament_ReturnsRegistration()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			var tournamentId = Guid.NewGuid();
			dbContext.Users.Add(user);
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "Test" });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, UserId = userId, Game = Game.CSGO,
				TeamId = Guid.NewGuid(), Email = "a@b.com"
			});
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.GetRegistrationForTournament(tournamentId);

			Assert.NotNull(result);
			Assert.Equal(tournamentId, result.TournamentId);
			Assert.Equal(userId, result.UserId);
		}

		[Fact]
		public async Task GetUserRegistrations_ReturnsUserRegistrations()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			var tournamentId = Guid.NewGuid();
			dbContext.Users.Add(user);
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "Test" });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, UserId = userId, Game = Game.CSGO,
				TeamId = Guid.NewGuid(), Email = "a@b.com"
			});
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.GetUserRegistrations();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var registrations = Assert.IsAssignableFrom<List<TournamentApplication>>(okResult.Value);
			Assert.Single(registrations);
		}

		[Fact]
		public async Task GetAllApplications_ReturnsAll()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "Test" });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = Guid.NewGuid(),
				Email = "a@b.com", UserId = "u1"
			});
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "p#1",
				Email = "b@b.com", UserId = "u2"
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));

			var result = await controller.GetAllApplications();

			var okResult = Assert.IsType<OkObjectResult>(result);
			var apps = Assert.IsAssignableFrom<List<TournamentApplication>>(okResult.Value);
			Assert.Equal(2, apps.Count);
		}

		[Fact]
		public async Task GetApplications_ReturnsForTournament()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId1 = Guid.NewGuid();
			var tournamentId2 = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId1, Name = "T1" });
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId2, Name = "T2" });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId1, Game = Game.CSGO, TeamId = Guid.NewGuid(),
				Email = "a@b.com", UserId = "u1"
			});
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId2, Game = Game.CSGO, TeamId = Guid.NewGuid(),
				Email = "b@b.com", UserId = "u2"
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));

			var result = await controller.GetApplications(tournamentId1);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var apps = Assert.IsAssignableFrom<List<TournamentApplication>>(okResult.Value);
			Assert.Single(apps);
			Assert.Equal(tournamentId1, apps[0].TournamentId);
		}

		[Fact]
		public async Task CreateTournament_CreatesNew_ReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));

			var tournament = new Tournament { Name = "NewTournament", Active = true };
			var result = await controller.CreateTournament(tournament);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var saved = Assert.IsType<Tournament>(okResult.Value);
			Assert.Equal("NewTournament", saved.Name);
			Assert.Single(dbContext.Tournaments);
		}

		[Fact]
		public async Task CreateTournament_UpdatesExisting_ReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "OldName", Active = false });
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));

			var tournament = new Tournament { ID = tournamentId, Name = "NewName", Active = true };
			var result = await controller.CreateTournament(tournament);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Single(dbContext.Tournaments);
			var updated = dbContext.Tournaments.Find(tournamentId);
			Assert.Equal("NewName", updated!.Name);
			Assert.True(updated.Active);
		}

		[Fact]
		public async Task CreateTournament_InvalidModel_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));
			controller.ModelState.AddModelError("Name", "Required");

			var tournament = new Tournament();
			var result = await controller.CreateTournament(tournament);

			var badResult = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Equal("Invalid tournament", badResult.Value);
		}

		[Fact]
		public async Task DeleteRegistraion_OwnRegistration_ReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			var appId = Guid.NewGuid();
			dbContext.Users.Add(user);
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = userId,
				Game = Game.CSGO, TeamId = Guid.NewGuid(), Email = "a@b.com"
			});
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin")).ReturnsAsync(false);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.DeleteRegistraion(appId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(appId, okResult.Value);
			Assert.Empty(dbContext.TournamentApplications);
		}

		[Fact]
		public async Task DeleteRegistraion_NotOwnRegistration_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var userId = "user1";
			var user = new ApplicationUser { Id = userId, UserName = "testuser" };
			var appId = Guid.NewGuid();
			dbContext.Users.Add(user);
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "otherUser",
				Game = Game.CSGO, TeamId = Guid.NewGuid(), Email = "a@b.com"
			});
			dbContext.SaveChanges();

			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.IsInRoleAsync(user, "admin")).ReturnsAsync(false);

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

			var result = await controller.DeleteRegistraion(appId);

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task GetCSGOMatches_WithTournamentIdFilter()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var otherTournamentId = Guid.NewGuid();
			var team1Id = Guid.NewGuid();
			var team2Id = Guid.NewGuid();
			var team3Id = Guid.NewGuid();
			var team4Id = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "T1" });
			dbContext.Tournaments.Add(new Tournament { ID = otherTournamentId, Name = "T2" });
			dbContext.CSGOTeams.Add(new CSGOTeam { TeamId = team1Id, TeamName = "A", CustomUrl = "a", SteamGroupId = "s1" });
			dbContext.CSGOTeams.Add(new CSGOTeam { TeamId = team2Id, TeamName = "B", CustomUrl = "b", SteamGroupId = "s2" });
			dbContext.CSGOTeams.Add(new CSGOTeam { TeamId = team3Id, TeamName = "C", CustomUrl = "c", SteamGroupId = "s3" });
			dbContext.CSGOTeams.Add(new CSGOTeam { TeamId = team4Id, TeamName = "D", CustomUrl = "d", SteamGroupId = "s4" });
			dbContext.TournamentCSGOMatches.Add(new TournamentCSGOMatch
			{
				TournamentId = tournamentId, Team1Id = team1Id, Team2Id = team2Id
			});
			dbContext.TournamentCSGOMatches.Add(new TournamentCSGOMatch
			{
				TournamentId = otherTournamentId, Team1Id = team3Id, Team2Id = team4Id
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetCSGOMatches(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var matches = Assert.IsAssignableFrom<List<TournamentCSGOMatch>>(okResult.Value);
			Assert.Single(matches);
		}

		[Fact]
		public async Task GetCSGOMatch_Found_ReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			dbContext.TournamentCSGOMatches.Add(new TournamentCSGOMatch
			{
				Id = matchId, Team1Id = Guid.NewGuid(), Team2Id = Guid.NewGuid()
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetCSGOMatch(matchId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var match = Assert.IsType<TournamentCSGOMatch>(okResult.Value);
			Assert.Equal(matchId, match.Id);
		}

		[Fact]
		public async Task GetCSGOMatch_NotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetCSGOMatch(Guid.NewGuid());

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task GetSC2Matches_WithTournamentIdFilter()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var otherTournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "T1" });
			dbContext.Tournaments.Add(new Tournament { ID = otherTournamentId, Name = "T2" });
			dbContext.TournamentSC2Matches.Add(new TournamentSC2Match
			{
				TournamentId = tournamentId, Player1Id = "p1", Player2Id = "p2"
			});
			dbContext.TournamentSC2Matches.Add(new TournamentSC2Match
			{
				TournamentId = otherTournamentId, Player1Id = "p3", Player2Id = "p4"
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetSC2Matches(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var matches = Assert.IsAssignableFrom<List<TournamentSC2Match>>(okResult.Value);
			Assert.Single(matches);
		}

		[Fact]
		public async Task GetSC2Match_Found_ReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			dbContext.TournamentSC2Matches.Add(new TournamentSC2Match
			{
				Id = matchId, Player1Id = "p1", Player2Id = "p2"
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetSC2Match(matchId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var match = Assert.IsType<TournamentSC2Match>(okResult.Value);
			Assert.Equal(matchId, match.Id);
		}

		[Fact]
		public async Task GetSC2Match_NotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetSC2Match(Guid.NewGuid());

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task WeeklyCheckin_ValidHash_Redirect()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "u1",
				Game = Game.CSGO, TeamId = Guid.NewGuid(), Email = "a@b.com",
				Hash = "abc12345", State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.WeeklyCheckin(appId, "abc12345");

			var redirectResult = Assert.IsType<RedirectResult>(result);
			Assert.Contains("Checkin successful", redirectResult.Url);
			var updated = dbContext.TournamentApplications.Find(appId);
			Assert.Equal(TournamentApplicationState.Confirmed, updated!.State);
		}

		[Fact]
		public async Task WeeklyCheckin_InvalidHash_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "u1",
				Game = Game.CSGO, TeamId = Guid.NewGuid(), Email = "a@b.com",
				Hash = "abc12345", State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.WeeklyCheckin(appId, "wronghash");

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task ConfirmRegistration_Found_ReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "u1",
				Game = Game.CSGO, TeamId = Guid.NewGuid(), Email = "a@b.com",
				State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));

			var updatedApp = new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "u1",
				Game = Game.CSGO, TeamId = Guid.NewGuid(), Email = "a@b.com",
				State = TournamentApplicationState.Confirmed
			};
			var result = await controller.ConfirmRegistration(appId, updatedApp);

			var okResult = Assert.IsType<OkObjectResult>(result);
		}

		[Fact]
		public async Task ConfirmRegistration_NotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));

			var updatedApp = new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), UserId = "u1",
				Game = Game.CSGO, TeamId = Guid.NewGuid(), Email = "a@b.com"
			};
			var result = await controller.ConfirmRegistration(Guid.NewGuid(), updatedApp);

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task DeleteGroup_CSGOGroupFound_ReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "Test" });
			dbContext.TournamentCSGOGroups.Add(new TournamentCSGOGroup
			{
				Id = groupId, Name = "Group A", TournamentId = tournamentId
			});
			dbContext.SaveChanges();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));

			var result = await controller.DeleteGroup(groupId);

			Assert.IsType<OkResult>(result);
			Assert.Empty(dbContext.TournamentCSGOGroups);
		}

		[Fact]
		public async Task DeleteGroup_NotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();

			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("admin1"));

			var result = await controller.DeleteGroup(Guid.NewGuid());

			Assert.IsType<NotFoundResult>(result);
		}

		private const string NoSendEmail = "player@example.com";

		private ApplicationUser SetupAuthUser(string userId, params string[] roles)
		{
			var user = new ApplicationUser { Id = userId, UserName = userId };
			_mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
			_mockUserManager.Setup(m => m.IsInRoleAsync(user, It.IsAny<string>()))
				.ReturnsAsync((ApplicationUser _, string role) => roles.Contains(role));
			return user;
		}

		private TournamentController CreateAuthenticatedController(BellumGensDbContext dbContext, string userId)
		{
			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));
			return controller;
		}

		#region Authorization attributes

		[Theory]
		[InlineData(nameof(TournamentController.SubmitCSGOMatch))]
		[InlineData(nameof(TournamentController.DeleteCSGOMatch))]
		[InlineData(nameof(TournamentController.SubmitCSGOMatchMap))]
		[InlineData(nameof(TournamentController.DeleteCSGOMatchMap))]
		[InlineData(nameof(TournamentController.SubmitSC2Match))]
		[InlineData(nameof(TournamentController.DeleteSC2Match))]
		[InlineData(nameof(TournamentController.SubmitSC2MatchMap))]
		[InlineData(nameof(TournamentController.DeleteSC2MatchMap))]
		[InlineData(nameof(TournamentController.SubmitCSGOGroup))]
		[InlineData(nameof(TournamentController.SubmitSC2Group))]
		[InlineData(nameof(TournamentController.DeleteGroup))]
		[InlineData(nameof(TournamentController.AddToGroup))]
		[InlineData(nameof(TournamentController.RemoveFromGroup))]
		[InlineData(nameof(TournamentController.SubmitParticipantPoints))]
		[InlineData(nameof(TournamentController.ResetRegistrationsState))]
		public void EventAdminEndpoints_RequireAdminOrEventAdminRole(string methodName)
		{
			var method = typeof(TournamentController).GetMethod(methodName);

			Assert.NotNull(method);
			var authorize = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
			Assert.Equal(new[] { "admin", "event-admin" }, authorize.Roles!.Split(',').Select(r => r.Trim()).ToArray());
			Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
		}

		[Fact]
		public void SendCheckinEmails_RequiresAdminRoleOnly()
		{
			var method = typeof(TournamentController).GetMethod(nameof(TournamentController.SendCheckinEmails));

			var authorize = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
			Assert.Equal("admin", authorize.Roles);
			Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
		}

		[Theory]
		[InlineData(nameof(TournamentController.Register))]
		[InlineData(nameof(TournamentController.RegisterForBGE))]
		public void RegistrationEndpoints_RequireAuthenticatedUser(string methodName)
		{
			Assert.NotEmpty(typeof(TournamentController).GetCustomAttributes(typeof(AuthorizeAttribute), false));
			var method = typeof(TournamentController).GetMethod(methodName);

			Assert.NotNull(method);
			Assert.Empty(method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
		}

		#endregion

		#region Register

		[Fact]
		public async Task Register_InvalidModel_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");
			controller.ModelState.AddModelError("Email", "Invalid");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), Game = Game.CSGO, TeamId = Guid.NewGuid(), Email = NoSendEmail
			});

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TournamentApplications);
		}

		[Fact]
		public async Task Register_SC2_MissingBattleNetId_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), Game = Game.StarCraft2, CompanyId = "Acme", Email = NoSendEmail
			});

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TournamentApplications);
			Assert.Empty(dbContext.Companies);
		}

		[Fact]
		public async Task Register_SC2_DuplicateBattleNetIdInSameTournament_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				Email = "a@b.com", UserId = "other"
			});
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				CompanyId = "Acme", Email = NoSendEmail
			});

			var badResult = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Contains("player#1", badResult.Value as string);
			Assert.Single(dbContext.TournamentApplications);
		}

		[Fact]
		public async Task Register_SC2_Success_SavesApplicationAndSetsUserBattleNetId()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "EBL", Active = true });
			// Same BattleNetId in a different tournament must not block registration
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), Game = Game.StarCraft2, BattleNetId = "player#1",
				Email = "a@b.com", UserId = "other"
			});
			dbContext.SaveChanges();
			var user = SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var application = new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				CompanyId = "Acme", Email = NoSendEmail
			};
			var result = await controller.Register(application);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var saved = Assert.IsType<TournamentApplication>(okResult.Value);
			Assert.Equal("user1", saved.UserId);
			Assert.Equal(8, saved.Hash.Length);
			Assert.Equal("player#1", user.BattleNetId);
			Assert.Equal(2, dbContext.TournamentApplications.Count());
			var persisted = dbContext.TournamentApplications.Single(a => a.TournamentId == tournamentId);
			Assert.Equal("user1", persisted.UserId);
			Assert.Equal(TournamentApplicationState.Pending, persisted.State);
			Assert.NotNull(dbContext.Companies.Find("Acme"));
		}

		[Fact]
		public async Task Register_SC2_DoesNotOverwriteExistingUserBattleNetId()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var user = SetupAuthUser("user1");
			user.BattleNetId = "original#1";
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), Game = Game.StarCraft2, BattleNetId = "smurf#2",
				CompanyId = "Acme", Email = NoSendEmail
			});

			Assert.IsType<OkObjectResult>(result);
			Assert.Equal("original#1", user.BattleNetId);
		}

		[Fact]
		public async Task Register_CSGO_EmptyTeamId_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), Game = Game.CSGO, TeamId = Guid.Empty,
				CompanyId = "Acme", Email = NoSendEmail
			});

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TournamentApplications);
		}

		[Fact]
		public async Task Register_CSGO_DuplicateTeamInSameTournament_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var teamId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = teamId,
				Email = "a@b.com", UserId = "other"
			});
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = teamId,
				CompanyId = "Acme", Email = NoSendEmail
			});

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Single(dbContext.TournamentApplications);
		}

		[Fact]
		public async Task Register_CSGO_Success_ExistingCompanyNotDuplicated()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var teamId = Guid.NewGuid();
			dbContext.Companies.Add(new Company { Name = "Acme" });
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = teamId,
				CompanyId = "Acme", Email = NoSendEmail
			});

			var okResult = Assert.IsType<OkObjectResult>(result);
			var saved = Assert.IsType<TournamentApplication>(okResult.Value);
			Assert.Equal("user1", saved.UserId);
			Assert.False(string.IsNullOrEmpty(saved.Hash));
			var persisted = Assert.Single(dbContext.TournamentApplications);
			Assert.Equal(teamId, persisted.TeamId);
			Assert.Single(dbContext.Companies);
		}

		[Fact]
		public async Task Register_KeepsProvidedHash()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), Game = Game.CSGO, TeamId = Guid.NewGuid(),
				CompanyId = "Acme", Email = NoSendEmail, Hash = "fixedhsh"
			});

			Assert.IsType<OkObjectResult>(result);
			Assert.Equal("fixedhsh", Assert.Single(dbContext.TournamentApplications).Hash);
		}

		#endregion

		#region RegisterForBGE

		[Fact]
		public async Task RegisterForBGE_InvalidModel_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");
			controller.ModelState.AddModelError("Email", "Invalid");

			var result = await controller.RegisterForBGE(null, new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), Game = Game.StarCraft2, Email = NoSendEmail
			});

			var badResult = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Equal("We couldn't validate your submission...", badResult.Value);
			Assert.Empty(dbContext.TournamentApplications);
		}

		[Fact]
		public async Task RegisterForBGE_NewRegistration_SavesApplicationAndCompany()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly", Active = true });
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.RegisterForBGE(null, new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				CompanyId = "Acme", Email = NoSendEmail
			});

			var okResult = Assert.IsType<OkObjectResult>(result);
			var saved = Assert.IsType<TournamentApplication>(okResult.Value);
			Assert.Equal("user1", saved.UserId);
			Assert.Equal(8, saved.Hash.Length);
			var persisted = Assert.Single(dbContext.TournamentApplications);
			Assert.Equal(tournamentId, persisted.TournamentId);
			Assert.Equal("user1", persisted.UserId);
			Assert.NotNull(dbContext.Companies.Find("Acme"));
		}

		[Fact]
		public async Task RegisterForBGE_NewRegistration_WithoutCompany_DoesNotCreateCompany()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly", Active = true });
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.RegisterForBGE(null, new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				CompanyId = null, Email = NoSendEmail
			});

			Assert.IsType<OkObjectResult>(result);
			Assert.Single(dbContext.TournamentApplications);
			Assert.Empty(dbContext.Companies);
		}

		[Fact]
		public async Task RegisterForBGE_WithTournamentId_UpdatesExistingApplication()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "old#1",
				Email = "a@b.com", UserId = "user1", Hash = "abc12345"
			});
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.RegisterForBGE(tournamentId, new TournamentApplication
			{
				Id = appId, TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "new#2",
				Email = "a@b.com", UserId = "user1", Hash = "abc12345", Discord = "disc#1"
			});

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentApplications);
			Assert.Equal("new#2", persisted.BattleNetId);
			Assert.Equal("disc#1", persisted.Discord);
		}

		[Fact]
		public async Task RegisterForBGE_WithTournamentId_ApplicationNotFound_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.RegisterForBGE(Guid.NewGuid(), new TournamentApplication
			{
				Id = Guid.NewGuid(), TournamentId = Guid.NewGuid(), Game = Game.StarCraft2,
				Email = NoSendEmail, UserId = "user1"
			});

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TournamentApplications);
		}

		#endregion

		#region ResetRegistrationsState / SendCheckinEmails / WeeklyCheckin

		[Fact]
		public async Task ResetRegistrationsState_ResetsNonBannedApplicationsInTournament()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var otherTournamentId = Guid.NewGuid();
			var confirmedId = Guid.NewGuid();
			var pendingId = Guid.NewGuid();
			var bannedId = Guid.NewGuid();
			var otherId = Guid.NewGuid();
			dbContext.TournamentApplications.AddRange(
				new TournamentApplication { Id = confirmedId, TournamentId = tournamentId, UserId = "u1", Email = "a@b.com", State = TournamentApplicationState.Confirmed },
				new TournamentApplication { Id = pendingId, TournamentId = tournamentId, UserId = "u2", Email = "a@b.com", State = TournamentApplicationState.Pending },
				new TournamentApplication { Id = bannedId, TournamentId = tournamentId, UserId = "u3", Email = "a@b.com", State = TournamentApplicationState.Banned },
				new TournamentApplication { Id = otherId, TournamentId = otherTournamentId, UserId = "u4", Email = "a@b.com", State = TournamentApplicationState.Confirmed });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.ResetRegistrationsState(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var message = okResult.Value!.GetType().GetProperty("message")!.GetValue(okResult.Value) as string;
			Assert.Equal("2 registrations have been set to pending state.", message);
			Assert.Equal(TournamentApplicationState.Pending, dbContext.TournamentApplications.Find(confirmedId)!.State);
			Assert.Equal(TournamentApplicationState.Pending, dbContext.TournamentApplications.Find(pendingId)!.State);
			Assert.Equal(TournamentApplicationState.Banned, dbContext.TournamentApplications.Find(bannedId)!.State);
			Assert.Equal(TournamentApplicationState.Confirmed, dbContext.TournamentApplications.Find(otherId)!.State);
		}

		[Fact]
		public async Task SendCheckinEmails_BuildsCheckinLinkForEachNonBannedApplication()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly" });
			dbContext.TournamentApplications.AddRange(
				new TournamentApplication { TournamentId = tournamentId, UserId = "u1", Email = NoSendEmail, Hash = "hash0001", State = TournamentApplicationState.Pending },
				new TournamentApplication { TournamentId = tournamentId, UserId = "u2", Email = NoSendEmail, Hash = "hash0002", State = TournamentApplicationState.Confirmed },
				new TournamentApplication { TournamentId = tournamentId, UserId = "u3", Email = NoSendEmail, Hash = "hash0003", State = TournamentApplicationState.Banned },
				new TournamentApplication { TournamentId = Guid.NewGuid(), UserId = "u4", Email = NoSendEmail, Hash = "hash0004" });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");
			var mockUrl = new Mock<IUrlHelper>();
			mockUrl.SetupGet(u => u.ActionContext).Returns(controller.ControllerContext);
			mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://test/api/tournament/checkin");
			controller.Url = mockUrl.Object;

			var result = await controller.SendCheckinEmails(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var message = okResult.Value!.GetType().GetProperty("message")!.GetValue(okResult.Value) as string;
			Assert.Equal("2 emails sent", message);
			mockUrl.Verify(u => u.Action(It.Is<UrlActionContext>(c => c.Action == "WeeklyCheckin" && c.Controller == "Tournament")), Times.Exactly(2));
		}

		[Fact]
		public async Task WeeklyCheckin_Owner_ConfirmsApplication()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "user1", Email = "a@b.com",
				State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.WeeklyCheckin(appId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(appId, Assert.IsType<TournamentApplication>(okResult.Value).Id);
			Assert.Equal(TournamentApplicationState.Confirmed, dbContext.TournamentApplications.Find(appId)!.State);
		}

		[Fact]
		public async Task WeeklyCheckin_AdminForOtherUser_ConfirmsApplication()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "player", Email = "a@b.com",
				State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();
			SetupAuthUser("admin1", "admin");
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.WeeklyCheckin(appId);

			Assert.IsType<OkObjectResult>(result);
			Assert.Equal(TournamentApplicationState.Confirmed, dbContext.TournamentApplications.Find(appId)!.State);
		}

		[Fact]
		public async Task WeeklyCheckin_NonOwnerNonAdmin_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "player", Email = "a@b.com",
				State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();
			SetupAuthUser("user1", "event-admin");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.WeeklyCheckin(appId);

			Assert.IsType<NotFoundResult>(result);
			Assert.Equal(TournamentApplicationState.Pending, dbContext.TournamentApplications.Find(appId)!.State);
		}

		[Fact]
		public async Task WeeklyCheckin_BannedApplication_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), UserId = "user1", Email = "a@b.com",
				State = TournamentApplicationState.Banned
			});
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.WeeklyCheckin(appId);

			Assert.IsType<NotFoundResult>(result);
			Assert.Equal(TournamentApplicationState.Banned, dbContext.TournamentApplications.Find(appId)!.State);
		}

		#endregion

		#region Groups

		[Fact]
		public async Task SubmitCSGOGroup_New_AssignsActiveTournamentAndSaves()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var activeId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = Guid.NewGuid(), Name = "Old", Active = false });
			dbContext.Tournaments.Add(new Tournament { ID = activeId, Name = "Current", Active = true });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitCSGOGroup(null, new TournamentCSGOGroup { Name = "Group A" });

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(activeId, Assert.IsType<TournamentCSGOGroup>(okResult.Value).TournamentId);
			var persisted = Assert.Single(dbContext.TournamentCSGOGroups);
			Assert.Equal("Group A", persisted.Name);
			Assert.Equal(activeId, persisted.TournamentId);
		}

		[Fact]
		public async Task SubmitCSGOGroup_Existing_UpdatesGroup()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var tournamentId = Guid.NewGuid();
			dbContext.TournamentCSGOGroups.Add(new TournamentCSGOGroup { Id = groupId, Name = "Old", TournamentId = tournamentId });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitCSGOGroup(groupId, new TournamentCSGOGroup { Id = groupId, Name = "Renamed", TournamentId = tournamentId });

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentCSGOGroups);
			Assert.Equal("Renamed", persisted.Name);
		}

		[Fact]
		public async Task SubmitSC2Group_New_AssignsActiveTournamentAndSaves()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var activeId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = activeId, Name = "Current", Active = true });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitSC2Group(null, new TournamentSC2Group { Name = "Group B" });

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentSC2Groups);
			Assert.Equal("Group B", persisted.Name);
			Assert.Equal(activeId, persisted.TournamentId);
			Assert.Empty(dbContext.TournamentCSGOGroups);
		}

		[Fact]
		public async Task SubmitSC2Group_Existing_UpdatesGroup()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var tournamentId = Guid.NewGuid();
			dbContext.TournamentSC2Groups.Add(new TournamentSC2Group { Id = groupId, Name = "Old", TournamentId = tournamentId });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitSC2Group(groupId, new TournamentSC2Group { Id = groupId, Name = "Renamed", TournamentId = tournamentId });

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentSC2Groups);
			Assert.Equal("Renamed", persisted.Name);
		}

		[Fact]
		public async Task DeleteGroup_SC2GroupFound_ReturnsOk()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var csgoGroupId = Guid.NewGuid();
			var tournamentId = Guid.NewGuid();
			dbContext.TournamentSC2Groups.Add(new TournamentSC2Group { Id = groupId, Name = "SC2 Group", TournamentId = tournamentId });
			dbContext.TournamentCSGOGroups.Add(new TournamentCSGOGroup { Id = csgoGroupId, Name = "CSGO Group", TournamentId = tournamentId });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.DeleteGroup(groupId);

			Assert.IsType<OkResult>(result);
			Assert.Empty(dbContext.TournamentSC2Groups);
			Assert.Single(dbContext.TournamentCSGOGroups);
		}

		[Fact]
		public async Task AddToGroup_CSGOGroup_AddsParticipant()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var appId = Guid.NewGuid();
			dbContext.TournamentCSGOGroups.Add(new TournamentCSGOGroup { Id = groupId, Name = "A", TournamentId = Guid.NewGuid() });
			dbContext.TournamentApplications.Add(new TournamentApplication { Id = appId, TournamentId = Guid.NewGuid(), UserId = "u1", Email = "a@b.com" });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.AddToGroup(groupId, new TournamentApplication { Id = appId });

			Assert.IsType<OkResult>(result);
			var participant = Assert.Single(dbContext.TournamentGroupParticipants);
			Assert.Equal(groupId, participant.TournamentGroupId);
			Assert.Equal(appId, participant.TournamentApplicationId);
			Assert.Equal(0, participant.Points);
		}

		[Fact]
		public async Task AddToGroup_SC2Group_AddsParticipant()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var appId = Guid.NewGuid();
			dbContext.TournamentSC2Groups.Add(new TournamentSC2Group { Id = groupId, Name = "B", TournamentId = Guid.NewGuid() });
			dbContext.TournamentApplications.Add(new TournamentApplication { Id = appId, TournamentId = Guid.NewGuid(), UserId = "u1", Email = "a@b.com" });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.AddToGroup(groupId, new TournamentApplication { Id = appId });

			Assert.IsType<OkResult>(result);
			var participant = Assert.Single(dbContext.TournamentGroupParticipants);
			Assert.Equal(groupId, participant.TournamentGroupId);
			Assert.Equal(appId, participant.TournamentApplicationId);
		}

		[Fact]
		public async Task AddToGroup_GroupNotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.AddToGroup(Guid.NewGuid(), new TournamentApplication { Id = Guid.NewGuid() });

			Assert.IsType<NotFoundResult>(result);
			Assert.Empty(dbContext.TournamentGroupParticipants);
		}

		[Fact]
		public async Task RemoveFromGroup_Found_RemovesParticipant()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var appId = Guid.NewGuid();
			var otherAppId = Guid.NewGuid();
			dbContext.TournamentGroupParticipants.Add(new TournamentGroupParticipant { TournamentGroupId = groupId, TournamentApplicationId = appId });
			dbContext.TournamentGroupParticipants.Add(new TournamentGroupParticipant { TournamentGroupId = groupId, TournamentApplicationId = otherAppId });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.RemoveFromGroup(appId, groupId);

			Assert.IsType<OkResult>(result);
			var remaining = Assert.Single(dbContext.TournamentGroupParticipants);
			Assert.Equal(otherAppId, remaining.TournamentApplicationId);
		}

		[Fact]
		public async Task RemoveFromGroup_NotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var appId = Guid.NewGuid();
			dbContext.TournamentGroupParticipants.Add(new TournamentGroupParticipant { TournamentGroupId = groupId, TournamentApplicationId = appId });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			// Arguments swapped: key order must matter
			var result = await controller.RemoveFromGroup(groupId, appId);

			Assert.IsType<NotFoundResult>(result);
			Assert.Single(dbContext.TournamentGroupParticipants);
		}

		[Fact]
		public async Task SubmitParticipantPoints_Found_UpdatesPoints()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var appId = Guid.NewGuid();
			dbContext.TournamentGroupParticipants.Add(new TournamentGroupParticipant { TournamentGroupId = groupId, TournamentApplicationId = appId, Points = 1 });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitParticipantPoints(appId, groupId, new TournamentGroupParticipant { Points = 7 });

			Assert.IsType<OkResult>(result);
			Assert.Equal(7, dbContext.TournamentGroupParticipants.Find(groupId, appId)!.Points);
		}

		[Fact]
		public async Task SubmitParticipantPoints_NotFound_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitParticipantPoints(Guid.NewGuid(), Guid.NewGuid(), new TournamentGroupParticipant { Points = 7 });

			Assert.IsType<NotFoundResult>(result);
			Assert.Empty(dbContext.TournamentGroupParticipants);
		}

		#endregion

		#region CS:GO matches

		[Fact]
		public async Task SubmitCSGOMatch_New_SavesMatchWithMapsAndLoadsTeams()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var team1Id = Guid.NewGuid();
			var team2Id = Guid.NewGuid();
			dbContext.CSGOTeams.Add(new CSGOTeam { TeamId = team1Id, TeamName = "Alpha", CustomUrl = "alpha", SteamGroupId = "s1" });
			dbContext.CSGOTeams.Add(new CSGOTeam { TeamId = team2Id, TeamName = "Beta", CustomUrl = "beta", SteamGroupId = "s2" });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var match = new TournamentCSGOMatch { Team1Id = team1Id, Team2Id = team2Id, Team1Points = 2, Team2Points = 1 };
			match.Maps.Add(new CSGOMatchMap { Map = CSGOMap.Dust2, Team1Score = 16, Team2Score = 10 });
			match.Maps.Add(new CSGOMatchMap { Map = CSGOMap.Inferno, Team1Score = 12, Team2Score = 16 });
			var result = await controller.SubmitCSGOMatch(null, match);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var returned = Assert.IsType<TournamentCSGOMatch>(okResult.Value);
			Assert.Equal("Alpha", returned.Team1!.TeamName);
			Assert.Equal("Beta", returned.Team2!.TeamName);
			Assert.Equal(team1Id, returned.WinnerTeamId);
			var persisted = Assert.Single(dbContext.TournamentCSGOMatches);
			Assert.NotEqual(Guid.Empty, persisted.Id);
			Assert.Equal(2, dbContext.CSGOMatchMaps.Count());
			Assert.All(dbContext.CSGOMatchMaps, m => Assert.Equal(persisted.Id, m.CsgoMatchId));
		}

		[Fact]
		public async Task SubmitCSGOMatch_Existing_UpdatesMatchAndMaps()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			var mapId = Guid.NewGuid();
			var team1Id = Guid.NewGuid();
			var team2Id = Guid.NewGuid();
			dbContext.TournamentCSGOMatches.Add(new TournamentCSGOMatch { Id = matchId, Team1Id = team1Id, Team2Id = team2Id });
			dbContext.CSGOMatchMaps.Add(new CSGOMatchMap { Id = mapId, CsgoMatchId = matchId, Map = CSGOMap.Mirage });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var update = new TournamentCSGOMatch
			{
				Id = matchId, Team1Id = team1Id, Team2Id = team2Id, Team1Points = 0, Team2Points = 2,
				DemoLink = "https://demo"
			};
			update.Maps.Add(new CSGOMatchMap { Id = mapId, CsgoMatchId = matchId, Map = CSGOMap.Mirage, Team1Score = 5, Team2Score = 16 });
			update.Maps.Add(new CSGOMatchMap { CsgoMatchId = matchId, Map = CSGOMap.Nuke, Team1Score = 8, Team2Score = 16 });
			var result = await controller.SubmitCSGOMatch(matchId, update);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var returned = Assert.IsType<TournamentCSGOMatch>(okResult.Value);
			Assert.Equal(matchId, returned.Id);
			var persisted = Assert.Single(dbContext.TournamentCSGOMatches);
			Assert.Equal(2, persisted.Team2Points);
			Assert.Equal("https://demo", persisted.DemoLink);
			Assert.Equal(2, dbContext.CSGOMatchMaps.Count());
			Assert.Equal(16, dbContext.CSGOMatchMaps.Find(mapId)!.Team2Score);
			Assert.Contains(dbContext.CSGOMatchMaps, m => m.Map == CSGOMap.Nuke);
		}

		[Fact]
		public async Task DeleteCSGOMatch_Found_RemovesMatch()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			var otherId = Guid.NewGuid();
			dbContext.TournamentCSGOMatches.Add(new TournamentCSGOMatch { Id = matchId, Team1Id = Guid.NewGuid(), Team2Id = Guid.NewGuid() });
			dbContext.TournamentCSGOMatches.Add(new TournamentCSGOMatch { Id = otherId, Team1Id = Guid.NewGuid(), Team2Id = Guid.NewGuid() });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.DeleteCSGOMatch(matchId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(matchId, Assert.IsType<TournamentCSGOMatch>(okResult.Value).Id);
			Assert.Equal(otherId, Assert.Single(dbContext.TournamentCSGOMatches).Id);
		}

		[Fact]
		public async Task SubmitCSGOMatchMap_New_AddsMap()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitCSGOMatchMap(null, new CSGOMatchMap { CsgoMatchId = matchId, Map = CSGOMap.Overpass, Team1Score = 16 });

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(CSGOMap.Overpass, Assert.IsType<CSGOMatchMap>(okResult.Value).Map);
			var persisted = Assert.Single(dbContext.CSGOMatchMaps);
			Assert.Equal(matchId, persisted.CsgoMatchId);
			Assert.Equal(16, persisted.Team1Score);
		}

		[Fact]
		public async Task SubmitCSGOMatchMap_Existing_UpdatesMap()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			var mapId = Guid.NewGuid();
			var teamId = Guid.NewGuid();
			dbContext.CSGOMatchMaps.Add(new CSGOMatchMap { Id = mapId, CsgoMatchId = matchId, Map = CSGOMap.Dust2 });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitCSGOMatchMap(mapId, new CSGOMatchMap
			{
				Id = mapId, CsgoMatchId = matchId, Map = CSGOMap.Train, TeamPickId = teamId, Team1Score = 16, Team2Score = 14
			});

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.CSGOMatchMaps);
			Assert.Equal(CSGOMap.Train, persisted.Map);
			Assert.Equal(teamId, persisted.TeamPickId);
			Assert.Equal(14, persisted.Team2Score);
		}

		[Fact]
		public async Task DeleteCSGOMatchMap_Found_RemovesMap()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var mapId = Guid.NewGuid();
			dbContext.CSGOMatchMaps.Add(new CSGOMatchMap { Id = mapId, CsgoMatchId = Guid.NewGuid(), Map = CSGOMap.Dust2 });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.DeleteCSGOMatchMap(mapId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(mapId, Assert.IsType<CSGOMatchMap>(okResult.Value).Id);
			Assert.Empty(dbContext.CSGOMatchMaps);
		}

		#endregion

		#region StarCraft II matches

		[Fact]
		public async Task SubmitSC2Match_New_SavesMatchWithMapsAndLoadsPlayers()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			dbContext.Users.Add(new ApplicationUser { Id = "p1", UserName = "PlayerOne" });
			dbContext.Users.Add(new ApplicationUser { Id = "p2", UserName = "PlayerTwo" });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var match = new TournamentSC2Match
			{
				Player1Id = "p1", Player2Id = "p2", Player1Points = 1, Player2Points = 2,
				Maps = new List<SC2MatchMap>
				{
					new SC2MatchMap { Map = SC2Map.EverDreamLE, WinnerId = "p2" }
				}
			};
			var result = await controller.SubmitSC2Match(null, match);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var returned = Assert.IsType<TournamentSC2Match>(okResult.Value);
			Assert.Equal("PlayerOne", returned.Player1!.UserName);
			Assert.Equal("PlayerTwo", returned.Player2!.UserName);
			Assert.Equal("p2", returned.WinnerPlayerId);
			var persisted = Assert.Single(dbContext.TournamentSC2Matches);
			var map = Assert.Single(dbContext.SC2MatchMaps);
			Assert.Equal(persisted.Id, map.Sc2MatchId);
			Assert.Equal("p2", map.WinnerId);
		}

		[Fact]
		public async Task SubmitSC2Match_Existing_UpdatesMatchAndMaps()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			var mapId = Guid.NewGuid();
			dbContext.TournamentSC2Matches.Add(new TournamentSC2Match { Id = matchId, Player1Id = "p1", Player2Id = "p2" });
			dbContext.SC2MatchMaps.Add(new SC2MatchMap { Id = mapId, Sc2MatchId = matchId, Map = SC2Map.EverDreamLE });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var update = new TournamentSC2Match
			{
				Id = matchId, Player1Id = "p1", Player2Id = "p2", Player1Points = 2, Player2Points = 0,
				Player1Race = SC2Race.Zerg, VideoLink = "https://vod",
				Maps = new List<SC2MatchMap>
				{
					new SC2MatchMap { Id = mapId, Sc2MatchId = matchId, Map = SC2Map.EverDreamLE, WinnerId = "p1" },
					new SC2MatchMap { Sc2MatchId = matchId, Map = SC2Map.GoldenWallLE, WinnerId = "p1" }
				}
			};
			var result = await controller.SubmitSC2Match(matchId, update);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(matchId, Assert.IsType<TournamentSC2Match>(okResult.Value).Id);
			var persisted = Assert.Single(dbContext.TournamentSC2Matches);
			Assert.Equal(2, persisted.Player1Points);
			Assert.Equal(SC2Race.Zerg, persisted.Player1Race);
			Assert.Equal("https://vod", persisted.VideoLink);
			Assert.Equal(2, dbContext.SC2MatchMaps.Count());
			Assert.Equal("p1", dbContext.SC2MatchMaps.Find(mapId)!.WinnerId);
		}

		[Fact]
		public async Task DeleteSC2Match_Found_RemovesMatch()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			dbContext.TournamentSC2Matches.Add(new TournamentSC2Match { Id = matchId, Player1Id = "p1", Player2Id = "p2" });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.DeleteSC2Match(matchId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(matchId, Assert.IsType<TournamentSC2Match>(okResult.Value).Id);
			Assert.Empty(dbContext.TournamentSC2Matches);
		}

		[Fact]
		public async Task SubmitSC2MatchMap_New_AddsMap()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitSC2MatchMap(null, new SC2MatchMap { Sc2MatchId = matchId, Map = SC2Map.EverDreamLE, PlayerPickId = "p1" });

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(SC2Map.EverDreamLE, Assert.IsType<SC2MatchMap>(okResult.Value).Map);
			var persisted = Assert.Single(dbContext.SC2MatchMaps);
			Assert.Equal(matchId, persisted.Sc2MatchId);
			Assert.Equal("p1", persisted.PlayerPickId);
		}

		[Fact]
		public async Task SubmitSC2MatchMap_Existing_UpdatesMap()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var matchId = Guid.NewGuid();
			var mapId = Guid.NewGuid();
			dbContext.SC2MatchMaps.Add(new SC2MatchMap { Id = mapId, Sc2MatchId = matchId, Map = SC2Map.EverDreamLE });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitSC2MatchMap(mapId, new SC2MatchMap
			{
				Id = mapId, Sc2MatchId = matchId, Map = SC2Map.GoldenWallLE, WinnerId = "p2", PlayerBanId = "p1"
			});

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.SC2MatchMaps);
			Assert.Equal(SC2Map.GoldenWallLE, persisted.Map);
			Assert.Equal("p2", persisted.WinnerId);
			Assert.Equal("p1", persisted.PlayerBanId);
		}

		[Fact]
		public async Task DeleteSC2MatchMap_Found_RemovesMap()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var mapId = Guid.NewGuid();
			dbContext.SC2MatchMaps.Add(new SC2MatchMap { Id = mapId, Sc2MatchId = Guid.NewGuid(), Map = SC2Map.EverDreamLE });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.DeleteSC2MatchMap(mapId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(mapId, Assert.IsType<SC2MatchMap>(okResult.Value).Id);
			Assert.Empty(dbContext.SC2MatchMaps);
		}

		#endregion

		#region Registration hardening

		private static Guid SeedRegistration(BellumGensDbContext dbContext, Guid tournamentId, string ownerId)
		{
			var appId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "old#1",
				Email = "a@b.com", UserId = ownerId, Hash = "abc12345", State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();
			return appId;
		}

		[Fact]
		public async Task RegisterForBGE_Update_ReturnsBadRequest_WhenUserIsNotOwnerOrAdmin()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var appId = SeedRegistration(dbContext, tournamentId, "owner1");
			SetupAuthUser("user2");
			var controller = CreateAuthenticatedController(dbContext, "user2");

			var result = await controller.RegisterForBGE(tournamentId, new TournamentApplication
			{
				Id = appId, TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "hijack#1",
				Email = "evil@b.com", UserId = "user2", State = TournamentApplicationState.Confirmed
			});

			Assert.IsType<BadRequestObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentApplications);
			Assert.Equal("owner1", persisted.UserId);
			Assert.Equal("old#1", persisted.BattleNetId);
			Assert.Equal(TournamentApplicationState.Pending, persisted.State);
		}

		[Fact]
		public async Task RegisterForBGE_Update_ByOwner_KeepsServerControlledFields()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var appId = SeedRegistration(dbContext, tournamentId, "owner1");
			var submitted = dbContext.TournamentApplications.Find(appId)!.DateSubmitted;
			SetupAuthUser("owner1");
			var controller = CreateAuthenticatedController(dbContext, "owner1");

			var result = await controller.RegisterForBGE(tournamentId, new TournamentApplication
			{
				Id = appId, TournamentId = Guid.NewGuid(), Game = Game.StarCraft2, BattleNetId = "new#2",
				Email = "a@b.com", UserId = "someoneelse", Hash = "zzzzzzzz",
				State = TournamentApplicationState.Confirmed, DateSubmitted = submitted.AddDays(-30)
			});

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentApplications);
			Assert.Equal("new#2", persisted.BattleNetId);
			Assert.Equal("owner1", persisted.UserId);
			Assert.Equal("abc12345", persisted.Hash);
			Assert.Equal(tournamentId, persisted.TournamentId);
			Assert.Equal(submitted, persisted.DateSubmitted);
			Assert.Equal(TournamentApplicationState.Pending, persisted.State);
		}

		[Theory]
		[InlineData("admin")]
		[InlineData("event-admin")]
		public async Task RegisterForBGE_Update_ByTournamentAdmin_CanChangeStateButNotOwner(string role)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var appId = SeedRegistration(dbContext, tournamentId, "owner1");
			SetupAuthUser("admin1", role);
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.RegisterForBGE(tournamentId, new TournamentApplication
			{
				Id = appId, TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "old#1",
				Email = "a@b.com", UserId = "admin1", State = TournamentApplicationState.Confirmed
			});

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentApplications);
			Assert.Equal(TournamentApplicationState.Confirmed, persisted.State);
			Assert.Equal("owner1", persisted.UserId);
		}

		[Fact]
		public async Task RegisterForBGE_NewRegistration_IgnoresClientSuppliedState()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly", Active = true });
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.RegisterForBGE(null, new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				Email = NoSendEmail, State = TournamentApplicationState.Confirmed
			});

			Assert.IsType<OkObjectResult>(result);
			Assert.Equal(TournamentApplicationState.Pending, Assert.Single(dbContext.TournamentApplications).State);
		}

		[Fact]
		public async Task Register_WithoutCompany_IgnoresClientSuppliedState_AndCreatesNoCompany()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "EBL", Active = true });
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1",
				CompanyId = null, Email = NoSendEmail, State = TournamentApplicationState.Confirmed
			});

			Assert.IsType<OkObjectResult>(result);
			Assert.Equal(TournamentApplicationState.Pending, Assert.Single(dbContext.TournamentApplications).State);
			Assert.Empty(dbContext.Companies);
		}

		[Fact]
		public async Task Register_CSGO_NullTeamId_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = null, CompanyId = "Acme", Email = NoSendEmail
			});

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TournamentApplications);
		}

		[Fact]
		public async Task WeeklyCheckin_UnknownApplication_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.WeeklyCheckin(Guid.NewGuid());

			Assert.IsType<NotFoundResult>(result);
		}

		[Theory]
		[InlineData(nameof(TournamentController.DeleteCSGOMatch))]
		[InlineData(nameof(TournamentController.DeleteCSGOMatchMap))]
		[InlineData(nameof(TournamentController.DeleteSC2Match))]
		[InlineData(nameof(TournamentController.DeleteSC2MatchMap))]
		public async Task DeleteMatchOrMap_UnknownId_ReturnsNotFound(string action)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAuthenticatedController(dbContext, "admin1");
			var id = Guid.NewGuid();

			var result = action switch
			{
				nameof(TournamentController.DeleteCSGOMatch) => await controller.DeleteCSGOMatch(id),
				nameof(TournamentController.DeleteCSGOMatchMap) => await controller.DeleteCSGOMatchMap(id),
				nameof(TournamentController.DeleteSC2Match) => await controller.DeleteSC2Match(id),
				_ => await controller.DeleteSC2MatchMap(id)
			};

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task SubmitCSGOGroup_New_ReturnsBadRequest_WhenNoActiveTournament()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			dbContext.Tournaments.Add(new Tournament { ID = Guid.NewGuid(), Name = "Old", Active = false });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitCSGOGroup(null, new TournamentCSGOGroup { Name = "Group A" });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TournamentCSGOGroups);
		}

		[Fact]
		public async Task SubmitSC2Group_New_ReturnsBadRequest_WhenNoActiveTournament()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitSC2Group(null, new TournamentSC2Group { Name = "Group A" });

			Assert.IsType<BadRequestObjectResult>(result);
			Assert.Empty(dbContext.TournamentSC2Groups);
		}

		#endregion

		#region Check-in notifications and null-safety

		private static IUrlHelper MockCheckinUrl(TournamentController controller)
		{
			var mockUrl = new Mock<IUrlHelper>();
			mockUrl.SetupGet(u => u.ActionContext).Returns(controller.ControllerContext);
			mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://test/api/tournament/checkin");
			return mockUrl.Object;
		}

		[Fact]
		public async Task SendCheckinEmails_SendsPushNotificationWithOwnSubscriptions_ToEveryNonBannedRegistration()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly" });
			dbContext.TournamentApplications.AddRange(
				new TournamentApplication { TournamentId = tournamentId, UserId = "u1", Email = NoSendEmail, Hash = "hash0001", State = TournamentApplicationState.Pending },
				new TournamentApplication { TournamentId = tournamentId, UserId = "u2", Email = NoSendEmail, Hash = "hash0002", State = TournamentApplicationState.Confirmed },
				new TournamentApplication { TournamentId = tournamentId, UserId = "u3", Email = NoSendEmail, Hash = "hash0003", State = TournamentApplicationState.Pending },
				new TournamentApplication { TournamentId = tournamentId, UserId = "nosubs", Email = NoSendEmail, Hash = "hash0004", State = TournamentApplicationState.Pending },
				new TournamentApplication { TournamentId = tournamentId, UserId = "banned", Email = NoSendEmail, Hash = "hash0005", State = TournamentApplicationState.Banned },
				new TournamentApplication { TournamentId = Guid.NewGuid(), UserId = "other", Email = NoSendEmail, Hash = "hash0006" });
			dbContext.BellumGensPushSubscriptions.AddRange(
				new BellumGensPushSubscription { UserId = "u1", Endpoint = "e-u1-a", P256dh = "p-u1-a", Auth = "a-u1-a" },
				new BellumGensPushSubscription { UserId = "u1", Endpoint = "e-u1-b", P256dh = "p-u1-b", Auth = "a-u1-b" },
				new BellumGensPushSubscription { UserId = "u2", Endpoint = "e-u2", P256dh = "p-u2", Auth = "a-u2" },
				new BellumGensPushSubscription { UserId = "u3", Endpoint = "e-u3", P256dh = "p-u3", Auth = "a-u3" },
				new BellumGensPushSubscription { UserId = "banned", Endpoint = "e-banned", P256dh = "p-banned", Auth = "a-banned" },
				new BellumGensPushSubscription { UserId = "other", Endpoint = "e-other", P256dh = "p-other", Auth = "a-other" });
			dbContext.SaveChanges();
			var pushed = new ConcurrentDictionary<string, (List<string> endpoints, string url)>();
			var duplicates = 0;
			_mockNotificationService
				.Setup(n => n.SendNotificationAsync(It.IsAny<List<BellumGensPushSubscription>>(), It.IsAny<TournamentApplication>(), It.IsAny<string>()))
				.Callback((List<BellumGensPushSubscription> subs, TournamentApplication app, string url) =>
				{
					if (!pushed.TryAdd(app.UserId, (subs.Select(s => s.Endpoint).OrderBy(e => e).ToList(), url)))
					{
						System.Threading.Interlocked.Increment(ref duplicates);
					}
				})
				.Returns(Task.CompletedTask);
			var controller = CreateAuthenticatedController(dbContext, "admin1");
			controller.Url = MockCheckinUrl(controller);

			var result = await controller.SendCheckinEmails(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var message = okResult.Value!.GetType().GetProperty("message")!.GetValue(okResult.Value) as string;
			Assert.Equal("4 emails sent", message);
			// Emails to NoSendEmail fail, but that must not stop the push notifications.
			Assert.Equal(0, duplicates);
			Assert.Equal(new[] { "u1", "u2", "u3" }, pushed.Keys.OrderBy(k => k).ToArray());
			Assert.Equal(new[] { "e-u1-a", "e-u1-b" }, pushed["u1"].endpoints);
			Assert.Equal(new[] { "e-u2" }, pushed["u2"].endpoints);
			Assert.Equal(new[] { "e-u3" }, pushed["u3"].endpoints);
			Assert.All(pushed.Values, p => Assert.Equal("https://test/api/tournament/checkin", p.url));
		}

		[Fact]
		public async Task SendCheckinEmails_PushFailureForOneRegistration_StillNotifiesTheOthers()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly" });
			dbContext.TournamentApplications.AddRange(
				new TournamentApplication { TournamentId = tournamentId, UserId = "u1", Email = NoSendEmail, Hash = "hash0001" },
				new TournamentApplication { TournamentId = tournamentId, UserId = "u2", Email = NoSendEmail, Hash = "hash0002" });
			dbContext.BellumGensPushSubscriptions.AddRange(
				new BellumGensPushSubscription { UserId = "u1", Endpoint = "e1", P256dh = "p1", Auth = "a1" },
				new BellumGensPushSubscription { UserId = "u2", Endpoint = "e2", P256dh = "p2", Auth = "a2" });
			dbContext.SaveChanges();
			var notified = new ConcurrentBag<string>();
			_mockNotificationService
				.Setup(n => n.SendNotificationAsync(It.IsAny<List<BellumGensPushSubscription>>(), It.IsAny<TournamentApplication>(), It.IsAny<string>()))
				.Returns((List<BellumGensPushSubscription> _, TournamentApplication app, string _) =>
				{
					if (app.UserId == "u1")
					{
						throw new InvalidOperationException("push failed");
					}
					notified.Add(app.UserId);
					return Task.CompletedTask;
				});
			var controller = CreateAuthenticatedController(dbContext, "admin1");
			controller.Url = MockCheckinUrl(controller);

			var result = await controller.SendCheckinEmails(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			Assert.Equal("2 emails sent", okResult.Value!.GetType().GetProperty("message")!.GetValue(okResult.Value) as string);
			Assert.Equal("u2", Assert.Single(notified));
		}

		[Fact]
		public void TournamentSC2Match_Maps_DefaultsToEmptyCollection()
		{
			var match = new TournamentSC2Match();

			Assert.NotNull(match.Maps);
			Assert.Empty(match.Maps);
		}

		[Fact]
		public async Task SubmitSC2Match_WithoutMaps_SavesMatch()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitSC2Match(null, new TournamentSC2Match { Player1Id = "p1", Player2Id = "p2", Player1Points = 1 });

			Assert.IsType<OkObjectResult>(result);
			Assert.Single(dbContext.TournamentSC2Matches);
			Assert.Empty(dbContext.SC2MatchMaps);
		}

		[Fact]
		public async Task SubmitSC2Match_NullMaps_SavesMatch()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitSC2Match(null, new TournamentSC2Match { Player1Id = "p1", Player2Id = "p2", Maps = null! });

			Assert.IsType<OkObjectResult>(result);
			Assert.Single(dbContext.TournamentSC2Matches);
		}

		[Fact]
		public async Task SubmitCSGOMatch_NullMaps_SavesMatch()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitCSGOMatch(null, new TournamentCSGOMatch { Team1Id = Guid.NewGuid(), Team2Id = Guid.NewGuid(), Maps = null! });

			Assert.IsType<OkObjectResult>(result);
			Assert.Single(dbContext.TournamentCSGOMatches);
		}

		[Fact]
		public async Task DeleteRegistraion_Admin_UnknownId_ReturnsNotFound()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("admin1", "admin");
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.DeleteRegistraion(Guid.NewGuid());

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public async Task RegisterForBGE_NewRegistration_UnknownTournament_ReturnsBadRequest()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.RegisterForBGE(null, new TournamentApplication
			{
				TournamentId = Guid.NewGuid(), Game = Game.StarCraft2, BattleNetId = "player#1",
				CompanyId = "Acme", Email = NoSendEmail
			});

			var badResult = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Equal("Tournament not found.", badResult.Value);
			Assert.Empty(dbContext.TournamentApplications);
			Assert.Empty(dbContext.Companies);
		}

		// An authenticated principal whose account no longer exists (FindByIdAsync returns null).
		[Theory]
		[InlineData(nameof(TournamentController.Register))]
		[InlineData(nameof(TournamentController.RegisterForBGE) + "Create")]
		[InlineData(nameof(TournamentController.RegisterForBGE) + "Update")]
		[InlineData(nameof(TournamentController.GetUserRegistrations))]
		[InlineData(nameof(TournamentController.WeeklyCheckin))]
		[InlineData(nameof(TournamentController.DeleteRegistraion))]
		public async Task UserEndpoints_UnknownAuthenticatedUser_ReturnUnauthorized(string action)
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var appId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly", Active = true });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = tournamentId, UserId = "owner", Game = Game.StarCraft2,
				BattleNetId = "owner#1", Email = NoSendEmail, Hash = "hash0001", State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "ghost");
			var application = new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = Guid.NewGuid(), BattleNetId = "ghost#1",
				CompanyId = "Acme", Email = NoSendEmail
			};

			var result = action switch
			{
				nameof(TournamentController.Register) => await controller.Register(application),
				nameof(TournamentController.RegisterForBGE) + "Create" => await controller.RegisterForBGE(null, application),
				nameof(TournamentController.RegisterForBGE) + "Update" => await controller.RegisterForBGE(tournamentId, new TournamentApplication { Id = appId, TournamentId = tournamentId, Email = NoSendEmail }),
				nameof(TournamentController.GetUserRegistrations) => await controller.GetUserRegistrations(),
				nameof(TournamentController.WeeklyCheckin) => await controller.WeeklyCheckin(appId),
				_ => await controller.DeleteRegistraion(appId)
			};

			Assert.IsType<UnauthorizedResult>(result);
			var persisted = Assert.Single(dbContext.TournamentApplications);
			Assert.Equal(TournamentApplicationState.Pending, persisted.State);
			Assert.Equal("owner", persisted.UserId);
		}

		[Fact]
		public async Task GetRegistrationForTournament_UnknownAuthenticatedUser_ReturnsNull()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication { TournamentId = tournamentId, UserId = "owner", Email = NoSendEmail });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "ghost");

			var result = await controller.GetRegistrationForTournament(tournamentId);

			Assert.Null(result);
		}

		[Fact]
		public async Task GetSC2Registrations_SkipsRegistrationsWithoutUser()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "SC2Tourney", Active = true });
			dbContext.Users.Add(new ApplicationUser { Id = "sc2player", UserName = "sc2user" });
			dbContext.TournamentApplications.AddRange(
				new TournamentApplication { TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1", Email = "a@b.com", UserId = "sc2player" },
				new TournamentApplication { TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "orphan#1", Email = "c@d.com", UserId = null });
			dbContext.SaveChanges();
			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetSC2sRegistrations(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var registrations = Assert.IsAssignableFrom<List<TournamentSC2Participant>>(okResult.Value);
			Assert.Equal("player#1", Assert.Single(registrations).BattleTag);
		}

		[Fact]
		public async Task GetCSGORegistrations_ComputesRoundDifferenceFromMatchMaps()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			var teamId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "CSGOTourney", Active = true });
			dbContext.CSGOTeams.Add(new CSGOTeam { TeamId = teamId, TeamName = "Team1", CustomUrl = "team-1", SteamGroupId = "sg1" });
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.CSGO, TeamId = teamId, Email = "a@b.com", UserId = "u1"
			});
			var match = new TournamentCSGOMatch { TournamentId = tournamentId, Team1Id = teamId, Team2Id = Guid.NewGuid(), Team1Points = 2, Team2Points = 1 };
			match.Maps.Add(new CSGOMatchMap { Map = CSGOMap.Dust2, Team1Score = 16, Team2Score = 10 });
			match.Maps.Add(new CSGOMatchMap { Map = CSGOMap.Inferno, Team1Score = 12, Team2Score = 16 });
			dbContext.TournamentCSGOMatches.Add(match);
			dbContext.SaveChanges();
			// Simulate a fresh request: nothing is tracked, so maps are only there if the query loads them.
			dbContext.ChangeTracker.Clear();
			var controller = CreateController(dbContext);
			TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

			var result = await controller.GetCSGORegistrations(tournamentId);

			var okResult = Assert.IsType<OkObjectResult>(result);
			var participant = Assert.Single(Assert.IsAssignableFrom<List<TournamentCSGOParticipant>>(okResult.Value));
			Assert.Equal(2, participant.TeamPoints);
			Assert.Equal(1, participant.Wins);
			Assert.Equal(2, participant.RoundDifference);
		}

		#endregion

		#region Upsert key pinning

		[Fact]
		public async Task ConfirmRegistration_KeepsIdentityFields_WhileUpdatingState()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var appId = Guid.NewGuid();
			var tournamentId = Guid.NewGuid();
			dbContext.TournamentApplications.Add(new TournamentApplication
			{
				Id = appId, TournamentId = tournamentId, UserId = "u1", Hash = "abc12345",
				Game = Game.StarCraft2, BattleNetId = "p#1", Email = "a@b.com", State = TournamentApplicationState.Pending
			});
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.ConfirmRegistration(appId, new TournamentApplication
			{
				Game = Game.StarCraft2, BattleNetId = "p#1", Email = "a@b.com", State = TournamentApplicationState.Confirmed
			});

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentApplications);
			Assert.Equal(TournamentApplicationState.Confirmed, persisted.State);
			Assert.Equal("u1", persisted.UserId);
			Assert.Equal("abc12345", persisted.Hash);
			Assert.Equal(tournamentId, persisted.TournamentId);
		}

		[Fact]
		public async Task SubmitCSGOGroup_Existing_UpdatesEntity_WhenBodyIdIsMissing()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var groupId = Guid.NewGuid();
			var tournamentId = Guid.NewGuid();
			dbContext.TournamentCSGOGroups.Add(new TournamentCSGOGroup { Id = groupId, Name = "Old", TournamentId = tournamentId });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitCSGOGroup(groupId, new TournamentCSGOGroup { Name = "Renamed", TournamentId = tournamentId });

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.TournamentCSGOGroups);
			Assert.Equal(groupId, persisted.Id);
			Assert.Equal("Renamed", persisted.Name);
		}

		[Fact]
		public async Task SubmitSC2MatchMap_Existing_UpdatesEntity_WhenBodyIdDiffers()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var mapId = Guid.NewGuid();
			var matchId = Guid.NewGuid();
			dbContext.SC2MatchMaps.Add(new SC2MatchMap { Id = mapId, Sc2MatchId = matchId });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");

			var result = await controller.SubmitSC2MatchMap(mapId, new SC2MatchMap { Id = Guid.NewGuid(), Sc2MatchId = matchId, WinnerId = "p1" });

			Assert.IsType<OkObjectResult>(result);
			var persisted = Assert.Single(dbContext.SC2MatchMaps);
			Assert.Equal(mapId, persisted.Id);
			Assert.Equal("p1", persisted.WinnerId);
		}

		#endregion

		#region Emails

		[Fact]
		public async Task SendCheckinEmails_EmailsEveryNonBannedRegistrationWithItsCheckinLink()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly" });
			dbContext.TournamentApplications.AddRange(
				new TournamentApplication { TournamentId = tournamentId, UserId = "u1", Email = "u1@example.com", Hash = "hash0001", State = TournamentApplicationState.Pending },
				new TournamentApplication { TournamentId = tournamentId, UserId = "u2", Email = "u2@example.com", Hash = "hash0002", State = TournamentApplicationState.Confirmed },
				new TournamentApplication { TournamentId = tournamentId, UserId = "u3", Email = "u3@example.com", Hash = "hash0003", State = TournamentApplicationState.Banned },
				new TournamentApplication { TournamentId = Guid.NewGuid(), UserId = "u4", Email = "u4@example.com", Hash = "hash0004" });
			dbContext.SaveChanges();
			var controller = CreateAuthenticatedController(dbContext, "admin1");
			var mockUrl = new Mock<IUrlHelper>();
			mockUrl.SetupGet(u => u.ActionContext).Returns(controller.ControllerContext);
			mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://test/api/tournament/checkin");
			controller.Url = mockUrl.Object;

			await controller.SendCheckinEmails(tournamentId);

			_mockEmailService.Verify(e => e.SendEmailAsync("u1@example.com", It.IsAny<string>(), It.Is<string>(b => b.Contains("https://test/api/tournament/checkin"))), Times.Once);
			_mockEmailService.Verify(e => e.SendEmailAsync("u2@example.com", It.IsAny<string>(), It.Is<string>(b => b.Contains("https://test/api/tournament/checkin"))), Times.Once);
			_mockEmailService.Verify(e => e.SendEmailAsync("u3@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
			_mockEmailService.Verify(e => e.SendEmailAsync("u4@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[Fact]
		public async Task Register_SC2_Success_EmailsRegistrationConfirmation()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "EBL", Active = true });
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.Register(new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1", Email = "player1@example.com"
			});

			Assert.IsType<OkObjectResult>(result);
			_mockEmailService.Verify(e => e.SendEmailAsync("player1@example.com", It.IsAny<string>(), It.Is<string>(b => b.Contains("player#1"))), Times.Once);
		}

		[Fact]
		public async Task RegisterForBGE_NewRegistration_EmailsRegistrationConfirmation()
		{
			using var dbContext = TestUtils.CreateInMemoryDbContext();
			var tournamentId = Guid.NewGuid();
			dbContext.Tournaments.Add(new Tournament { ID = tournamentId, Name = "BGE Weekly", Active = true });
			dbContext.SaveChanges();
			SetupAuthUser("user1");
			var controller = CreateAuthenticatedController(dbContext, "user1");

			var result = await controller.RegisterForBGE(null, new TournamentApplication
			{
				TournamentId = tournamentId, Game = Game.StarCraft2, BattleNetId = "player#1", Email = "player1@example.com"
			});

			Assert.IsType<OkObjectResult>(result);
			_mockEmailService.Verify(e => e.SendEmailAsync("player1@example.com", It.IsAny<string>(), It.Is<string>(b => b.Contains("BGE Weekly"))), Times.Once);
		}

		#endregion
	}
}
