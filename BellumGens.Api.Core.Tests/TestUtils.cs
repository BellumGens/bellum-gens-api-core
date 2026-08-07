using System;
using System.Collections.Generic;
using System.Security.Claims;
using BellumGens.Api.Core;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BellumGens.Api.Core.Tests
{
    public static class TestUtils
    {
        public static BellumGensDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<BellumGensDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new BellumGensDbContext(options);
        }

        public static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        public static Mock<RoleManager<IdentityRole>> CreateMockRoleManager()
        {
            var store = new Mock<IRoleStore<IdentityRole>>();
            return new Mock<RoleManager<IdentityRole>>(
                store.Object, null!, null!, null!, null!);
        }

        public static Mock<SignInManager<ApplicationUser>> CreateMockSignInManager(
            Mock<UserManager<ApplicationUser>> userManager)
        {
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            return new Mock<SignInManager<ApplicationUser>>(
                userManager.Object, contextAccessor.Object, claimsFactory.Object, null!, null!, null!, null!);
        }

        public static EmailServiceProvider CreateMockEmailServiceProvider()
        {
            var appConfig = CreateAppConfiguration();
            return new EmailServiceProvider(appConfig);
        }

        public static Mock<ISteamService> CreateMockSteamService()
        {
            return new Mock<ISteamService>();
        }

        public static Mock<IBattleNetService> CreateMockBattleNetService()
        {
            return new Mock<IBattleNetService>();
        }

        public static Mock<INotificationService> CreateMockNotificationService()
        {
            return new Mock<INotificationService>();
        }

        public static Mock<IStorageService> CreateMockStorageService()
        {
            return new Mock<IStorageService>();
        }

        public static Mock<ILogger<T>> CreateMockLogger<T>()
        {
            return new Mock<ILogger<T>>();
        }

        public static AppConfiguration CreateAppConfiguration()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "steamApiKey", "test" },
                { "battleNet:clientId", "test" },
                { "battleNet:secret", "test" },
                { "twitch:clientId", "test" },
                { "twitch:secret", "test" },
                { "vapid:public", "test" },
                { "vapid:private", "test" },
                { "email:username", "test@test.com" },
                { "email:password", "test" },
                { "bank:name", "test" },
                { "bank:owner", "test" },
                { "bank:bic", "test" },
                { "bank:account", "test" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            return new AppConfiguration(configuration);
        }

        public static ClaimsPrincipal CreateAuthenticatedUser(string userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "testuser")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        public static ClaimsPrincipal CreateUnauthenticatedUser()
        {
            var identity = new ClaimsIdentity();
            return new ClaimsPrincipal(identity);
        }

        public static void SetupControllerContext(ControllerBase controller, ClaimsPrincipal user)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
    }
}
