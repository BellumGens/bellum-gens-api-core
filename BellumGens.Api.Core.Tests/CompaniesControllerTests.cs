using System.Collections.Generic;
using BellumGens.Api.Controllers;
using BellumGens.Api.Core.Models;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    public class CompaniesControllerTests
    {
        [Fact]
        public void Get_ReturnsEmptyList_WhenNoCompanies()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            var controller = new CompaniesController(dbContext);

            List<string> result = controller.Get();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Get_ReturnsCompanyNames_WhenCompaniesExist()
        {
            using var dbContext = TestUtils.CreateInMemoryDbContext();
            dbContext.Companies.Add(new Company { Name = "TestCompany1", Logo = "logo1.png" });
            dbContext.Companies.Add(new Company { Name = "TestCompany2", Logo = "logo2.png" });
            dbContext.SaveChanges();

            var controller = new CompaniesController(dbContext);

            List<string> result = controller.Get();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains("TestCompany1", result);
            Assert.Contains("TestCompany2", result);
        }
    }
}
