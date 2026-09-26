using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<StrategyController>> _mockLogger;

        public StrategyControllerTests()
        {
            _mockUserManager = TestUtils.CreateMockUserManager();
            _mockRoleManager = TestUtils.CreateMockRoleManager();
            _mockSignInManager = TestUtils.CreateMockSignInManager(_mockUserManager);
            _mockEmailService = TestUtils.CreateMockEmailService();
            _mockStorageService = TestUtils.CreateMockStorageService();
            _mockNotificationService = TestUtils.CreateMockNotificationService();
            _mockLogger = TestUtils.CreateMockLogger<StrategyController>();
        }

        private StrategyController CreateController(BellumGensDbContext dbContext)
        {
            return new StrategyController(
                _mockStorageService.Object, _mockNotificationService.Object,
                _mockUserManager.Object, _mockRoleManager.Object,
                _mockSignInManager.Object, _mockEmailService.Object, dbContext, _mockLogger.Object);
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

        [Fact]
        public async Task GetTeamStrats_ReturnsBadRequest_WhenNotMember()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var teamId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team1", CustomUrl = "team-1", SteamGroupId = "sg1"
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetTeamStrats(teamId);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTeamStrats_ReturnsStrats_WhenMember()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var teamId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team1", CustomUrl = "team-strats", SteamGroupId = "sg1"
            });
            dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId, UserId = userId, IsActive = true, IsAdmin = false
            });
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "TeamStrat", TeamId = teamId, Visible = false,
                CustomUrl = "team-strat-1", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetTeamStrats(teamId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var strats = Assert.IsAssignableFrom<IEnumerable<CSGOStrategy>>(okResult.Value);
            Assert.Single(strats);
        }

        [Fact]
        public async Task GetUserStrats_ReturnsStrats_ForOwnUser()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "MyStrat", UserId = userId, Visible = false,
                CustomUrl = "my-strat", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetUserStrats(userId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var strats = Assert.IsAssignableFrom<IEnumerable<CSGOStrategy>>(okResult.Value);
            Assert.Single(strats);
        }

        [Fact]
        public async Task GetUserStrats_ReturnsBadRequest_ForDifferentUser()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.GetUserStrats("otheruser");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SubmitStrategy_CreatesNewStrategy_WhenNoExistingEntity()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var strategy = new CSGOStrategy
            {
                Title = "NewStrat", Visible = true,
                Side = Side.TSide, Map = CSGOMap.Dust2
            };
            var result = await controller.SubmitStrategy(strategy);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var saved = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.Equal("NewStrat", saved.Title);
            Assert.Equal(userId, saved.UserId);
        }

        [Fact]
        public async Task DeleteStrategy_ReturnsOk_WhenUserOwnsStrategy()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var stratId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "ToDelete", UserId = userId, Visible = true,
                CustomUrl = "to-delete", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.DeleteStrategy(stratId);

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DeleteStrategy_ReturnsBadRequest_WhenUserCannotEdit()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var stratId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "NotMine", UserId = "otheruser", Visible = true,
                CustomUrl = "not-mine", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.DeleteStrategy(stratId);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteStrategyComment_ReturnsOk_WhenUserOwnsComment()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var commentId = Guid.NewGuid();
            var stratId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Strat", UserId = userId, Visible = true,
                CustomUrl = "del-comment-strat", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.StrategyComments.Add(new StrategyComment
            {
                Id = commentId, StratId = stratId, UserId = userId, Comment = "My comment"
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.DeleteStrategyComment(commentId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Null(await dbContext.StrategyComments.FindAsync([commentId], TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DeleteStrategyComment_ReturnsBadRequest_WhenWrongUser()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var commentId = Guid.NewGuid();
            var stratId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.StrategyComments.Add(new StrategyComment
            {
                Id = commentId, StratId = stratId, UserId = "otheruser", Comment = "Not mine"
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.DeleteStrategyComment(commentId);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteStrategyComment_ReturnsBadRequest_WhenCommentNotFound()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var result = await controller.DeleteStrategyComment(Guid.NewGuid());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SubmitStrategyVote_ChangesDirection_WhenExistingVoteDiffers()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            var stratId = Guid.NewGuid();
            dbContext.Users.Add(user);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "ChangeVoteStrat", Visible = true,
                CustomUrl = "change-vote", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.StrategyVotes.Add(new StrategyVote
            {
                StratId = stratId, UserId = userId, Vote = VoteDirection.Up
            });
            dbContext.SaveChanges();

            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var voteModel = new VoteModel { id = stratId, direction = VoteDirection.Down };
            var result = await controller.SubmitStrategyVote(voteModel);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var vote = Assert.IsType<StrategyVote>(okResult.Value);
            Assert.Equal(VoteDirection.Down, vote.Vote);
        }

        private ApplicationUser SeedAuthUser(BellumGensDbContext dbContext, string userId)
        {
            var user = new ApplicationUser { Id = userId, UserName = userId };
            dbContext.Users.Add(user);
            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            return user;
        }

        private static Guid SeedTeam(BellumGensDbContext dbContext, params TeamMember[] members)
        {
            var teamId = Guid.NewGuid();
            dbContext.CSGOTeams.Add(new CSGOTeam
            {
                TeamId = teamId, TeamName = "Team", CustomUrl = "team-" + teamId, SteamGroupId = "sg-" + teamId
            });
            foreach (var member in members)
            {
                member.TeamId = teamId;
                member.IsActive = true;
                dbContext.TeamMembers.Add(member);
            }
            return teamId;
        }

        [Fact]
        public async Task SubmitStrategy_ForTeam_ReturnsBadRequest_WhenUserIsMemberButNotEditor()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var teamId = SeedTeam(dbContext,
                new TeamMember { UserId = "user1", IsEditor = false, IsAdmin = false },
                new TeamMember { UserId = "admin", IsAdmin = true });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Title = "Team Strat", TeamId = teamId, Side = Side.TSide, Map = CSGOMap.Dust2
            });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("team editor", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await dbContext.CSGOStrategies.ToListAsync(TestContext.Current.CancellationToken));
            _mockStorageService.Verify(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SubmitStrategy_ForTeam_ReturnsBadRequest_WhenUserIsNotTeamMember()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var teamId = SeedTeam(dbContext, new TeamMember { UserId = "admin", IsAdmin = true });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Title = "Team Strat", TeamId = teamId, Side = Side.TSide, Map = CSGOMap.Dust2
            });

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(await dbContext.CSGOStrategies.ToListAsync(TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task SubmitStrategy_ForTeam_CreatesStrategy_WhenUserIsEditorOrAdmin(bool isEditor, bool isAdmin)
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var teamId = SeedTeam(dbContext, new TeamMember { UserId = "user1", IsEditor = isEditor, IsAdmin = isAdmin });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Title = "Team Execute", TeamId = teamId, Side = Side.CTSide, Map = CSGOMap.Mirage
            });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var saved = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.Equal("user1", saved.UserId);
            Assert.Equal(teamId, saved.TeamId);
            Assert.Equal("Team-Execute", saved.CustomUrl);

            var stored = Assert.Single(await dbContext.CSGOStrategies.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Team Execute", stored.Title);
            Assert.Equal(teamId, stored.TeamId);
            // No image was submitted, so there is nothing to upload
            _mockStorageService.Verify(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SubmitStrategy_Create_GeneratesUniqueCustomUrl_WhenTitleUrlIsTaken()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "Dust2 Split", UserId = "other", CustomUrl = "Dust2-Split",
                Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Title = "Dust2 Split", Side = Side.TSide, Map = CSGOMap.Dust2
            });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var saved = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.StartsWith("Dust2-Split-", saved.CustomUrl);
            Assert.Equal("Dust2-Split-".Length + 6, saved.CustomUrl.Length);
            Assert.Equal(2, await dbContext.CSGOStrategies.CountAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SubmitStrategy_Update_ByOwner_UploadsImageAndUpdatesEntity()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var stratId = Guid.NewGuid();
            var originalTimestamp = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Old Title", UserId = "user1", CustomUrl = "old-title",
                Side = Side.TSide, Map = CSGOMap.Dust2, LastUpdated = originalTimestamp
            });
            dbContext.SaveChanges();

            // Raw base64 payload (not an http(s) URL, so the controller sends it to storage)
            const string imageData = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
            const string uploadedUrl = "https://storage.example.com/strategies/image.png";
            _mockStorageService.Setup(s => s.SaveImage(imageData, stratId.ToString())).ReturnsAsync(uploadedUrl);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "New Title", UserId = "user1", CustomUrl = "old-title",
                Description = "Updated", StratImage = imageData, Side = Side.CTSide, Map = CSGOMap.Dust2
            });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.Equal(uploadedUrl, returned.StratImage);
            _mockStorageService.Verify(s => s.SaveImage(imageData, stratId.ToString()), Times.Once);

            var stored = Assert.Single(await dbContext.CSGOStrategies.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(stratId, stored.Id);
            Assert.Equal("New Title", stored.Title);
            Assert.Equal("Updated", stored.Description);
            Assert.Equal(Side.CTSide, stored.Side);
            Assert.Equal(uploadedUrl, stored.StratImage);
            Assert.Equal("user1", stored.UserId);
            Assert.True(stored.LastUpdated > originalTimestamp);
        }

        [Fact]
        public async Task SubmitStrategy_Update_WithAbsoluteImageUrl_DoesNotCallStorage()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var stratId = Guid.NewGuid();
            const string existingUrl = "https://storage.example.com/strategies/existing.png";
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Title", UserId = "user1", CustomUrl = "title",
                StratImage = existingUrl, Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "Renamed", UserId = "user1", CustomUrl = "title",
                StratImage = existingUrl, Side = Side.TSide, Map = CSGOMap.Dust2
            });

            Assert.IsType<OkObjectResult>(result);
            _mockStorageService.Verify(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            var stored = await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken);
            Assert.NotNull(stored);
            Assert.Equal("Renamed", stored.Title);
            Assert.Equal(existingUrl, stored.StratImage);
        }

        [Fact]
        public async Task SubmitStrategy_Update_ReturnsBadRequest_WhenImageUploadFails()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Old Title", UserId = "user1", CustomUrl = "old-title",
                Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            _mockStorageService.Setup(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new FormatException("Invalid image data"));

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "New Title", UserId = "user1", CustomUrl = "old-title",
                StratImage = "not-valid-base64", Side = Side.TSide, Map = CSGOMap.Dust2
            });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid image data", badRequest.Value);
            _mockStorageService.Verify(s => s.SaveImage("not-valid-base64", stratId.ToString()), Times.Once);
            var stored = await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken);
            Assert.NotNull(stored);
            Assert.Equal("Old Title", stored.Title);
        }

        [Fact]
        public async Task SubmitStrategy_Update_TeamStrategyWithoutOwner_AssignsCurrentUser()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var teamId = SeedTeam(dbContext, new TeamMember { UserId = "user1", IsEditor = true });
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Unowned", TeamId = teamId, UserId = null, CustomUrl = "unowned",
                Side = Side.TSide, Map = CSGOMap.Nuke
            });
            dbContext.SaveChanges();

            const string imageUrl = "https://storage.example.com/strategies/unowned.png";
            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "Now Owned", TeamId = teamId, CustomUrl = "unowned",
                StratImage = imageUrl, Side = Side.TSide, Map = CSGOMap.Nuke
            });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.Equal("user1", returned.UserId);
            var stored = await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken);
            Assert.NotNull(stored);
            Assert.Equal("user1", stored.UserId);
            Assert.Equal("Now Owned", stored.Title);
            Assert.Equal(teamId, stored.TeamId);
        }

        [Fact]
        public async Task DeleteStrategy_ReturnsBadRequest_WhenStrategyNotFound()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Title = "Mine", UserId = "user1", CustomUrl = "mine", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.DeleteStrategy(Guid.NewGuid());

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(1, await dbContext.CSGOStrategies.CountAsync(TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task DeleteStrategy_TeamStrategy_ReturnsOk_WhenUserIsEditorOrAdmin(bool isEditor, bool isAdmin)
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var teamId = SeedTeam(dbContext, new TeamMember { UserId = "user1", IsEditor = isEditor, IsAdmin = isAdmin });
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Team Strat", TeamId = teamId, UserId = "someoneelse", CustomUrl = "team-strat",
                Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.DeleteStrategy(stratId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Ok", okResult.Value);
            Assert.Null(await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DeleteStrategy_TeamStrategy_ReturnsBadRequest_WhenUserIsMemberButNotEditor()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var teamId = SeedTeam(dbContext,
                new TeamMember { UserId = "user1", IsEditor = false, IsAdmin = false },
                new TeamMember { UserId = "admin", IsAdmin = true });
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Team Strat", TeamId = teamId, UserId = "admin", CustomUrl = "team-strat",
                Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.DeleteStrategy(stratId);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DeleteStrategy_TeamStrategy_ReturnsBadRequest_WhenUserIsNotTeamMember()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var teamId = SeedTeam(dbContext, new TeamMember { UserId = "admin", IsAdmin = true });
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Team Strat", TeamId = teamId, UserId = "admin", CustomUrl = "team-strat",
                Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.DeleteStrategy(stratId);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SubmitStrategy_TeamStrategy_ReturnsBadRequest_WhenNonMemberEditsTeamThatHasAnAdmin()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "attacker");
            var teamId = SeedTeam(dbContext, new TeamMember { UserId = "admin1", IsAdmin = true, IsEditor = true });
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Team Strat", TeamId = teamId, UserId = "admin1", CustomUrl = "team-strat",
                Side = Side.TSide, Map = CSGOMap.Nuke
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("attacker"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "Hijacked", TeamId = null, UserId = "attacker", CustomUrl = "team-strat",
                StratImage = "https://storage.example.com/x.png", Side = Side.TSide, Map = CSGOMap.Nuke
            });

            Assert.IsType<BadRequestObjectResult>(result);
            var stored = Assert.Single(dbContext.CSGOStrategies);
            Assert.Equal("Team Strat", stored.Title);
            Assert.Equal(teamId, stored.TeamId);
            Assert.Equal("admin1", stored.UserId);
        }

        [Fact]
        public async Task SubmitStrategy_ReturnsBadRequest_WhenStrategyBelongsToAnotherUser()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Owner Strat", UserId = "owner1", CustomUrl = "owner-strat",
                Side = Side.CTSide, Map = CSGOMap.Mirage
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "Overwritten", UserId = "user1", CustomUrl = "owner-strat",
                Side = Side.CTSide, Map = CSGOMap.Mirage
            });

            Assert.IsType<BadRequestObjectResult>(result);
            var stored = Assert.Single(dbContext.CSGOStrategies);
            Assert.Equal("Owner Strat", stored.Title);
            Assert.Equal("owner1", stored.UserId);
        }

        [Fact]
        public async Task SubmitStrategy_Update_KeepsExistingOwner_WhenPayloadHasDifferentUserId()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "editor1");
            var teamId = SeedTeam(dbContext,
                new TeamMember { UserId = "owner1", IsAdmin = true, IsEditor = true },
                new TeamMember { UserId = "editor1", IsEditor = true });
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Team Strat", TeamId = teamId, UserId = "owner1", CustomUrl = "team-strat",
                Side = Side.TSide, Map = CSGOMap.Inferno
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("editor1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "Edited", TeamId = teamId, UserId = "editor1", CustomUrl = "team-strat",
                StratImage = "https://storage.example.com/x.png", Side = Side.TSide, Map = CSGOMap.Inferno
            });

            Assert.IsType<OkObjectResult>(result);
            var stored = Assert.Single(dbContext.CSGOStrategies);
            Assert.Equal("Edited", stored.Title);
            Assert.Equal("owner1", stored.UserId);
        }

        private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        private const string PngDataUri = "data:image/png;base64," + PngBase64;

        [Theory]
        [InlineData(PngDataUri)]
        [InlineData(PngBase64)]
        public async Task SubmitStrategy_Create_UploadsImage_AndStoresUploadedUrl(string image)
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            dbContext.SaveChanges();

            const string uploadedUrl = "https://storage.example.com/strategies/new.png";
            string? uploadedName = null;
            _mockStorageService.Setup(s => s.SaveImage(image, It.IsAny<string>()))
                .Callback<string, string>((_, name) => uploadedName = name)
                .ReturnsAsync(uploadedUrl);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Title = "New Strat", StratImage = image, Side = Side.TSide, Map = CSGOMap.Dust2
            });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.NotEqual(Guid.Empty, returned.Id);
            Assert.Equal(uploadedUrl, returned.StratImage);
            _mockStorageService.Verify(s => s.SaveImage(image, It.IsAny<string>()), Times.Once);
            // The blob is named after the id the strategy is saved with
            Assert.Equal(returned.Id.ToString(), uploadedName);

            dbContext.ChangeTracker.Clear();
            var stored = Assert.Single(await dbContext.CSGOStrategies.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(returned.Id, stored.Id);
            Assert.Equal(uploadedUrl, stored.StratImage);
        }

        [Fact]
        public async Task SubmitStrategy_Create_WithClientSuppliedId_UsesItForBlobName()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            dbContext.SaveChanges();

            var stratId = Guid.NewGuid();
            const string uploadedUrl = "https://storage.example.com/strategies/client-id.png";
            _mockStorageService.Setup(s => s.SaveImage(PngDataUri, stratId.ToString())).ReturnsAsync(uploadedUrl);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "Client Id", StratImage = PngDataUri, Side = Side.TSide, Map = CSGOMap.Dust2
            });

            Assert.IsType<OkObjectResult>(result);
            _mockStorageService.Verify(s => s.SaveImage(PngDataUri, stratId.ToString()), Times.Once);
            var stored = await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken);
            Assert.NotNull(stored);
            Assert.Equal(uploadedUrl, stored.StratImage);
        }

        [Theory]
        [InlineData("https://storage.example.com/strategies/existing.png")]
        [InlineData("http://storage.example.com/strategies/existing.png")]
        public async Task SubmitStrategy_Create_WithHttpImageUrl_DoesNotCallStorage(string imageUrl)
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Title = "Linked Image", StratImage = imageUrl, Side = Side.TSide, Map = CSGOMap.Dust2
            });

            Assert.IsType<OkObjectResult>(result);
            _mockStorageService.Verify(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            var stored = Assert.Single(await dbContext.CSGOStrategies.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(imageUrl, stored.StratImage);
        }

        [Fact]
        public async Task SubmitStrategy_Create_ReturnsBadRequest_AndDoesNotSave_WhenImageUploadFails()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            dbContext.SaveChanges();

            _mockStorageService.Setup(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new FormatException("Invalid image data"));

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Title = "Broken Image", StratImage = "not-valid-base64", Side = Side.TSide, Map = CSGOMap.Dust2
            });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid image data", badRequest.Value);
            _mockStorageService.Verify(s => s.SaveImage("not-valid-base64", It.IsAny<string>()), Times.Once);
            Assert.Empty(await dbContext.CSGOStrategies.ToListAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SubmitStrategy_Update_WithDataUriImage_UploadsImage()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Title", UserId = "user1", CustomUrl = "title",
                StratImage = "https://storage.example.com/strategies/old.png", Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            const string uploadedUrl = "https://storage.example.com/strategies/new.png";
            _mockStorageService.Setup(s => s.SaveImage(PngDataUri, stratId.ToString())).ReturnsAsync(uploadedUrl);

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "Title", UserId = "user1", CustomUrl = "title",
                StratImage = PngDataUri, Side = Side.TSide, Map = CSGOMap.Dust2
            });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsType<CSGOStrategy>(okResult.Value);
            Assert.Equal(uploadedUrl, returned.StratImage);
            _mockStorageService.Verify(s => s.SaveImage(PngDataUri, stratId.ToString()), Times.Once);
            var stored = await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken);
            Assert.NotNull(stored);
            Assert.Equal(uploadedUrl, stored.StratImage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task SubmitStrategy_Update_WithoutImage_DoesNotCallStorage(string? image)
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            SeedAuthUser(dbContext, "user1");
            var stratId = Guid.NewGuid();
            dbContext.CSGOStrategies.Add(new CSGOStrategy
            {
                Id = stratId, Title = "Title", UserId = "user1", CustomUrl = "title",
                Side = Side.TSide, Map = CSGOMap.Dust2
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser("user1"));

            var result = await controller.SubmitStrategy(new CSGOStrategy
            {
                Id = stratId, Title = "Renamed", UserId = "user1", CustomUrl = "title",
                StratImage = image!, Side = Side.TSide, Map = CSGOMap.Dust2
            });

            Assert.IsType<OkObjectResult>(result);
            _mockStorageService.Verify(s => s.SaveImage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            var stored = await dbContext.CSGOStrategies.FindAsync([stratId], TestContext.Current.CancellationToken);
            Assert.NotNull(stored);
            Assert.Equal("Renamed", stored.Title);
        }
    }
}
