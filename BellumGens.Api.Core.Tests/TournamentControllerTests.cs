using System;
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
    }
}
