using System;
using BellumGens.Api.Core.Providers;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    public class SteamServiceProviderTests
    {
        private readonly SteamServiceProvider _provider;

        public SteamServiceProviderTests()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var appConfig = TestUtils.CreateAppConfiguration();
            _provider = new SteamServiceProvider(cache, appConfig);
        }

        [Fact]
        public void NormalizeUsername_With17DigitSteamId_UsesProfilesUrl()
        {
            var result = _provider.NormalizeUsername("76561198012345678");

            Assert.Equal(new Uri("https://steamcommunity.com/profiles/76561198012345678/?xml=1"), result);
        }

        [Fact]
        public void NormalizeUsername_WithSteamCommunityUrl_AppendXmlParam()
        {
            var result = _provider.NormalizeUsername("https://steamcommunity.com/id/someuser");

            Assert.Equal(new Uri("https://steamcommunity.com/id/someuser/?xml=1"), result);
        }

        [Fact]
        public void NormalizeUsername_WithPlainUsername_UsesIdUrl()
        {
            var result = _provider.NormalizeUsername("myuser");

            Assert.Equal(new Uri("https://steamcommunity.com/id/myuser/?xml=1"), result);
        }

        [Fact]
        public void SteamUserId_WithValidOpenIdUrl_ReturnsId()
        {
            var result = _provider.SteamUserId("https://steamcommunity.com/openid/id/76561198012345678");

            Assert.Equal("76561198012345678", result);
        }

        [Fact]
        public void SteamUserId_WithShortUrl_ReturnsNull()
        {
            var result = _provider.SteamUserId("https://short/url");

            Assert.Null(result);
        }
    }
}
