using BellumGens.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    public class HomeControllerTests
    {
        [Fact]
        public void Index_ReturnsRedirectResult()
        {
            var controller = new HomeController();

            var result = controller.Index();

            Assert.IsType<RedirectResult>(result);
        }
    }
}
