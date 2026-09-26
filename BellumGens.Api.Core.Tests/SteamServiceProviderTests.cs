using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.Extensions.Caching.Memory;
using SteamModels;
using SteamModels.CSGO;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
	public class SteamServiceProviderTests : IDisposable
	{
		private const string SteamId = "76561198012345678";
		private const string GroupId = "103582791429521408";

		private readonly MemoryCache _cache;
		private readonly StubHttpMessageHandler _handler;
		private readonly HttpClient _httpClient;
		private readonly SteamServiceProvider _provider;

		public SteamServiceProviderTests()
		{
			_cache = new MemoryCache(new MemoryCacheOptions());
			_handler = new StubHttpMessageHandler();
			_httpClient = new HttpClient(_handler);
			var appConfig = TestUtils.CreateAppConfiguration();
			_provider = new SteamServiceProvider(_httpClient, _cache, appConfig);
		}

		public void Dispose()
		{
			_httpClient.Dispose();
			_handler.Dispose();
			_cache.Dispose();
			GC.SuppressFinalize(this);
		}

		#region Canned Steam responses

		private static string ProfileXml(string steamId = SteamId, string name = "TestPlayer") => $"""
			<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
			<profile>
				<steamID64>{steamId}</steamID64>
				<steamID><![CDATA[{name}]]></steamID>
				<onlineState>online</onlineState>
				<stateMessage><![CDATA[Online]]></stateMessage>
				<privacyState>public</privacyState>
				<visibilityState>3</visibilityState>
				<avatarIcon><![CDATA[https://avatars.steamstatic.com/abc.jpg]]></avatarIcon>
				<avatarMedium><![CDATA[https://avatars.steamstatic.com/abc_medium.jpg]]></avatarMedium>
				<avatarFull><![CDATA[https://avatars.steamstatic.com/abc_full.jpg]]></avatarFull>
				<vacBanned>0</vacBanned>
				<tradeBanState>None</tradeBanState>
				<isLimitedAccount>0</isLimitedAccount>
				<customURL><![CDATA[testplayer]]></customURL>
				<memberSince>March 1, 2010</memberSince>
				<steamRating></steamRating>
				<hoursPlayed2Wk>12.5</hoursPlayed2Wk>
				<headline><![CDATA[]]></headline>
				<location><![CDATA[Sofia, Bulgaria]]></location>
				<realname><![CDATA[Test Player]]></realname>
				<summary><![CDATA[No information given.]]></summary>
				<mostPlayedGames>
					<mostPlayedGame>
						<gameName><![CDATA[Counter-Strike 2]]></gameName>
						<gameLink><![CDATA[https://steamcommunity.com/app/730]]></gameLink>
						<hoursPlayed>10.2</hoursPlayed>
					</mostPlayedGame>
				</mostPlayedGames>
				<groups>
					<group isPrimary="1">
						<groupID64>{GroupId}</groupID64>
					</group>
				</groups>
			</profile>
			""";

		private static string GroupXml(params string[] members) => $"""
			<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
			<memberList>
				<groupID64>{GroupId}</groupID64>
				<groupDetails>
					<groupName><![CDATA[Bellum Gens]]></groupName>
					<groupURL><![CDATA[bellumgens]]></groupURL>
					<headline><![CDATA[]]></headline>
					<summary><![CDATA[Team group]]></summary>
					<avatarIcon><![CDATA[https://avatars.steamstatic.com/group.jpg]]></avatarIcon>
					<memberCount>{members.Length}</memberCount>
					<membersInChat>0</membersInChat>
					<membersInGame>0</membersInGame>
					<membersOnline>1</membersOnline>
				</groupDetails>
				<memberCount>{members.Length}</memberCount>
				<totalPages>1</totalPages>
				<currentPage>1</currentPage>
				<startingMember>0</startingMember>
				<members>
			{string.Concat(members.Select(m => $"		<steamID64>{m}</steamID64>\n"))}	</members>
			</memberList>
			""";

		private const string StatsJson = """
			{"playerstats":{"steamID":"76561198012345678","gameName":"ValveTestApp260","stats":[
				{"name":"total_kills","value":1000},{"name":"total_deaths","value":500},
				{"name":"total_kills_headshot","value":250},{"name":"total_shots_fired","value":10000},
				{"name":"total_shots_hit","value":2000},{"name":"total_kills_ak47","value":400},
				{"name":"total_shots_ak47","value":4000},{"name":"total_hits_ak47","value":800}],
				"achievements":[{"name":"WIN_BOMB_PLANT","achieved":1}]}}
			""";

		private const string PrivateStatsJson = """{"playerstats":{"error":"Profile is not public"}}""";

		private const string SummariesJson = """
			{"response":{"players":[{"steamid":"76561198012345678","communityvisibilitystate":3,"profilestate":1,
				"personaname":"TestPlayer","commentpermission":1,"profileurl":"https://steamcommunity.com/id/testplayer/",
				"avatar":"https://avatars.steamstatic.com/abc.jpg","avatarmedium":"https://avatars.steamstatic.com/abc_medium.jpg",
				"avatarfull":"https://avatars.steamstatic.com/abc_full.jpg","avatarhash":"abc","lastlogoff":1700000000,
				"personastate":1,"primaryclanid":"103582791429521408","timecreated":1267401600,"personastateflags":0,
				"loccountrycode":"BG"}]}}
			""";

		private static bool IsProfileRequest(HttpRequestMessage r) => r.RequestUri!.Host == "steamcommunity.com" && r.RequestUri.AbsolutePath.StartsWith("/profiles/");
		private static bool IsStatsRequest(HttpRequestMessage r) => r.RequestUri!.AbsolutePath.Contains("GetUserStatsForGame");
		private static bool IsGroupRequest(HttpRequestMessage r) => r.RequestUri!.AbsolutePath.Contains("memberslistxml");

		#endregion

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

		[Fact]
		public async Task GetSteamUser_WhenUserStatsCached_ReturnsCachedSteamUserWithoutRequest()
		{
			var steamUser = new SteamUser { steamID64 = "76561198012345678", steamID = "cachedUser" };
			_cache.Set("cachedUser", new UserStatsViewModel { SteamUser = steamUser });

			var result = await _provider.GetSteamUser("cachedUser");

			Assert.Same(steamUser, result);
			Assert.Empty(_handler.Requests);
		}

		[Fact]
		public async Task GetSteamUser_OnCacheMiss_FetchesAndParsesProfileXml()
		{
			_handler.Respond(IsProfileRequest, HttpStatusCode.OK, ProfileXml(), "text/xml");

			var result = await _provider.GetSteamUser(SteamId);

			Assert.Equal(SteamId, result.steamID64);
			Assert.Equal("TestPlayer", result.steamID);
			Assert.Equal("Sofia, Bulgaria", result.location);
			Assert.Equal(12.5f, result.hoursPlayed2Wk);
			Assert.Single(result.mostPlayedGames);
			Assert.Equal(GroupId, Assert.Single(result.groups).groupID64);
			var request = Assert.Single(_handler.Requests);
			Assert.Equal(new Uri($"https://steamcommunity.com/profiles/{SteamId}/?xml=1"), request.RequestUri);
		}

		[Fact]
		public async Task GetSteamUser_WithNonSuccessStatus_Throws()
		{
			_handler.Respond(IsProfileRequest, HttpStatusCode.ServiceUnavailable, "busy");

			await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetSteamUser(SteamId));
		}

		[Fact]
		public async Task GetSteamUsersSummary_ParsesPlayersAndSendsKeyAndIds()
		{
			_handler.Respond(r => r.RequestUri!.AbsolutePath.Contains("GetPlayerSummaries"), HttpStatusCode.OK, SummariesJson, "application/json");

			var result = await _provider.GetSteamUsersSummary($"{SteamId},76561198000000002");

			var player = Assert.Single(result);
			Assert.Equal(SteamId, player.steamid);
			Assert.Equal("TestPlayer", player.personaname);
			Assert.Equal("BG", player.loccountrycode);
			var query = Assert.Single(_handler.Requests).RequestUri!.Query;
			Assert.Contains("key=test", query);
			Assert.Contains($"steamids={SteamId},76561198000000002", Uri.UnescapeDataString(query));
		}

		[Fact]
		public async Task GetStatsForCSGOUser_ParsesStatsAndDerivedValues()
		{
			_handler.Respond(IsStatsRequest, HttpStatusCode.OK, StatsJson, "application/json");

			var result = await _provider.GetStatsForCSGOUser(SteamId);

			Assert.True(result.playerstats.success);
			Assert.Equal(2m, result.killDeathRatio);
			Assert.Equal("ak47", result.favouriteWeapon.name);
			var query = Assert.Single(_handler.Requests).RequestUri!.Query;
			Assert.Contains("appid=730", query);
			Assert.Contains($"steamid={SteamId}", query);
		}

		[Fact]
		public async Task GetSteamUserDetails_WhenCached_ReturnsCachedModelWithoutRequest()
		{
			var cached = new UserStatsViewModel
			{
				SteamUser = new SteamUser { steamID64 = "76561198012345678", steamID = "cachedUser" },
				UserStats = new CSGOPlayerStats()
			};
			_cache.Set("76561198012345678", cached);

			var result = await _provider.GetSteamUserDetails("76561198012345678");

			Assert.Same(cached, result);
			Assert.False(result.SteamUserException);
			Assert.False(result.UserStatsException);
			Assert.Empty(_handler.Requests);
		}

		[Fact]
		public async Task GetSteamUserDetails_OnSuccess_ParsesProfileAndStatsAndCaches()
		{
			_handler.Respond(IsProfileRequest, HttpStatusCode.OK, ProfileXml(), "text/xml");
			_handler.Respond(IsStatsRequest, HttpStatusCode.OK, StatsJson, "application/json");

			var result = await _provider.GetSteamUserDetails(SteamId);

			Assert.False(result.SteamUserException);
			Assert.False(result.UserStatsException);
			Assert.Equal("TestPlayer", result.SteamUser.steamID);
			Assert.Equal(2m, result.UserStats.killDeathRatio);
			Assert.Contains($"steamid={SteamId}", _handler.Requests.Single(IsStatsRequest).RequestUri!.Query);

			var second = await _provider.GetSteamUserDetails(SteamId);

			Assert.Same(result, second);
			Assert.Equal(2, _handler.Requests.Count);
		}

		[Fact]
		public async Task GetSteamUserDetails_WhenProfileRequestFails_FlagsSteamUserExceptionAndSkipsStats()
		{
			_handler.Respond(IsProfileRequest, HttpStatusCode.NotFound, "");

			var result = await _provider.GetSteamUserDetails(SteamId);

			Assert.True(result.SteamUserException);
			Assert.Null(result.SteamUser);
			Assert.DoesNotContain(_handler.Requests, IsStatsRequest);
		}

		[Fact]
		public async Task GetSteamUserDetails_WhenProfileIsNotXml_FlagsSteamUserException()
		{
			_handler.Respond(IsProfileRequest, HttpStatusCode.OK, "<html><body>Rate limited</body></html>", "text/html");

			var result = await _provider.GetSteamUserDetails(SteamId);

			Assert.True(result.SteamUserException);
			Assert.DoesNotContain(_handler.Requests, IsStatsRequest);
		}

		[Fact]
		public async Task GetSteamUserDetails_WhenStatsRequestFails_FlagsUserStatsExceptionAndDoesNotCache()
		{
			_handler.Respond(IsProfileRequest, HttpStatusCode.OK, ProfileXml(), "text/xml");
			_handler.Respond(IsStatsRequest, HttpStatusCode.Forbidden, "");

			var result = await _provider.GetSteamUserDetails(SteamId);

			Assert.False(result.SteamUserException);
			Assert.True(result.UserStatsException);
			Assert.Equal(SteamId, result.SteamUser.steamID64);
			Assert.False(_cache.TryGetValue(SteamId, out _));
		}

		[Fact]
		public async Task GetSteamUserDetails_WhenStatsArePrivate_FlagsUserStatsExceptionAndDoesNotCache()
		{
			_handler.Respond(IsProfileRequest, HttpStatusCode.OK, ProfileXml(), "text/xml");
			_handler.Respond(IsStatsRequest, HttpStatusCode.OK, PrivateStatsJson, "application/json");

			var result = await _provider.GetSteamUserDetails(SteamId);

			Assert.True(result.UserStatsException);
			await _provider.GetSteamUserDetails(SteamId);
			Assert.Equal(4, _handler.Requests.Count);
		}

		[Fact]
		public async Task GetSteamGroup_WhenCached_ReturnsCachedGroupWithoutRequest()
		{
			var group = new SteamGroup
			{
				groupID64 = "103582791429521408",
				members = new List<string> { "76561198000000001", "76561198000000002" }
			};
			_cache.Set("103582791429521408", group);

			var result = await _provider.GetSteamGroup("103582791429521408");

			Assert.Same(group, result);
			Assert.Empty(_handler.Requests);
		}

		[Fact]
		public async Task GetSteamGroup_OnCacheMiss_ParsesMemberListAndCaches()
		{
			_handler.Respond(IsGroupRequest, HttpStatusCode.OK, GroupXml("76561198000000001", "76561198000000002"), "text/xml");

			var result = await _provider.GetSteamGroup(GroupId);

			Assert.Equal(GroupId, result.groupID64);
			Assert.Equal("Bellum Gens", result.groupDetails.groupName);
			Assert.Equal(new[] { "76561198000000001", "76561198000000002" }, result.members);
			Assert.Equal(new Uri($"https://steamcommunity.com/gid/{GroupId}/memberslistxml/?xml=1"), Assert.Single(_handler.Requests).RequestUri);

			var second = await _provider.GetSteamGroup(GroupId);

			Assert.Same(result, second);
			Assert.Single(_handler.Requests);
		}

		[Fact]
		public async Task GetSteamGroup_WithNonSuccessStatus_ReturnsNullAndDoesNotCache()
		{
			_handler.Respond(IsGroupRequest, HttpStatusCode.NotFound, "");

			var result = await _provider.GetSteamGroup(GroupId);

			Assert.Null(result);
			Assert.False(_cache.TryGetValue(GroupId, out _));
		}

		[Fact]
		public async Task VerifyUserIsGroupAdmin_WhenUserIsFirstMember_ReturnsTrue()
		{
			_cache.Set("group1", new SteamGroup
			{
				members = new List<string> { "76561198000000001", "76561198000000002" }
			});

			var result = await _provider.VerifyUserIsGroupAdmin("76561198000000001", "group1");

			Assert.True(result);
		}

		[Fact]
		public async Task VerifyUserIsGroupAdmin_WhenUserIsNotFirstMember_ReturnsFalse()
		{
			_cache.Set("group1", new SteamGroup
			{
				members = new List<string> { "76561198000000001", "76561198000000002" }
			});

			var result = await _provider.VerifyUserIsGroupAdmin("76561198000000002", "group1");

			Assert.False(result);
		}

		[Fact]
		public async Task VerifyUserIsGroupAdmin_FetchedGroup_ChecksFirstMember()
		{
			_handler.Respond(IsGroupRequest, HttpStatusCode.OK, GroupXml("76561198000000001", "76561198000000002"), "text/xml");

			Assert.True(await _provider.VerifyUserIsGroupAdmin("76561198000000001", GroupId));
			Assert.False(await _provider.VerifyUserIsGroupAdmin("76561198000000002", GroupId));
		}

		// Regression: a failed group lookup returned null and group.members[0] threw NullReferenceException.
		[Fact]
		public async Task VerifyUserIsGroupAdmin_WhenGroupLookupFails_ReturnsFalse()
		{
			_handler.Respond(IsGroupRequest, HttpStatusCode.InternalServerError, "");

			var result = await _provider.VerifyUserIsGroupAdmin("76561198000000001", GroupId);

			Assert.False(result);
		}

		// Regression: an empty member list threw ArgumentOutOfRangeException.
		[Fact]
		public async Task VerifyUserIsGroupAdmin_WhenGroupHasNoMembers_ReturnsFalse()
		{
			_handler.Respond(IsGroupRequest, HttpStatusCode.OK, GroupXml(), "text/xml");

			var result = await _provider.VerifyUserIsGroupAdmin("76561198000000001", GroupId);

			Assert.False(result);
		}

		[Fact]
		public async Task VerifyUserIsGroupAdmin_WhenMemberListIsMissing_ReturnsFalse()
		{
			_cache.Set("group1", new SteamGroup { groupID64 = "group1", members = null! });

			var result = await _provider.VerifyUserIsGroupAdmin("76561198000000001", "group1");

			Assert.False(result);
		}

		[Fact]
		public void InvalidateUserCache_RemovesCachedUserStats()
		{
			_cache.Set("cachedUser", new UserStatsViewModel());

			_provider.InvalidateUserCache("cachedUser");

			Assert.False(_cache.TryGetValue("cachedUser", out _));
		}

		[Fact]
		public void InvalidateUserCache_KeepsEntriesThatAreNotUserStats()
		{
			var group = new SteamGroup { groupID64 = "group1" };
			_cache.Set("group1", group);

			_provider.InvalidateUserCache("group1");

			Assert.True(_cache.TryGetValue("group1", out var cached));
			Assert.Same(group, cached);
		}
	}

	/// <summary>
	/// In-memory HttpMessageHandler: answers requests from registered rules (first match wins) and records
	/// every request it sees. Unmatched requests fail the test instead of reaching the network.
	/// </summary>
	internal sealed class StubHttpMessageHandler : HttpMessageHandler
	{
		private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _rules = new();
		private readonly List<HttpRequestMessage> _requests = new();
		private readonly List<string?> _requestBodies = new();
		private readonly object _gate = new();

		public IReadOnlyList<HttpRequestMessage> Requests
		{
			get { lock (_gate) { return _requests.ToList(); } }
		}

		public IReadOnlyList<string?> RequestBodies
		{
			get { lock (_gate) { return _requestBodies.ToList(); } }
		}

		public void Respond(Func<HttpRequestMessage, bool> match, Func<HttpRequestMessage, HttpResponseMessage> respond)
		{
			lock (_gate) { _rules.Add((match, respond)); }
		}

		public void Respond(Func<HttpRequestMessage, bool> match, HttpStatusCode status, string body, string mediaType = "text/plain")
		{
			Respond(match, _ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
			Func<HttpRequestMessage, HttpResponseMessage>? respond;
			lock (_gate)
			{
				_requests.Add(request);
				_requestBodies.Add(body);
				respond = _rules.FirstOrDefault(r => r.Match(request)).Respond;
			}
			if (respond == null)
			{
				throw new InvalidOperationException($"No stubbed response for {request.Method} {request.RequestUri}");
			}
			var response = respond(request);
			response.RequestMessage = request;
			return response;
		}
	}
}
