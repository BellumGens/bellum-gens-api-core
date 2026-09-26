using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BellumGens.Api.Core.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StarCraft2Models;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
    public class BattleNetServiceProviderTests : IDisposable
    {
        private const string PlayerId = "1234567";

        private readonly MemoryCache _cache;
        private readonly StubHttpMessageHandler _handler;
        private readonly HttpClient _httpClient;
        private readonly ManualTimeProvider _time;
        private readonly IConfiguration _config;
        private readonly BattleNetServiceProvider _provider;
        private int _tokenCounter;

        public BattleNetServiceProviderTests()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _handler = new StubHttpMessageHandler();
            _httpClient = new HttpClient(_handler);
            _time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "battleNet:clientId", "client-id" },
                    { "battleNet:secret", "client-secret" }
                })
                .Build();
            _provider = CreateProvider(new BattleNetTokenCache(_time));
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _handler.Dispose();
            _cache.Dispose();
            GC.SuppressFinalize(this);
        }

        private BattleNetServiceProvider CreateProvider(BattleNetTokenCache tokenCache) =>
            new(_httpClient, _cache, _config, tokenCache, NullLogger<BattleNetServiceProvider>.Instance);

        #region Canned battle.net responses

        private static bool IsTokenRequest(HttpRequestMessage r) => r.RequestUri == new Uri("https://oauth.battle.net/token");
        private static bool IsPlayerRequest(HttpRequestMessage r) => r.RequestUri!.AbsolutePath.StartsWith("/sc2/player/");
        private static bool IsProfileRequest(HttpRequestMessage r) => r.RequestUri!.AbsolutePath.StartsWith("/sc2/metadata/profile/");

        private int TokenRequests => _handler.Requests.Count(IsTokenRequest);
        private int PlayerRequests => _handler.Requests.Count(IsPlayerRequest);
        private int ProfileRequests => _handler.Requests.Count(IsProfileRequest);

        // Each successful token request issues a new token: tok-1, tok-2, ...
        private void StubTokenEndpoint(int expiresIn = 3600)
        {
            _handler.Respond(IsTokenRequest, _ => Json(HttpStatusCode.OK,
                $$"""{"access_token":"tok-{{Interlocked.Increment(ref _tokenCounter)}}","token_type":"bearer","expires_in":{{expiresIn}},"sub":"client-id"}"""));
        }

        private static string PlayerJson(string profileId = PlayerId, string name = "TestPlayer", int regionId = 2) => $$"""
            [{"name":"{{name}}","profileUrl":"https://starcraft2.blizzard.com/profile/{{regionId}}/1/{{profileId}}",
              "avatarUrl":"https://static.starcraft2.com/starport/avatar.jpg","profileId":"{{profileId}}","regionId":{{regionId}},"realmId":1}]
            """;

        private const string ProfileJson = """
            {"summary":{"id":"1234567","realm":1,"displayName":"TestPlayer","portrait":"https://static.starcraft2.com/portrait.jpg",
              "decalTerran":"https://static.starcraft2.com/terran.jpg","decalProtoss":"https://static.starcraft2.com/protoss.jpg",
              "decalZerg":"https://static.starcraft2.com/zerg.jpg","totalSwarmLevel":123,"totalAchievementPoints":4560}}
            """;

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        #endregion

        [Fact]
        public async Task GetStarCraft2Player_OnCacheMiss_RequestsTokenThenPlayer()
        {
            StubTokenEndpoint();
            _handler.Respond(IsPlayerRequest, HttpStatusCode.OK, PlayerJson(), "application/json");

            var result = await _provider.GetStarCraft2Player(PlayerId);

            Assert.NotNull(result);
            Assert.Equal("TestPlayer", result.name);
            Assert.Equal(PlayerId, result.profileId);
            Assert.Equal(2, result.regionId);

            var requests = _handler.Requests;
            Assert.Equal(2, requests.Count);
            var tokenRequest = requests[0];
            Assert.Equal(HttpMethod.Post, tokenRequest.Method);
            Assert.True(IsTokenRequest(tokenRequest));
            Assert.Equal("Basic", tokenRequest.Headers.Authorization!.Scheme);
            Assert.Equal("client-id:client-secret", Encoding.UTF8.GetString(Convert.FromBase64String(tokenRequest.Headers.Authorization.Parameter!)));
            Assert.Equal("grant_type=client_credentials", _handler.RequestBodies[0]);

            var playerRequest = requests[1];
            Assert.Equal(new Uri($"https://eu.api.blizzard.com/sc2/player/{PlayerId}"), playerRequest.RequestUri);
            Assert.Equal("Bearer", playerRequest.Headers.Authorization!.Scheme);
            Assert.Equal("tok-1", playerRequest.Headers.Authorization.Parameter);
        }

        // Regression: the provider cached Player[] but checked "is Player", so the cache never hit.
        [Fact]
        public async Task GetStarCraft2Player_SecondCall_IsServedFromCache()
        {
            StubTokenEndpoint();
            _handler.Respond(IsPlayerRequest, HttpStatusCode.OK, PlayerJson(), "application/json");

            var first = await _provider.GetStarCraft2Player(PlayerId);
            var second = await _provider.GetStarCraft2Player(PlayerId);

            Assert.Same(first, second);
            Assert.Equal(1, PlayerRequests);
            Assert.Equal(1, TokenRequests);
        }

        [Fact]
        public async Task GetStarCraft2Player_WhenPlayerCached_ReturnsCachedPlayerWithoutRequests()
        {
            var player = new Player
            {
                name = "CachedPlayer", profileId = PlayerId, regionId = 2, realmId = 1,
                profileUrl = "https://starcraft2.blizzard.com/profile/2/1/1234567"
            };
            _cache.Set(BattleNetServiceProvider.PlayerCacheKey(PlayerId, "eu"), player);

            var result = await _provider.GetStarCraft2Player(PlayerId);

            Assert.Same(player, result);
            Assert.Empty(_handler.Requests);
        }

        [Fact]
        public async Task GetStarCraft2Player_CacheIsPerRegion()
        {
            StubTokenEndpoint();
            _handler.Respond(r => IsPlayerRequest(r) && r.RequestUri!.Host == "eu.api.blizzard.com", HttpStatusCode.OK, PlayerJson(name: "EuPlayer"), "application/json");
            _handler.Respond(r => IsPlayerRequest(r) && r.RequestUri!.Host == "us.api.blizzard.com", HttpStatusCode.OK, PlayerJson(name: "UsPlayer", regionId: 1), "application/json");

            var eu = await _provider.GetStarCraft2Player(PlayerId, "eu");
            var us = await _provider.GetStarCraft2Player(PlayerId, "us");

            Assert.Equal("EuPlayer", eu.name);
            Assert.Equal("UsPlayer", us.name);
            Assert.Equal(2, PlayerRequests);
        }

        [Fact]
        public async Task GetStarCraft2Player_WithNonSuccessStatus_ReturnsNullAndDoesNotCache()
        {
            StubTokenEndpoint();
            _handler.Respond(IsPlayerRequest, HttpStatusCode.NotFound, "{}", "application/json");

            Assert.Null(await _provider.GetStarCraft2Player(PlayerId));
            Assert.Null(await _provider.GetStarCraft2Player(PlayerId));

            Assert.Equal(2, PlayerRequests);
            Assert.Equal(1, TokenRequests);
        }

        [Fact]
        public async Task GetStarCraft2Player_WithEmptyPlayerList_ReturnsNull()
        {
            StubTokenEndpoint();
            _handler.Respond(IsPlayerRequest, HttpStatusCode.OK, "[]", "application/json");

            var result = await _provider.GetStarCraft2Player(PlayerId);

            Assert.Null(result);
        }

        // Regression: a failed token request left the token null and the next line threw NullReferenceException.
        [Theory]
        [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""")]
        [InlineData(HttpStatusCode.ServiceUnavailable, "")]
        [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
        [InlineData(HttpStatusCode.OK, """{"token_type":"bearer","expires_in":3600}""")]
        public async Task GetStarCraft2Player_WhenTokenRequestFails_ReturnsNullWithoutCallingApi(HttpStatusCode status, string body)
        {
            _handler.Respond(IsTokenRequest, status, body, "application/json");

            var result = await _provider.GetStarCraft2Player(PlayerId);

            Assert.Null(result);
            Assert.Equal(0, PlayerRequests);
        }

        [Fact]
        public async Task GetStarCraft2Player_WhenTokenEndpointUnreachable_ReturnsNull()
        {
            _handler.Respond(IsTokenRequest, _ => throw new HttpRequestException("connection refused"));

            var result = await _provider.GetStarCraft2Player(PlayerId);

            Assert.Null(result);
            Assert.Equal(0, PlayerRequests);
        }

        [Fact]
        public async Task GetStarCraft2PlayerProfile_WhenTokenRequestFails_ReturnsNullWithoutCallingApi()
        {
            _handler.Respond(IsTokenRequest, HttpStatusCode.BadRequest, "", "application/json");

            var result = await _provider.GetStarCraft2PlayerProfile(PlayerId);

            Assert.Null(result);
            Assert.Equal(0, ProfileRequests);
        }

        [Fact]
        public async Task FailedTokenRequest_IsNotCached_AndIsRetriedOnNextCall()
        {
            var tokenCalls = 0;
            _handler.Respond(IsTokenRequest, _ => ++tokenCalls == 1
                ? Json(HttpStatusCode.ServiceUnavailable, "")
                : Json(HttpStatusCode.OK, """{"access_token":"tok-ok","token_type":"bearer","expires_in":3600}"""));
            _handler.Respond(IsPlayerRequest, HttpStatusCode.OK, PlayerJson(), "application/json");

            Assert.Null(await _provider.GetStarCraft2Player(PlayerId));
            var result = await _provider.GetStarCraft2Player(PlayerId);

            Assert.NotNull(result);
            Assert.Equal(2, tokenCalls);
            Assert.Equal("tok-ok", _handler.Requests.Single(IsPlayerRequest).Headers.Authorization!.Parameter);
        }

        [Fact]
        public async Task Token_IsReusedUntilItExpires()
        {
            StubTokenEndpoint(expiresIn: 3600);
            _handler.Respond(IsPlayerRequest, r => Json(HttpStatusCode.OK, PlayerJson(r.RequestUri!.Segments.Last())));
            _handler.Respond(IsProfileRequest, HttpStatusCode.OK, ProfileJson, "application/json");

            await _provider.GetStarCraft2Player("1");
            _time.Advance(TimeSpan.FromMinutes(30));
            await _provider.GetStarCraft2Player("2");
            await _provider.GetStarCraft2PlayerProfile("3");

            Assert.Equal(1, TokenRequests);
            Assert.All(_handler.Requests.Where(r => !IsTokenRequest(r)), r => Assert.Equal("tok-1", r.Headers.Authorization!.Parameter));
        }

        [Fact]
        public async Task Token_IsRefreshedAfterExpiry()
        {
            StubTokenEndpoint(expiresIn: 3600);
            _handler.Respond(IsPlayerRequest, r => Json(HttpStatusCode.OK, PlayerJson(r.RequestUri!.Segments.Last())));

            await _provider.GetStarCraft2Player("1");
            _time.Advance(TimeSpan.FromSeconds(3601));
            await _provider.GetStarCraft2Player("2");

            Assert.Equal(2, TokenRequests);
            var playerRequests = _handler.Requests.Where(IsPlayerRequest).ToList();
            Assert.Equal("tok-1", playerRequests[0].Headers.Authorization!.Parameter);
            Assert.Equal("tok-2", playerRequests[1].Headers.Authorization!.Parameter);
        }

        [Fact]
        public async Task Token_IsRefreshedShortlyBeforeExpiry()
        {
            StubTokenEndpoint(expiresIn: 3600);
            _handler.Respond(IsPlayerRequest, r => Json(HttpStatusCode.OK, PlayerJson(r.RequestUri!.Segments.Last())));

            await _provider.GetStarCraft2Player("1");
            _time.Advance(TimeSpan.FromSeconds(3600) - BattleNetTokenCache.ExpirySafetyMargin);
            await _provider.GetStarCraft2Player("2");

            Assert.Equal(2, TokenRequests);
        }

        // Regression: every successful player fetch reset the token issue time, so a token that was
        // used regularly was considered valid long after it had actually expired.
        [Fact]
        public async Task Token_ExpiryIsMeasuredFromIssueTime_NotFromLastSuccessfulCall()
        {
            StubTokenEndpoint(expiresIn: 3600);
            _handler.Respond(IsPlayerRequest, r => Json(HttpStatusCode.OK, PlayerJson(r.RequestUri!.Segments.Last())));

            await _provider.GetStarCraft2Player("1");
            _time.Advance(TimeSpan.FromMinutes(50));
            await _provider.GetStarCraft2Player("2");
            _time.Advance(TimeSpan.FromMinutes(15));
            await _provider.GetStarCraft2Player("3");

            Assert.Equal(2, TokenRequests);
            Assert.Equal("tok-2", _handler.Requests.Where(IsPlayerRequest).Last().Headers.Authorization!.Parameter);
        }

        [Fact]
        public async Task Token_IsDiscardedWhenApiRejectsIt()
        {
            StubTokenEndpoint(expiresIn: 3600);
            var playerCalls = 0;
            _handler.Respond(IsPlayerRequest, _ => ++playerCalls == 1
                ? Json(HttpStatusCode.Unauthorized, "")
                : Json(HttpStatusCode.OK, PlayerJson()));

            Assert.Null(await _provider.GetStarCraft2Player(PlayerId));
            Assert.NotNull(await _provider.GetStarCraft2Player(PlayerId));

            Assert.Equal(2, TokenRequests);
            Assert.Equal("tok-2", _handler.Requests.Where(IsPlayerRequest).Last().Headers.Authorization!.Parameter);
        }

        // Regression: token state was static, so it leaked between provider instances (and tests).
        [Fact]
        public async Task TokenState_IsOwnedByTheTokenCache_NotSharedStatically()
        {
            StubTokenEndpoint();
            _handler.Respond(IsPlayerRequest, r => Json(HttpStatusCode.OK, PlayerJson(r.RequestUri!.Segments.Last())));
            var otherProvider = CreateProvider(new BattleNetTokenCache(_time));

            await _provider.GetStarCraft2Player("1");
            await otherProvider.GetStarCraft2Player("2");

            Assert.Equal(2, TokenRequests);
        }

        [Fact]
        public async Task GetStarCraft2PlayerProfile_OnCacheMiss_ParsesProfileAndSendsBearerToken()
        {
            StubTokenEndpoint();
            _handler.Respond(IsProfileRequest, HttpStatusCode.OK, ProfileJson, "application/json");

            var result = await _provider.GetStarCraft2PlayerProfile(PlayerId, "us", 1, 2);

            Assert.Equal("1234567", result.summary.id);
            Assert.Equal("TestPlayer", result.summary.displayName);
            Assert.Equal(123, result.summary.totalSwarmLevel);
            var request = _handler.Requests.Single(IsProfileRequest);
            Assert.Equal(new Uri($"https://us.api.blizzard.com/sc2/metadata/profile/1/2/{PlayerId}?locale=en_US"), request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("tok-1", request.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task GetStarCraft2PlayerProfile_SecondCall_IsServedFromCache()
        {
            StubTokenEndpoint();
            _handler.Respond(IsProfileRequest, HttpStatusCode.OK, ProfileJson, "application/json");

            var first = await _provider.GetStarCraft2PlayerProfile(PlayerId);
            var second = await _provider.GetStarCraft2PlayerProfile(PlayerId);

            Assert.Same(first, second);
            Assert.Equal(1, ProfileRequests);
        }

        [Fact]
        public async Task GetStarCraft2PlayerProfile_WhenProfileCached_ReturnsCachedProfileWithoutRequests()
        {
            var profile = new PlayerProfile
            {
                summary = new PlayerProfileSummary { id = PlayerId, realm = 1, displayName = "CachedPlayer" }
            };
            _cache.Set(BattleNetServiceProvider.PlayerProfileCacheKey(PlayerId, "us", 1, 1), profile);

            var result = await _provider.GetStarCraft2PlayerProfile(PlayerId, "us", 1, 1);

            Assert.Same(profile, result);
            Assert.Empty(_handler.Requests);
        }

        [Fact]
        public async Task GetStarCraft2PlayerProfile_WithNonSuccessStatus_ReturnsNull()
        {
            StubTokenEndpoint();
            _handler.Respond(IsProfileRequest, HttpStatusCode.NotFound, "", "application/json");

            var result = await _provider.GetStarCraft2PlayerProfile(PlayerId);

            Assert.Null(result);
        }

        // Regression: Player and PlayerProfile shared the same cache key, so each evicted the other.
        [Fact]
        public async Task PlayerAndProfile_AreCachedUnderDistinctKeys()
        {
            StubTokenEndpoint();
            _handler.Respond(IsPlayerRequest, HttpStatusCode.OK, PlayerJson(), "application/json");
            _handler.Respond(IsProfileRequest, HttpStatusCode.OK, ProfileJson, "application/json");

            var player = await _provider.GetStarCraft2Player(PlayerId);
            var profile = await _provider.GetStarCraft2PlayerProfile(PlayerId);
            var playerAgain = await _provider.GetStarCraft2Player(PlayerId);
            var profileAgain = await _provider.GetStarCraft2PlayerProfile(PlayerId);

            Assert.Same(player, playerAgain);
            Assert.Same(profile, profileAgain);
            Assert.Equal(1, PlayerRequests);
            Assert.Equal(1, ProfileRequests);
        }

        [Fact]
        public async Task TokenCache_ConcurrentCallers_ShareASingleTokenRequest()
        {
            var tokenCache = new BattleNetTokenCache(_time);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var fetches = 0;
            async Task<BattleNetAccessToken> Fetch()
            {
                Interlocked.Increment(ref fetches);
                await release.Task;
                return new BattleNetAccessToken("shared", TimeSpan.FromHours(1));
            }

            var callers = Enumerable.Range(0, 10).Select(_ => Task.Run(() => tokenCache.GetAccessTokenAsync(Fetch), TestContext.Current.CancellationToken)).ToArray();
            await Task.Delay(50, TestContext.Current.CancellationToken);
            release.SetResult();
            var tokens = await Task.WhenAll(callers);

            Assert.Equal(1, fetches);
            Assert.All(tokens, t => Assert.Equal("shared", t));
        }

        [Fact]
        public async Task TokenCache_Invalidate_ForcesNewTokenRequest()
        {
            var tokenCache = new BattleNetTokenCache(_time);
            var fetches = 0;
            Task<BattleNetAccessToken> Fetch() => Task.FromResult(new BattleNetAccessToken($"t{++fetches}", TimeSpan.FromHours(1)));

            Assert.Equal("t1", await tokenCache.GetAccessTokenAsync(Fetch));
            Assert.Equal("t1", await tokenCache.GetAccessTokenAsync(Fetch));
            tokenCache.Invalidate();
            Assert.Equal("t2", await tokenCache.GetAccessTokenAsync(Fetch));
        }
    }

    /// <summary>A TimeProvider whose clock only moves when the test advances it.</summary>
    internal sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset start)
        {
            _utcNow = start;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by) => _utcNow += by;
    }
}
