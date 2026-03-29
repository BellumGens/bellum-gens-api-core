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
    public class TournamentControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly EmailServiceProvider _emailService;
        private readonly AppConfiguration _appConfig;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<AccountController>> _mockLogger;

        public TournamentControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _emailService = TestUtils.CreateMockEmailServiceProvider();
            _appConfig = TestUtils.CreateAppConfiguration();
            _mockNotificationService = TestUtils.CreateMockNotificationService();
            _mockLogger = TestUtils.CreateMockLogger<AccountController>();
        }

        private TournamentController CreateController(BellumGensDbContext dbContext)
        {
            return new TournamentController(
                _appConfig, _mockNotificationService.Object,
                _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
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
    }
}
