using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
	public class SteamServiceProviderDota2Tests : IDisposable
	{
		private const string SteamId = "76561198012345678";
		private const uint AccountId = 52079950;

		private readonly MemoryCache _cache;
		private readonly StubHttpMessageHandler _handler;
		private readonly HttpClient _httpClient;
		private readonly SteamServiceProvider _provider;

		public SteamServiceProviderDota2Tests()
		{
			_cache = new MemoryCache(new MemoryCacheOptions());
			_handler = new StubHttpMessageHandler();
			_httpClient = new HttpClient(_handler);
			_provider = new SteamServiceProvider(_httpClient, _cache, TestUtils.CreateAppConfiguration(), TestUtils.CreateMockLogger<SteamServiceProvider>().Object);
		}

		public void Dispose()
		{
			_httpClient.Dispose();
			_handler.Dispose();
			_cache.Dispose();
			GC.SuppressFinalize(this);
		}

		#region Canned Steam responses

		// Match 101 is the older one, the player won it on Anti-Mage from the Radiant.
		// Match 102 is the newer one, the player lost it on Axe from the Dire.
		private const string HistoryJson = """
			{"result":{"status":1,"num_results":2,"total_results":2,"results_remaining":0,"matches":[
				{"match_id":101,"match_seq_num":1,"start_time":1700000000,"lobby_type":7,"radiant_team_id":0,"dire_team_id":0,
					"players":[{"account_id":52079950,"player_slot":0,"hero_id":1}]},
				{"match_id":102,"match_seq_num":2,"start_time":1700010000,"lobby_type":0,"radiant_team_id":0,"dire_team_id":0,
					"players":[{"account_id":52079950,"player_slot":128,"hero_id":2}]}]}}
			""";

		private const string PrivateHistoryJson = """
			{"result":{"status":15,"statusDetail":"Cannot get match history for a user that hasn't allowed it"}}
			""";

		private const string Match101Json = """
			{"result":{"players":[
				{"account_id":52079950,"player_slot":0,"hero_id":1,"kills":10,"deaths":2,"assists":6,"leaver_status":0,
					"last_hits":300,"denies":20,"gold_per_min":700,"xp_per_min":800,"level":25},
				{"account_id":4294967295,"player_slot":128,"hero_id":3,"kills":2,"deaths":10,"assists":1,"leaver_status":0,
					"last_hits":50,"denies":2,"gold_per_min":250,"xp_per_min":300,"level":14}],
				"radiant_win":true,"duration":2400,"start_time":1700000000,"match_id":101,"match_seq_num":1,"lobby_type":7,"game_mode":22}}
			""";

		private const string Match102Json = """
			{"result":{"players":[
				{"account_id":52079950,"player_slot":128,"hero_id":2,"kills":4,"deaths":6,"assists":8,"leaver_status":0,
					"last_hits":100,"denies":4,"gold_per_min":400,"xp_per_min":500,"level":18}],
				"radiant_win":true,"duration":1800,"start_time":1700010000,"match_id":102,"match_seq_num":2,"lobby_type":0,"game_mode":1}}
			""";

		private const string MatchNotFoundJson = """{"result":{"error":"Match ID not found"}}""";

		private const string HeroesJson = """
			{"result":{"heroes":[
				{"name":"npc_dota_hero_antimage","id":1,"localized_name":"Anti-Mage"},
				{"name":"npc_dota_hero_axe","id":2,"localized_name":"Axe"}],"status":200,"count":2}}
			""";

		private static bool IsHistoryRequest(HttpRequestMessage r) => r.RequestUri!.AbsolutePath.Contains("GetMatchHistory");
		private static bool IsDetailsRequest(HttpRequestMessage r) => r.RequestUri!.AbsolutePath.Contains("GetMatchDetails");
		private static bool IsDetailsRequest(HttpRequestMessage r, long matchId) => IsDetailsRequest(r) && r.RequestUri!.Query.Contains($"match_id={matchId}");
		private static bool IsHeroesRequest(HttpRequestMessage r) => r.RequestUri!.AbsolutePath.Contains("GetHeroes");

		private void RespondWithMatches()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, HistoryJson, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 101), HttpStatusCode.OK, Match101Json, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 102), HttpStatusCode.OK, Match102Json, "application/json");
			_handler.Respond(IsHeroesRequest, HttpStatusCode.OK, HeroesJson, "application/json");
		}

		#endregion

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("someuser")]
		[InlineData("123")]
		public async Task GetDota2Profile_WithInvalidSteamId_ReturnsNullWithoutRequest(string? steamId)
		{
			var result = await _provider.GetDota2Profile(steamId!);

			Assert.Null(result);
			Assert.Empty(_handler.Requests);
		}

		[Fact]
		public async Task GetDota2Profile_RequestsTheMatchHistoryOfTheAccount()
		{
			RespondWithMatches();

			await _provider.GetDota2Profile(SteamId);

			var query = _handler.Requests.Single(IsHistoryRequest).RequestUri!.Query;
			Assert.Contains($"account_id={AccountId}", query);
			Assert.Contains($"matches_requested={SteamServiceProvider.Dota2RecentMatchCount}", query);
			Assert.Contains("key=test", query);
		}

		[Fact]
		public async Task GetDota2Profile_AggregatesTheRecentMatches()
		{
			RespondWithMatches();

			var result = await _provider.GetDota2Profile(SteamId);

			Assert.NotNull(result);
			Assert.False(result.MatchHistoryPrivate);
			Assert.Equal(SteamId, result.SteamId);
			Assert.Equal(AccountId, result.AccountId);
			Assert.Equal(2, result.Matches);
			Assert.Equal(1, result.Wins);
			Assert.Equal(1, result.Losses);
			Assert.Equal(50m, result.WinRate);
			// (14 kills + 14 assists) / 8 deaths
			Assert.Equal(3.5m, result.Kda);
			Assert.Equal(550m, result.AverageGoldPerMinute);
			Assert.Equal(650m, result.AverageExperiencePerMinute);
			Assert.Equal(200m, result.AverageLastHits);
		}

		[Fact]
		public async Task GetDota2Profile_ListsTheMatchesNewestFirstFromThePlayerPerspective()
		{
			RespondWithMatches();

			var result = await _provider.GetDota2Profile(SteamId);

			Assert.Equal(new long[] { 102, 101 }, result!.RecentMatches.Select(m => m.MatchId));

			var lost = result.RecentMatches[0];
			Assert.False(lost.Won);
			Assert.Equal(2, lost.HeroId);
			Assert.Equal("Axe", lost.HeroName);
			Assert.Equal("https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/axe.png", lost.HeroImageUrl);
			Assert.Equal((4, 6, 8), (lost.Kills, lost.Deaths, lost.Assists));
			Assert.Equal(1800, lost.Duration);
			Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700010000), lost.StartTime);

			var won = result.RecentMatches[1];
			Assert.True(won.Won);
			Assert.Equal("Anti-Mage", won.HeroName);
			Assert.Equal(22, won.GameMode);
			Assert.Equal(7, won.LobbyType);
		}

		[Fact]
		public async Task GetDota2Profile_ListsTheMostPlayedHeroesWithTheirNamesAndPortraits()
		{
			RespondWithMatches();

			var result = await _provider.GetDota2Profile(SteamId);

			Assert.Collection(result!.Heroes,
				hero =>
				{
					// Equal play counts, so the hero with more wins goes first.
					Assert.Equal("Anti-Mage", hero.HeroName);
					Assert.Equal("https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/antimage.png", hero.HeroImageUrl);
					Assert.Equal((1, 1, 0), (hero.Matches, hero.Wins, hero.Losses));
					Assert.Equal(100m, hero.WinRate);
				},
				hero =>
				{
					Assert.Equal("Axe", hero.HeroName);
					Assert.Equal(0m, hero.WinRate);
				});
		}

		[Fact]
		public async Task GetDota2Profile_WhenMatchHistoryIsHidden_ReturnsAPrivateProfileWithoutMatchRequests()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, PrivateHistoryJson, "application/json");

			var result = await _provider.GetDota2Profile(SteamId);

			Assert.NotNull(result);
			Assert.True(result.MatchHistoryPrivate);
			Assert.Equal(0, result.Matches);
			Assert.Empty(result.RecentMatches);
			Assert.Single(_handler.Requests);
		}

		[Fact]
		public async Task GetDota2Profile_WithoutMatches_ReturnsAnEmptyProfile()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, """{"result":{"status":1,"num_results":0,"total_results":0,"results_remaining":0,"matches":[]}}""", "application/json");
			_handler.Respond(IsHeroesRequest, HttpStatusCode.OK, HeroesJson, "application/json");

			var result = await _provider.GetDota2Profile(SteamId);

			Assert.NotNull(result);
			Assert.False(result.MatchHistoryPrivate);
			Assert.Equal(0, result.Matches);
			Assert.Equal(0m, result.WinRate);
			Assert.Empty(result.Heroes);
			Assert.DoesNotContain(_handler.Requests, IsDetailsRequest);
		}

		[Theory]
		[InlineData(HttpStatusCode.ServiceUnavailable, "busy")]
		[InlineData(HttpStatusCode.OK, "<html>not json</html>")]
		[InlineData(HttpStatusCode.OK, """{"result":{"status":8,"statusDetail":"Error retrieving match data."}}""")]
		public async Task GetDota2Profile_WhenTheMatchHistoryFails_ReturnsNullAndDoesNotCache(HttpStatusCode status, string body)
		{
			_handler.Respond(IsHistoryRequest, status, body, "application/json");

			Assert.Null(await _provider.GetDota2Profile(SteamId));
			Assert.Null(await _provider.GetDota2Profile(SteamId));

			Assert.Equal(2, _handler.Requests.Count(IsHistoryRequest));
		}

		[Fact]
		public async Task GetDota2Profile_SkipsMatchesWhoseDetailsCouldNotBeRetrieved()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, HistoryJson, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 101), HttpStatusCode.OK, Match101Json, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 102), HttpStatusCode.OK, MatchNotFoundJson, "application/json");
			_handler.Respond(IsHeroesRequest, HttpStatusCode.OK, HeroesJson, "application/json");

			var result = await _provider.GetDota2Profile(SteamId);

			Assert.Equal(1, result!.Matches);
			Assert.Equal(101, Assert.Single(result.RecentMatches).MatchId);
		}

		[Fact]
		public async Task GetDota2Profile_WhenTheHeroListFails_StillReturnsTheProfileWithoutHeroNames()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, HistoryJson, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 101), HttpStatusCode.OK, Match101Json, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 102), HttpStatusCode.OK, Match102Json, "application/json");
			_handler.Respond(IsHeroesRequest, HttpStatusCode.InternalServerError, "error");

			var result = await _provider.GetDota2Profile(SteamId);

			Assert.Equal(2, result!.Matches);
			Assert.All(result.RecentMatches, m =>
			{
				Assert.Null(m.HeroName);
				Assert.Null(m.HeroImageUrl);
			});
			Assert.False(_cache.TryGetValue(SteamServiceProvider.Dota2HeroesCacheKey, out _));
		}

		[Fact]
		public async Task GetDota2Profile_CachesTheProfile()
		{
			RespondWithMatches();

			var first = await _provider.GetDota2Profile(SteamId);
			int requests = _handler.Requests.Count;
			var second = await _provider.GetDota2Profile(SteamId);

			Assert.Same(first, second);
			Assert.Equal(requests, _handler.Requests.Count);
		}

		[Fact]
		public async Task GetDota2Profile_ReusesCachedMatchesAndHeroesWhenTheProfileIsRebuilt()
		{
			RespondWithMatches();

			await _provider.GetDota2Profile(SteamId);
			_cache.Remove(SteamServiceProvider.Dota2ProfileCacheKey(AccountId));
			await _provider.GetDota2Profile(SteamId);

			Assert.Equal(2, _handler.Requests.Count(IsHistoryRequest));
			Assert.Equal(1, _handler.Requests.Count(r => IsDetailsRequest(r, 101)));
			Assert.Equal(1, _handler.Requests.Count(r => IsDetailsRequest(r, 102)));
			Assert.Equal(1, _handler.Requests.Count(IsHeroesRequest));
		}

		[Fact]
		public async Task GetDota2Profile_DoesNotCacheAProfileWithMissingMatches()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, HistoryJson, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 101), HttpStatusCode.OK, Match101Json, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 102), HttpStatusCode.ServiceUnavailable, "busy");
			_handler.Respond(IsHeroesRequest, HttpStatusCode.OK, HeroesJson, "application/json");

			await _provider.GetDota2Profile(SteamId);
			await _provider.GetDota2Profile(SteamId);

			Assert.False(_cache.TryGetValue(SteamServiceProvider.Dota2ProfileCacheKey(AccountId), out _));
			Assert.Equal(2, _handler.Requests.Count(IsHistoryRequest));
			// The match that was found is reused; only the missing one is asked for again
			Assert.Equal(1, _handler.Requests.Count(r => IsDetailsRequest(r, 101)));
			Assert.Equal(2, _handler.Requests.Count(r => IsDetailsRequest(r, 102)));
		}

		[Fact]
		public async Task GetDota2Profile_DoesNotCacheAProfileWithoutHeroNames()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, HistoryJson, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 101), HttpStatusCode.OK, Match101Json, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 102), HttpStatusCode.OK, Match102Json, "application/json");
			_handler.Respond(IsHeroesRequest, HttpStatusCode.InternalServerError, "error");

			await _provider.GetDota2Profile(SteamId);

			Assert.False(_cache.TryGetValue(SteamServiceProvider.Dota2ProfileCacheKey(AccountId), out _));
		}

		[Fact]
		public async Task GetDota2Profile_DoesNotCacheAPrivateProfile()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, PrivateHistoryJson, "application/json");

			await _provider.GetDota2Profile(SteamId);
			await _provider.GetDota2Profile(SteamId);

			Assert.False(_cache.TryGetValue(SteamServiceProvider.Dota2ProfileCacheKey(AccountId), out _));
			Assert.Equal(2, _handler.Requests.Count(IsHistoryRequest));
		}

		[Fact]
		public async Task GetDota2Profile_DoesNotCacheAMatchThatCameBackAsAnError()
		{
			_handler.Respond(IsHistoryRequest, HttpStatusCode.OK, HistoryJson, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 101), HttpStatusCode.OK, Match101Json, "application/json");
			_handler.Respond(r => IsDetailsRequest(r, 102), HttpStatusCode.OK, MatchNotFoundJson, "application/json");
			_handler.Respond(IsHeroesRequest, HttpStatusCode.OK, HeroesJson, "application/json");

			await _provider.GetDota2Profile(SteamId);

			Assert.True(_cache.TryGetValue(SteamServiceProvider.Dota2MatchCacheKey(101), out _));
			Assert.False(_cache.TryGetValue(SteamServiceProvider.Dota2MatchCacheKey(102), out _));
		}
	}
}
