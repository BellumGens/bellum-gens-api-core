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

        [Fact]
        public void JerseyCutNames_ContainsMaleEntry()
        {
            var names = Util.JerseyCutNames;

            Assert.True(names.ContainsKey(JerseyCut.Male));
            Assert.Equal("Мъжка", names[JerseyCut.Male]);
        }

        [Fact]
        public void JerseyCutNames_ContainsFemaleEntry()
        {
            var names = Util.JerseyCutNames;

            Assert.True(names.ContainsKey(JerseyCut.Female));
            Assert.Equal("Дамска", names[JerseyCut.Female]);
        }

        [Fact]
        public void JerseyCutNames_ContainsExactlyTwoEntries()
        {
            var names = Util.JerseyCutNames;

            Assert.Equal(2, names.Count);
        }

        [Fact]
        public void JerseySizeNames_ContainsAllSizes()
        {
            var names = Util.JerseySizeNames;

            Assert.Equal(7, names.Count);
            Assert.Equal("XS", names[JerseySize.XS]);
            Assert.Equal("S", names[JerseySize.S]);
            Assert.Equal("M", names[JerseySize.M]);
            Assert.Equal("L", names[JerseySize.L]);
            Assert.Equal("XL", names[JerseySize.XL]);
            Assert.Equal("XXL", names[JerseySize.XXL]);
            Assert.Equal("XXXL", names[JerseySize.XXXL]);
        }
    }
}
