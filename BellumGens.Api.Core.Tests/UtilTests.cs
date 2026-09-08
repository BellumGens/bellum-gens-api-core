using BellumGens.Api.Core.Common;
using BellumGens.Api.Core.Models;

namespace BellumGens.Api.Core.Tests
{
    public class UtilTests
    {
        [Fact]
        public void GenerateHashString_WithSpecifiedLength_ReturnsCorrectLength()
        {
            var result = Util.GenerateHashString(16);

            Assert.Equal(16, result.Length);
        }

        [Fact]
        public void GenerateHashString_WithZeroLength_ReturnsEmptyString()
        {
            var result = Util.GenerateHashString(0);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GenerateHashString_WithDefaultLength_ReturnsEmptyString()
        {
            var result = Util.GenerateHashString();

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GenerateHashString_ReturnsAlphanumericCharacters()
        {
            var result = Util.GenerateHashString(100);

            Assert.Matches("^[A-Za-z0-9]+$", result);
        }
    }
}
