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
    }
}
