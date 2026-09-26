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
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    public class SearchControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ISteamService> _mockSteamService;
        private readonly Mock<ILogger<SearchController>> _mockLogger;

        public SearchControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _mockEmailService = TestUtils.CreateMockEmailService();
            _mockSteamService = TestUtils.CreateMockSteamService();
            _mockLogger = TestUtils.CreateMockLogger<SearchController>();
        }

        private SearchController CreateController(BellumGensDbContext dbContext)
        {
            return new SearchController(
                _mockSteamService.Object, _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
        }

        private static DateTimeOffset At(int hour)
        {
            return new DateTimeOffset(new DateTime(2018, 1, 15, hour, 0, 0, DateTimeKind.Utc));
        }

        private static TeamAvailability TeamSlot(DayOfWeek day, int from, int to, bool available = true)
        {
            return new TeamAvailability { Day = day, From = At(from), To = At(to), Available = available };
        }

        private static UserAvailability UserSlot(DayOfWeek day, int from, int to, bool available = true)
        {
            return new UserAvailability { Day = day, From = At(from), To = At(to), Available = available };
        }

        private static CSGOTeam CreateTeam(string name, bool visible, params TeamAvailability[] schedule)
        {
            return new CSGOTeam
            {
                TeamName = name,
                CustomUrl = name.ToLowerInvariant(),
                SteamGroupId = "sg-" + name,
                Visible = visible,
                PracticeSchedule = new List<TeamAvailability>(schedule)
            };
        }

        private static ApplicationUser CreatePlayer(string id, PlaystyleRole primary, PlaystyleRole secondary,
            bool searchVisible = true, params UserAvailability[] availability)
        {
            return new ApplicationUser
            {
                Id = id,
                UserName = id,
                SteamID = "steam-" + id,
                SearchVisible = searchVisible,
                CSGODetails = new CSGODetails
                {
                    SteamId = "steam-" + id,
                    PreferredPrimaryRole = primary,
                    PreferredSecondaryRole = secondary
                },
                Availability = new List<UserAvailability>(availability)
            };
        }

        [Fact]
        public async Task Get_WithEmptyName_ReturnsEmptyResults()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = new SearchController(
                _mockSteamService.Object, _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
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
                _mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
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
                _mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
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
                _mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.SearchPlayers(null, 0, null);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsAssignableFrom<System.Collections.Generic.List<UserStatsViewModel>>(okResult.Value);
            Assert.Equal(2, players.Count);
        }

        [Fact]
        public async Task SearchTeams_WithOverlapNotAuthenticated_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.CSGOTeams.Add(new CSGOTeam { TeamName = "Team1", Visible = true, CustomUrl = "t1", SteamGroupId = "sg1" });
            dbContext.SaveChanges();

            var controller = new SearchController(
                _mockSteamService.Object, _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.SearchTeams(null, 1.0);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("sign in", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SearchTeams_WithRole_ReturnsVisibleTeamsMissingRoleWithAvailablePractice()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var needsAwper = CreateTeam("NeedsAwper", true, TeamSlot(DayOfWeek.Monday, 18, 22));
            needsAwper.Members.Add(new TeamMember { UserId = "m1", Role = PlaystyleRole.IGL, IsActive = true });
            var hasAwper = CreateTeam("HasAwper", true, TeamSlot(DayOfWeek.Monday, 18, 22));
            hasAwper.Members.Add(new TeamMember { UserId = "m2", Role = PlaystyleRole.Awper, IsActive = true });
            var noPractice = CreateTeam("NoPractice", true, TeamSlot(DayOfWeek.Monday, 18, 22, available: false));
            var hidden = CreateTeam("Hidden", false, TeamSlot(DayOfWeek.Monday, 18, 22));
            dbContext.CSGOTeams.AddRange(needsAwper, hasAwper, noPractice, hidden);
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.SearchTeams(PlaystyleRole.Awper, 0);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var teams = Assert.IsType<List<CSGOTeam>>(okResult.Value);
            var team = Assert.Single(teams);
            Assert.Equal("NeedsAwper", team.TeamName);
            _mockUserManager.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SearchTeams_WithOverlap_WhenUserHasNoAvailableDays_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 22, available: false));
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(CreateTeam("Team1", true, TeamSlot(DayOfWeek.Monday, 18, 22)));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchTeams(null, 2);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("availability", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SearchTeams_WithOverlap_ReturnsOnlyTeamsWithSufficientOverlap()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 22));
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.AddRange(
                // Exactly the same window: 4h overlap
                CreateTeam("FullMatch", true, TeamSlot(DayOfWeek.Monday, 18, 22)),
                // Team starts earlier and ends inside the user's window: 3h overlap
                CreateTeam("EarlyOverlap", true, TeamSlot(DayOfWeek.Monday, 16, 21)),
                // Fully inside the user's window but only 1h long
                CreateTeam("ShortPractice", true, TeamSlot(DayOfWeek.Monday, 19, 20)),
                // Enough hours, but on a different day
                CreateTeam("OtherDay", true, TeamSlot(DayOfWeek.Tuesday, 18, 22)),
                // Matching window but not marked available
                CreateTeam("Unavailable", true, TeamSlot(DayOfWeek.Monday, 18, 22, available: false)),
                // Matching window but hidden
                CreateTeam("Hidden", false, TeamSlot(DayOfWeek.Monday, 18, 22)));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchTeams(null, 3);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var teams = Assert.IsAssignableFrom<IEnumerable<CSGOTeam>>(okResult.Value);
            Assert.Equal(new[] { "EarlyOverlap", "FullMatch" }, teams.Select(t => t.TeamName).OrderBy(n => n));
        }

        [Fact]
        public async Task SearchTeams_WithOverlapAboveUserAvailability_CapsOverlapToUserTotal()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 20));
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.AddRange(
                CreateTeam("CoversUser", true, TeamSlot(DayOfWeek.Monday, 18, 20)),
                CreateTeam("HalfOverlap", true, TeamSlot(DayOfWeek.Monday, 18, 19)));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            // User only has 2h available, so the requested 10h overlap is capped to 2h
            var result = await controller.SearchTeams(null, 10);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var teams = Assert.IsAssignableFrom<IEnumerable<CSGOTeam>>(okResult.Value);
            var team = Assert.Single(teams);
            Assert.Equal("CoversUser", team.TeamName);
        }

        [Fact]
        public async Task SearchTeams_WithRoleAndOverlap_AppliesBothFilters()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.Awper, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 22));
            dbContext.Users.Add(user);
            var match = CreateTeam("Match", true, TeamSlot(DayOfWeek.Monday, 18, 22));
            var hasAwper = CreateTeam("HasAwper", true, TeamSlot(DayOfWeek.Monday, 18, 22));
            hasAwper.Members.Add(new TeamMember { UserId = "m1", Role = PlaystyleRole.Awper, IsActive = true });
            var otherDay = CreateTeam("OtherDay", true, TeamSlot(DayOfWeek.Tuesday, 18, 22));
            dbContext.CSGOTeams.AddRange(match, hasAwper, otherDay);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchTeams(PlaystyleRole.Awper, 2);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var teams = Assert.IsAssignableFrom<IEnumerable<CSGOTeam>>(okResult.Value);
            var team = Assert.Single(teams);
            Assert.Equal("Match", team.TeamName);
        }

        [Fact]
        public async Task SearchPlayers_WithRole_ReturnsVisiblePlayersWithMatchingPrimaryOrSecondaryRole()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.Users.AddRange(
                CreatePlayer("primary", PlaystyleRole.Awper, PlaystyleRole.Support),
                CreatePlayer("secondary", PlaystyleRole.IGL, PlaystyleRole.Awper),
                CreatePlayer("other", PlaystyleRole.IGL, PlaystyleRole.Support),
                CreatePlayer("hidden", PlaystyleRole.Awper, PlaystyleRole.Awper, searchVisible: false));
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.SearchPlayers(PlaystyleRole.Awper, 0, null);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsType<List<UserStatsViewModel>>(okResult.Value);
            Assert.Equal(new[] { "primary", "secondary" }, players.Select(p => p.Id).OrderBy(id => id));
            Assert.All(players, p => Assert.NotNull(p.CSGODetails));
        }

        [Fact]
        public async Task SearchPlayers_WithOverlapNotAuthenticated_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.Users.Add(CreatePlayer("p1", PlaystyleRole.Awper, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 22)));
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.SearchPlayers(null, 2, null);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("sign in", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SearchPlayers_WithOverlap_WhenUserHasNoAvailableDays_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, false,
                UserSlot(DayOfWeek.Monday, 18, 22, available: false));
            dbContext.Users.Add(user);
            dbContext.Users.Add(CreatePlayer("p1", PlaystyleRole.Awper, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 22)));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchPlayers(null, 2, null);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("availability", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SearchPlayers_WithOverlap_ReturnsPlayersOverlappingAuthUser()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, false,
                UserSlot(DayOfWeek.Monday, 18, 22));
            dbContext.Users.Add(user);
            dbContext.Users.AddRange(
                // Same window: 4h overlap
                CreatePlayer("same", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 18, 22)),
                // Player window contains the user's window: 4h overlap
                CreatePlayer("wider", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 17, 23)),
                // 4h total, but only 2h overlapping (18-20)
                CreatePlayer("partial", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 16, 20)),
                // No overlap at all
                CreatePlayer("morning", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 8, 12)),
                // Matching window but not available
                CreatePlayer("unavailable", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 18, 22, available: false)),
                // Matching window but hidden from search
                CreatePlayer("hidden", PlaystyleRole.IGL, PlaystyleRole.NotSet, false, UserSlot(DayOfWeek.Monday, 18, 22)));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchPlayers(null, 3, null);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsType<List<UserStatsViewModel>>(okResult.Value);
            Assert.Equal(new[] { "same", "wider" }, players.Select(p => p.Id).OrderBy(id => id));
        }

        [Fact]
        public async Task SearchPlayers_WithOverlapAndTeamId_ReturnsPlayersOverlappingTeamSchedule()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var team = CreateTeam("Team1", true, TeamSlot(DayOfWeek.Monday, 18, 22));
            dbContext.CSGOTeams.Add(team);
            dbContext.Users.AddRange(
                CreatePlayer("match", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 18, 22)),
                CreatePlayer("otherday", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Tuesday, 18, 22)),
                CreatePlayer("short", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 19, 20)));
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchPlayers(null, 2, team.TeamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsType<List<UserStatsViewModel>>(okResult.Value);
            var player = Assert.Single(players);
            Assert.Equal("match", player.Id);
            // The team schedule is used instead of the authenticated user's availability
            _mockUserManager.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SearchPlayers_WithRoleAndOverlap_AppliesBothFilters()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, false,
                UserSlot(DayOfWeek.Monday, 18, 22));
            dbContext.Users.Add(user);
            dbContext.Users.AddRange(
                CreatePlayer("awper", PlaystyleRole.Awper, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 18, 22)),
                CreatePlayer("igl", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 18, 22)),
                CreatePlayer("awperotherday", PlaystyleRole.Awper, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Tuesday, 18, 22)),
                CreatePlayer("awpernoschedule", PlaystyleRole.NotSet, PlaystyleRole.Awper, true));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchPlayers(PlaystyleRole.Awper, 2, null);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsType<List<UserStatsViewModel>>(okResult.Value);
            var player = Assert.Single(players);
            Assert.Equal("awper", player.Id);
        }

        [Fact]
        public async Task SearchTeams_WithOverlap_TeamStartingAndEndingLater_UsesActualOverlap()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 22));
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.AddRange(
                // Overlap is 19-22 = 3h (previously miscounted as 5h)
                CreateTeam("LateOverlap", true, TeamSlot(DayOfWeek.Monday, 19, 23)),
                CreateTeam("FullMatch", true, TeamSlot(DayOfWeek.Monday, 18, 22)));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchTeams(null, 4);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var teams = Assert.IsAssignableFrom<IEnumerable<CSGOTeam>>(okResult.Value);
            var team = Assert.Single(teams);
            Assert.Equal("FullMatch", team.TeamName);
        }

        [Fact]
        public async Task SearchTeams_WithOverlapAboveUserAvailability_CapIgnoresUnavailableDays()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            // 2h available on Monday; the 10h on Tuesday is not available and must not raise the cap
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 20),
                UserSlot(DayOfWeek.Tuesday, 10, 20, available: false));
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(CreateTeam("CoversUser", true,
                TeamSlot(DayOfWeek.Monday, 18, 20),
                TeamSlot(DayOfWeek.Tuesday, 10, 20)));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchTeams(null, 10);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var teams = Assert.IsAssignableFrom<IEnumerable<CSGOTeam>>(okResult.Value);
            var team = Assert.Single(teams);
            Assert.Equal("CoversUser", team.TeamName);
        }

        [Fact]
        public async Task SearchPlayers_WithOverlap_PlayerStartingAndEndingLater_UsesActualOverlap()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var user = CreatePlayer("me", PlaystyleRole.NotSet, PlaystyleRole.NotSet, false,
                UserSlot(DayOfWeek.Monday, 18, 22));
            dbContext.Users.Add(user);
            dbContext.Users.AddRange(
                // Overlap is 19-22 = 3h (previously miscounted as 5h)
                CreatePlayer("late", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 19, 23)),
                CreatePlayer("same", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 18, 22)));
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync("me")).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchPlayers(null, 4, null);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsType<List<UserStatsViewModel>>(okResult.Value);
            var player = Assert.Single(players);
            Assert.Equal("same", player.Id);
        }

        [Fact]
        public async Task SearchPlayers_WithOverlapAndTeamId_TeamStartingAndEndingLater_UsesActualOverlap()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var team = CreateTeam("Team1", true, TeamSlot(DayOfWeek.Monday, 19, 23));
            dbContext.CSGOTeams.Add(team);
            dbContext.Users.AddRange(
                // Overlap with the team is 19-22 = 3h (previously miscounted as 5h)
                CreatePlayer("early", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 18, 22)),
                CreatePlayer("match", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 19, 23)));
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchPlayers(null, 4, team.TeamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsType<List<UserStatsViewModel>>(okResult.Value);
            var player = Assert.Single(players);
            Assert.Equal("match", player.Id);
        }

        [Fact]
        public async Task SearchPlayers_WithOverlapAndTeamId_CapIgnoresUnavailableDays()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            // 4h available on Monday; the 10h on Tuesday is not available and must not raise the cap
            var team = CreateTeam("Team1", true,
                TeamSlot(DayOfWeek.Monday, 18, 22),
                TeamSlot(DayOfWeek.Tuesday, 10, 20, available: false));
            dbContext.CSGOTeams.Add(team);
            dbContext.Users.AddRange(
                CreatePlayer("match", PlaystyleRole.IGL, PlaystyleRole.NotSet, true,
                    UserSlot(DayOfWeek.Monday, 18, 22), UserSlot(DayOfWeek.Tuesday, 10, 20)),
                CreatePlayer("partial", PlaystyleRole.IGL, PlaystyleRole.NotSet, true, UserSlot(DayOfWeek.Monday, 18, 20)));
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchPlayers(null, 10, team.TeamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var players = Assert.IsType<List<UserStatsViewModel>>(okResult.Value);
            var player = Assert.Single(players);
            Assert.Equal("match", player.Id);
        }

        [Fact]
        public async Task SearchPlayers_WithOverlapAndTeamId_WhenTeamHasNoAvailableDays_ReturnsBadRequest()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var team = CreateTeam("Team1", true, TeamSlot(DayOfWeek.Monday, 18, 22, available: false));
            dbContext.CSGOTeams.Add(team);
            dbContext.Users.Add(CreatePlayer("p1", PlaystyleRole.IGL, PlaystyleRole.NotSet, true,
                UserSlot(DayOfWeek.Monday, 18, 22)));
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("me"));

            var result = await controller.SearchPlayers(null, 2, team.TeamId);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("practice schedule", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Get_DoesNotReturnHiddenStrategies_WhenOnlyDescriptionMatches()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.CSGOStrategies.AddRange(
                new CSGOStrategy { Id = Guid.NewGuid(), Title = "Secret", Description = "rush alpha", Visible = false, CustomUrl = "secret" },
                new CSGOStrategy { Id = Guid.NewGuid(), Title = "Public", Description = "rush alpha", Visible = true, CustomUrl = "public" },
                new CSGOStrategy { Id = Guid.NewGuid(), Title = "Alpha Hidden", Description = "nothing", Visible = false, CustomUrl = "alpha-hidden" });
            dbContext.SaveChanges();
            _mockSteamService.Setup(s => s.GetSteamUserDetails(It.IsAny<string>())).ReturnsAsync(new UserStatsViewModel());

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.Get("alpha");

            var searchResult = Assert.IsType<SearchResultViewModel>(Assert.IsType<OkObjectResult>(result).Value);
            var strategy = Assert.Single(searchResult.Strategies);
            Assert.Equal("Public", strategy.Title);
        }
    }
}
