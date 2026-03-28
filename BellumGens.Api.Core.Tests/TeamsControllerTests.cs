using System;
using System.Collections.Generic;
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
    }
}
