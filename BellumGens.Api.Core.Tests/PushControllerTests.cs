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
    public class PushControllerTests
    {
        [Fact]
        public async Task Subscribe_SavesPushSubscription()
        {
            var mockUserManager = TestUtils.CreateMockUserManager();
            var mockRoleManager = TestUtils.CreateMockRoleManager();
            var mockSignInManager = TestUtils.CreateMockSignInManager(mockUserManager);
            var emailService = TestUtils.CreateMockEmailServiceProvider();
            var mockLogger = TestUtils.CreateMockLogger<PushController>();

            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var userId = "user1";
            var user = new ApplicationUser { Id = userId, UserName = "testuser" };
            dbContext.Users.Add(user);
            dbContext.SaveChanges();

            mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var controller = new PushController(
                mockUserManager.Object, mockRoleManager.Object,
                mockSignInManager.Object, emailService, dbContext, mockLogger.Object);
            TestUtils.SetupControllerContext(controller, TestUtils.CreateAuthenticatedUser(userId));

            var sub = new BellumGensPushSubscriptionViewModel
            {
                Endpoint = "https://push.example.com/test",
                Keys = new BellumGensPushSubscriptionViewModel.SubKeys
                {
                    P256dh = "testP256dh",
                    Auth = "testAuth"
                }
            };

            var result = await controller.Subscribe(sub);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var savedSub = Assert.IsType<BellumGensPushSubscription>(okResult.Value);
            Assert.Equal("https://push.example.com/test", savedSub.Endpoint);
            Assert.Equal(userId, savedSub.UserId);
            Assert.Equal("testP256dh", savedSub.P256dh);
            Assert.Equal("testAuth", savedSub.Auth);
        }
    }
}
