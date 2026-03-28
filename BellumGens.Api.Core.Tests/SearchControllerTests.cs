using System;
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
    public class SearchControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly EmailServiceProvider _emailService;
        private readonly Mock<ISteamService> _mockSteamService;
        private readonly Mock<ILogger<SearchController>> _mockLogger;

        public SearchControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _emailService = TestUtils.CreateMockEmailServiceProvider();
            _mockSteamService = TestUtils.CreateMockSteamService();
            _mockLogger = TestUtils.CreateMockLogger<SearchController>();
        }

        [Fact]
        public async Task Get_WithEmptyName_ReturnsEmptyResults()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = new SearchController(
                _mockSteamService.Object, _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.Get(null!);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var searchResult = Assert.IsType<SearchResultViewModel>(okResult.Value);
            Assert.Empty(searchResult.Teams);
            Assert.Empty(searchResult.Players);
        }

        [Fact]
        public async Task Get_WithName_ReturnsMatchingTeamsAndPlayers()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.CSGOTeams.Add(new CSGOTeam { TeamName = "AlphaTeam", Visible = true, CustomUrl = "alpha", SteamGroupId = "g1" });
            dbContext.CSGOTeams.Add(new CSGOTeam { TeamName = "BetaTeam", Visible = true, CustomUrl = "beta", SteamGroupId = "g2" });
            dbContext.Users.Add(new ApplicationUser { Id = "user1", UserName = "AlphaPlayer", SearchVisible = true });
            dbContext.SaveChanges();

            _mockSteamService.Setup(s => s.GetSteamUserDetails(It.IsAny<string>()))
                .ReturnsAsync(new UserStatsViewModel());

            var controller = new SearchController(
                _mockSteamService.Object, _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.Get("Alpha");

            var okResult = Assert.IsType<OkObjectResult>(result);
            var searchResult = Assert.IsType<SearchResultViewModel>(okResult.Value);
            Assert.Single(searchResult.Teams);
            Assert.Single(searchResult.Players);
        }

        [Fact]
        public async Task SearchTeams_NoFilters_ReturnsAllVisibleTeams()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.CSGOTeams.Add(new CSGOTeam { TeamName = "Team1", Visible = true, CustomUrl = "t1", SteamGroupId = "sg1" });
            dbContext.CSGOTeams.Add(new CSGOTeam { TeamName = "Team2", Visible = true, CustomUrl = "t2", SteamGroupId = "sg2" });
            dbContext.CSGOTeams.Add(new CSGOTeam { TeamName = "Team3", Visible = false, CustomUrl = "t3", SteamGroupId = "sg3" });
            dbContext.SaveChanges();

            var controller = new SearchController(
                _mockSteamService.Object, _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.SearchTeams(null, 0);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var teams = Assert.IsAssignableFrom<System.Collections.Generic.List<CSGOTeam>>(okResult.Value);
            Assert.Equal(2, teams.Count);
        }

        [Fact]
        public async Task SearchPlayers_NoFilters_ReturnsAllVisiblePlayers()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.Users.Add(new ApplicationUser { Id = "u1", UserName = "Player1", SearchVisible = true });
            dbContext.Users.Add(new ApplicationUser { Id = "u2", UserName = "Player2", SearchVisible = true });
            dbContext.Users.Add(new ApplicationUser { Id = "u3", UserName = "Player3", SearchVisible = false });
            dbContext.SaveChanges();

            var controller = new SearchController(
                _mockSteamService.Object, _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.SearchPlayers(null, 0, null);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsAssignableFrom<System.Collections.Generic.List<UserStatsViewModel>>(okResult.Value);
            Assert.Equal(2, players.Count);
        }
    }
}
