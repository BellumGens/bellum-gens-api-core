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
    }
}
