using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    public class AdminControllerTests
    {
        [Fact]
        public async Task GetRoles_ReturnsRolesList()
        {
            var mockUserManager = TestUtils.CreateMockUserManager();
            var mockRoleManager = TestUtils.CreateMockRoleManager();
            var mockSignInManager = TestUtils.CreateMockSignInManager(mockUserManager);
            var emailService = TestUtils.CreateMockEmailServiceProvider();
            var mockLogger = TestUtils.CreateMockLogger<AdminController>();

            using var dbContext = TestUtils.CreateInMemoryDbContext();

            // Add roles directly to the db since RoleManager uses the same store
            dbContext.Roles.Add(new IdentityRole { Id = "1", Name = "admin", NormalizedName = "ADMIN" });
            dbContext.Roles.Add(new IdentityRole { Id = "2", Name = "event-admin", NormalizedName = "EVENT-ADMIN" });
            dbContext.SaveChanges();

            // Mock Roles property on RoleManager to use an IQueryable from the dbContext
            mockRoleManager.Setup(r => r.Roles)
                .Returns(dbContext.Roles);

            var controller = new AdminController(
                mockUserManager.Object, mockRoleManager.Object,
                mockSignInManager.Object, emailService, dbContext, mockLogger.Object);

            List<string> result = await controller.GetRoles();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains("admin", result);
            Assert.Contains("event-admin", result);
        }
    }
}
