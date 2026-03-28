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
    public class StrategyControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly EmailServiceProvider _emailService;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<StrategyController>> _mockLogger;

        public StrategyControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _emailService = TestUtils.CreateMockEmailServiceProvider();
            _mockStorageService = TestUtils.CreateMockStorageService();
            _mockNotificationService = TestUtils.CreateMockNotificationService();
            _mockLogger = TestUtils.CreateMockLogger<StrategyController>();
        }

        private StrategyController CreateController(BellumGensDbContext dbContext)
        {
            return new StrategyController(
                _mockStorageService.Object, _mockNotificationService.Object,
                _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _emailService, dbContext, _mockLogger.Object);
        }

        [Fact]
        public async Task GetStrategies_ReturnsPaginatedVisibleStrategies()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "Strat1", Visible = true, Url = "http://example.com/strat1",
                CustomUrl = "strat-1", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "Strat2", Visible = true, Url = "http://example.com/strat2",
                CustomUrl = "strat-2", Side = Side.CTSide, Map = CSGOMap.Inferno
            });
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "Strat3", Visible = false, Url = "http://example.com/strat3",
                CustomUrl = "strat-3", Side = Side.TSide, Map = CSGOMap.Mirage
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.GetStrategies(0);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var strategies = Assert.IsAssignableFrom<IEnumerable<CSGOStrategy>>(okResult.Value);
            Assert.Equal(2, new List<CSGOStrategy>(strategies).Count);
        }

        [Fact]
        public async Task GetStrat_ReturnsStrategy_WhenFoundById()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "TestStrat", Visible = true,
                CustomUrl = "test-strat", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.GetStrat(stratId.ToString());

            var okResult = Assert.IsType<OkObjectResult>(result);
            var strategy = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.Equal("TestStrat", strategy.Title);
        }

        [Fact]
        public async Task GetStrat_ReturnsBadRequest_WhenNotFound()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.GetStrat(Guid.NewGuid().ToString());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SubmitStrategyVote_CreatesNewVote()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var stratId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "VoteStrat", Visible = true,
                CustomUrl = "vote-strat", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var voteModel = new VoteModel { id = stratId, direction = VoteDirection.Up };
            var result = await controller.SubmitStrategyVote(voteModel);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var vote = Assert.IsType<StrategyVote>(okResult.Value);
            Assert.Equal(stratId, vote.StratId);
            Assert.Equal(userId, vote.UserId);
            Assert.Equal(VoteDirection.Up, vote.Vote);
        }

        [Fact]
        public async Task SubmitStrategyComment_CreatesNewComment()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var stratId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "CommentStrat", Visible = true,
                CustomUrl = "comment-strat", UserId = "otheruser",
                Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var comment = new StrategyComment
            {
                StratId = stratId,
                UserId = userId,
                Comment = "Great strategy!"
            };
            var result = await controller.SubmitStrategyComment(comment);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var savedComment = Assert.IsType<StrategyComment>(okResult.Value);
            Assert.Equal("Great strategy!", savedComment.Comment);
            Assert.Equal(stratId, savedComment.StratId);
        }

        [Fact]
        public async Task GetStrategies_ReturnsEmpty_WhenNoVisibleStrategies()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "HiddenStrat", Visible = false, Url = "http://example.com/hidden",
                CustomUrl = "hidden-strat", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.GetStrategies(0);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var strategies = Assert.IsAssignableFrom<IEnumerable<CSGOStrategy>>(okResult.Value);
            Assert.Empty(strategies);
        }

        [Fact]
        public async Task GetStrat_ReturnsStrategy_ByCustomUrl()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "CustomUrlStrat", Visible = true,
                CustomUrl = "my-custom-url", Side = Side.CTSide, Map = CSGOMap.Inferno
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateUnauthenticatedUser());

            var result = await controller.GetStrat("my-custom-url");

            var okResult = Assert.IsType<OkObjectResult>(result);
            var strategy = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.Equal("CustomUrlStrat", strategy.Title);
            Assert.Equal("my-custom-url", strategy.CustomUrl);
        }

        [Fact]
        public async Task SubmitStrategyVote_TogglesSameDirection_RemovesVote()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var stratId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "ToggleVoteStrat", Visible = true,
                CustomUrl = "toggle-vote", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.StrategyVotes.Add(new StrategyVote
            {
                StratId = stratId, UserId = userId, Vote = VoteDirection.Up
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var voteModel = new VoteModel { id = stratId, direction = VoteDirection.Up };
            var result = await controller.SubmitStrategyVote(voteModel);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Null(okResult.Value);
        }

        [Fact]
        public async Task SubmitStrategyComment_UpdatesExistingComment()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var stratId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "UpdateCommentStrat", Visible = true,
                CustomUrl = "update-comment", UserId = userId,
                Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.StrategyComments.Add(new StrategyComment
            {
                Id = commentId, StratId = stratId, UserId = userId, Comment = "Original comment"
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var updatedComment = new StrategyComment
            {
                Id = commentId, StratId = stratId, UserId = userId, Comment = "Updated comment"
            };
            var result = await controller.SubmitStrategyComment(updatedComment);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var savedComment = Assert.IsType<StrategyComment>(okResult.Value);
            Assert.Equal("Updated comment", savedComment.Comment);
        }
    }
}
