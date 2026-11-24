using SteamModels;
using System.Net.Http;
using System.Xml.Serialization;
using System.Collections.Generic;
using SteamModels.CSGO;
using SteamModels.Dota2;
using BellumGens.Api.Core.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BellumGens.Api.Core.Providers
{
	public class SteamServiceProvider : ISteamService
	{
		private readonly HttpClient _client;
		private readonly IMemoryCache _cache;
		private readonly Dota2Cache _dota2Cache;
		private readonly AppConfiguration _appInfo;
		private readonly ILogger<SteamServiceProvider> _logger;

		private static readonly string _statsForGameUrl = "https://api.steampowered.com/ISteamUserStats/GetUserStatsForGame/v0002/?appid={0}&key={1}&steamid={2}&format=json";
		private static readonly string _steamUserUrl = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}";
		private static readonly string _playerDetailsById = "https://steamcommunity.com/profiles/{0}/?xml=1";
		private static readonly string _playerDetailsByUrl = "https://steamcommunity.com/id/{0}/?xml=1";
		private static readonly string _groupMembersUrl = "https://steamcommunity.com/gid/{0}/memberslistxml/?xml=1";
		private static readonly string _dota2MatchHistoryUrl = "https://api.steampowered.com/IDOTA2Match_570/GetMatchHistory/v1/?key={0}&account_id={1}&matches_requested={2}";
		private static readonly string _dota2MatchDetailsUrl = "https://api.steampowered.com/IDOTA2Match_570/GetMatchDetails/v1/?key={0}&match_id={1}";
		private static readonly string _dota2HeroesUrl = "https://api.steampowered.com/IEconDOTA2_570/GetHeroes/v1/?key={0}&language=english";

		/// <summary>
		/// The number of most recent matches a Dota 2 profile is built from.
		/// </summary>
		public const int Dota2RecentMatchCount = 20;

		/// <summary>
		/// The status GetMatchHistory returns for a player who has hidden their match data.
		/// </summary>
		public const int Dota2MatchHistoryPrivateStatus = 15;

		/// <summary>
		/// How many Dota 2 match details are requested from the Steam API at the same time.
		/// </summary>
		private const int _dota2MaxConcurrentRequests = 4;

		// Kept as long as the Counter-Strike stats (see GetSteamUserDetails), so a player page costs one
		// round of Steam calls per account every few days.
		private static readonly TimeSpan _dota2ProfileCacheDuration = TimeSpan.FromDays(5);
		// A profile built while Steam answered some of its matches with an error is kept for a short while, so a
		// match Steam never has details for doesn't make every view of the profile call the Steam API again.
		private static readonly TimeSpan _dota2PartialProfileCacheDuration = TimeSpan.FromHours(1);
		// Finished matches never change, but a match is only looked up again when a profile with it is rebuilt,
		// so one that isn't used for a while is let go.
		private static readonly TimeSpan _dota2MatchCacheDuration = TimeSpan.FromDays(30);
		private static readonly TimeSpan _dota2MatchSlidingCacheDuration = TimeSpan.FromDays(2);
		// An error result (e.g. "Match ID not found") is kept as long as the profile built with it.
		private static readonly TimeSpan _dota2MatchErrorCacheDuration = _dota2PartialProfileCacheDuration;
		private static readonly TimeSpan _dota2HeroesCacheDuration = TimeSpan.FromDays(7);

		public const string Dota2HeroesCacheKey = "steam:dota2:heroes";
		public static string Dota2ProfileCacheKey(uint accountId) => $"steam:dota2:profile:{accountId}";
		public static string Dota2MatchCacheKey(long matchId) => $"steam:dota2:match:{matchId}";

		/// <summary>
		/// Name of the rate limiting policy on the anonymous Dota 2 profile endpoint. Building a profile that isn't
		/// cached costs up to <see cref="Dota2RecentMatchCount"/> + 2 calls against the shared Steam API key.
		/// </summary>
		public const string Dota2RateLimitPolicy = "steam-dota2";

		//private static readonly string _steamAppNewsUrl = "https://api.steampowered.com/ISteamNews/GetNewsForApp/v0002/?appid={0}&maxlength=300&format=json";

		public SteamServiceProvider(HttpClient client, IMemoryCache cache, Dota2Cache dota2Cache, AppConfiguration appInfo, ILogger<SteamServiceProvider> logger)
		{
			_client = client;
			_cache = cache;
			_dota2Cache = dota2Cache;
			_appInfo = appInfo;
			_logger = logger;
		}

		public async Task<CSGOPlayerStats> GetStatsForCSGOUser(string username)
		{
			Uri endpoint = new(string.Format(_statsForGameUrl, _appInfo.Config.CSGOGameId, _appInfo.Config.SteamApiKey, username));
			var statsForGameResponse = await _client.GetStringAsync(endpoint);
			return JsonSerializer.Deserialize<CSGOPlayerStats>(statsForGameResponse);
		}

		public async Task<SteamUser> GetSteamUser(string name)
		{
			if (_cache.Get(name) is UserStatsViewModel)
			{
				UserStatsViewModel viewModel = _cache.Get(name) as UserStatsViewModel;
				return viewModel.SteamUser;
			}

			await using var playerDetailsResponse = await _client.GetStreamAsync(NormalizeUsername(name));
			XmlSerializer serializer = new(typeof(SteamUser));
			return (SteamUser)serializer.Deserialize(playerDetailsResponse);
		}

		public async Task<List<SteamUserSummary>> GetSteamUsersSummary(string users)
		{
			var playerDetailsResponse = await _client.GetStringAsync(string.Format(_steamUserUrl, _appInfo.Config.SteamApiKey, users));
			SteamUsersSummary result = JsonSerializer.Deserialize<SteamUsersSummary>(playerDetailsResponse);
			return result.response.players;
		}

		public async Task<UserStatsViewModel> GetSteamUserDetails(string name)
		{
			if (_cache.Get(name) is UserStatsViewModel)
			{
				return _cache.Get(name) as UserStatsViewModel;
			}

			UserStatsViewModel model = new();
			using (var playerDetailsResponse = await _client.GetAsync(NormalizeUsername(name)))
			{
				if (playerDetailsResponse.IsSuccessStatusCode)
				{
					XmlSerializer serializer = new(typeof(SteamUser));

					try
					{
						model.SteamUser = (SteamUser)serializer.Deserialize(await playerDetailsResponse.Content.ReadAsStreamAsync());
					}
					catch
					{
						model.SteamUserException = true;
						return model;
					}
				}
				else
				{
					model.SteamUserException = true;
					return model;
				}
			}

			Uri endpoint = new(string.Format(_statsForGameUrl, _appInfo.Config.CSGOGameId, _appInfo.Config.SteamApiKey, model.SteamUser.steamID64));
			using var statsForGameResponse = await _client.GetAsync(endpoint);
			if (statsForGameResponse.IsSuccessStatusCode)
			{
				try
				{
					model.UserStats = JsonSerializer.Deserialize<CSGOPlayerStats>(await statsForGameResponse.Content.ReadAsStringAsync());
					// Private profiles can come back as a successful response carrying an error payload;
					// the derived stats would read as zeros and overwrite the stored ones.
					if (model.UserStats?.playerstats?.success != true)
					{
						model.UserStatsException = true;
						return model;
					}
					_cache.Set(name, model, DateTime.Now.AddDays(5));
					return model;
				}
				catch
				{
					model.UserStatsException = true;
					return model;
				}
			}
			model.UserStatsException = true;
			return model;
		}

		public async Task<SteamGroup> GetSteamGroup(string groupid)
		{
			if (_cache.Get(groupid) is SteamGroup)
			{
				return _cache.Get(groupid) as SteamGroup;
			}

			SteamGroup group = null;
			using var playerDetailsResponse = await _client.GetAsync(string.Format(_groupMembersUrl, groupid));
			if (playerDetailsResponse.IsSuccessStatusCode)
			{
				XmlSerializer serializer = new(typeof(SteamGroup));
				group = (SteamGroup)serializer.Deserialize(await playerDetailsResponse.Content.ReadAsStreamAsync());

				_cache.Set(groupid, group, DateTime.Now.AddDays(7));
			}

			return group;
		}

		public async Task<bool> VerifyUserIsGroupAdmin(string userid, string groupid)
		{
			SteamGroup group = await GetSteamGroup(groupid);
			// The group owner is listed first. A failed group lookup (null) or an empty member
			// list means ownership can't be verified.
			if (group?.members is not { Count: > 0 } members)
			{
				return false;
			}
			return members[0] == userid;
		}

		public async Task<Dota2ProfileViewModel> GetDota2Profile(string steamId64)
		{
			if (!Dota2Ids.TryGetAccountId(steamId64, out uint accountId))
			{
				return null;
			}

			string cacheKey = Dota2ProfileCacheKey(accountId);
			if (_dota2Cache.TryGetValue(cacheKey, out Dota2ProfileViewModel cached))
			{
				return cached;
			}

			Uri historyEndpoint = new(string.Format(_dota2MatchHistoryUrl, _appInfo.Config.SteamApiKey, accountId, Dota2RecentMatchCount));
			Dota2MatchHistoryResult history = (await GetSteamApiJson<Dota2MatchHistory>(historyEndpoint))?.result;
			if (history == null)
			{
				return null;
			}

			if (history.status == Dota2MatchHistoryPrivateStatus)
			{
				// Like private Counter-Strike stats, a hidden match history isn't cached, so the stats show up
				// as soon as the player makes their match data public.
				return Dota2ProfileViewModel.Private(steamId64, accountId);
			}
			if (!history.success)
			{
				_logger.LogWarning("Dota 2 match history for account {AccountId} returned status {Status}: {StatusDetail}", accountId, history.status, history.statusDetail);
				return null;
			}

			List<long> matchIds = (history.matches ?? new List<Dota2MatchSummary>())
				.OrderByDescending(m => m.start_time)
				.Select(m => m.match_id)
				.Take(Dota2RecentMatchCount)
				.ToList();
			Dota2MatchDetailsResult[] matches = await GetDota2MatchDetails(matchIds);
			Dictionary<int, Dota2Hero> heroes = await GetDota2Heroes();
			Dota2ProfileViewModel profile = Dota2ProfileViewModel.Create(steamId64, accountId, matches, heroes ?? new Dictionary<int, Dota2Hero>());

			// A profile built while some lookups couldn't reach Steam is still returned, but it isn't cached: the next
			// request rebuilds it, and only what's missing is requested again (the matches and heroes found are cached).
			// Matches Steam answered with an error may never have details, so that profile is kept for a short while.
			if (heroes != null && matches.All(m => m != null))
			{
				TimeSpan duration = matches.All(m => m.success) ? _dota2ProfileCacheDuration : _dota2PartialProfileCacheDuration;
				_dota2Cache.Set(cacheKey, profile, new MemoryCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = duration,
					Size = Dota2Cache.ProfileSize
				});
			}
			return profile;
		}

		private async Task<Dota2MatchDetailsResult[]> GetDota2MatchDetails(IEnumerable<long> matchIds)
		{
			using SemaphoreSlim gate = new(_dota2MaxConcurrentRequests);
			return await Task.WhenAll(matchIds.Select(async matchId =>
			{
				await gate.WaitAsync();
				try
				{
					return await GetDota2MatchDetails(matchId);
				}
				finally
				{
					gate.Release();
				}
			}));
		}

		private async Task<Dota2MatchDetailsResult> GetDota2MatchDetails(long matchId)
		{
			string cacheKey = Dota2MatchCacheKey(matchId);
			if (_dota2Cache.TryGetValue(cacheKey, out Dota2MatchDetailsResult cached))
			{
				return cached;
			}

			Uri endpoint = new(string.Format(_dota2MatchDetailsUrl, _appInfo.Config.SteamApiKey, matchId));
			Dota2MatchDetailsResult match = (await GetSteamApiJson<Dota2MatchDetails>(endpoint))?.result;
			if (match == null)
			{
				// Steam couldn't be reached; the next request asks again.
				return null;
			}

			if (match.success)
			{
				TrimMatchDetails(match);
				_dota2Cache.Set(cacheKey, match, new MemoryCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = _dota2MatchCacheDuration,
					SlidingExpiration = _dota2MatchSlidingCacheDuration,
					Priority = CacheItemPriority.Low,
					Size = Dota2Cache.MatchSize
				});
			}
			else
			{
				// Steam answered, but with an error, which asking again right away won't change.
				_dota2Cache.Set(cacheKey, match, new MemoryCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = _dota2MatchErrorCacheDuration,
					Priority = CacheItemPriority.Low,
					Size = Dota2Cache.ProfileSize
				});
			}
			return match;
		}

		/// <summary>
		/// Drops the parts of the match details a profile doesn't use, so a cached match takes less memory.
		/// </summary>
		private static void TrimMatchDetails(Dota2MatchDetailsResult match)
		{
			match.picks_bans = null;
			foreach (Dota2MatchPlayer player in match.players ?? Enumerable.Empty<Dota2MatchPlayer>())
			{
				player.ability_upgrades = null;
				player.additional_units = null;
			}
		}

		/// <returns>The heroes by id, or null when the Steam API could not be reached.</returns>
		private async Task<Dictionary<int, Dota2Hero>> GetDota2Heroes()
		{
			if (_dota2Cache.TryGetValue(Dota2HeroesCacheKey, out Dictionary<int, Dota2Hero> cached))
			{
				return cached;
			}

			Uri endpoint = new(string.Format(_dota2HeroesUrl, _appInfo.Config.SteamApiKey));
			List<Dota2Hero> list = (await GetSteamApiJson<Dota2Heroes>(endpoint))?.result?.heroes;
			if (list == null)
			{
				// Profiles still render without hero names; don't cache the failure.
				return null;
			}

			Dictionary<int, Dota2Hero> heroes = list.GroupBy(h => h.id).ToDictionary(g => g.Key, g => g.First());
			_dota2Cache.Set(Dota2HeroesCacheKey, heroes, new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = _dota2HeroesCacheDuration,
				// Every profile uses the hero list, so it's the last thing dropped when the cache is full.
				Priority = CacheItemPriority.High,
				Size = Dota2Cache.HeroesSize
			});
			return heroes;
		}

		/// <summary>
		/// GETs and deserializes a Steam API response, returning null when the request or the parsing fails.
		/// </summary>
		private async Task<T> GetSteamApiJson<T>(Uri endpoint) where T : class
		{
			try
			{
				using HttpResponseMessage response = await _client.GetAsync(endpoint);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogWarning("Steam API request {Path} failed with {StatusCode}", endpoint.AbsolutePath, (int)response.StatusCode);
					return null;
				}
				return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
			}
			catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
			{
				_logger.LogWarning(ex, "Steam API request {Path} failed", endpoint.AbsolutePath);
				return null;
			}
		}

		public void InvalidateUserCache(string name)
		{
			if (_cache.Get(name) is UserStatsViewModel)
			{
				_cache.Remove(name);
			}
		}

		//public static SteamAppNews GetSteamAppNews(int appid)
		//{
		//	HttpClient client = new HttpClient();
		//	var playerDetailsResponse = client.GetStreamAsync(_steamAppNewsUrl);
		//	XmlSerializer serializer = new XmlSerializer(typeof(SteamAppNews));
		//	SteamAppNews news = (SteamAppNews)serializer.Deserialize(playerDetailsResponse.Result);
		//	return news;
		//}

		//public static SteamNews GetSteamAppNewsJSON(int appid)
		//{
		//	HttpClient client = new HttpClient();
		//	var steamnews = client.GetStringAsync(_steamAppNewsUrl);
		//	SteamNews news = JsonConvert.DeserializeObject<SteamNews>(steamnews.Result);
		//	return news;
		//}

		public Uri NormalizeUsername(string name)
		{
			string pattern = "^[0-9]{17}$",
					url = "^http(s)?://steamcommunity.com";
			return Regex.IsMatch(name, url) ?
					new Uri(name + "/?xml=1") :
					Regex.IsMatch(name, pattern) ?
						new Uri(string.Format(_playerDetailsById, name)) :
						new Uri(string.Format(_playerDetailsByUrl, name));
		}

		public string SteamUserId(string userUri)
		{
			var parts = userUri.Split('/');
			return parts.Length >= 6 ? parts[5] : null;
		}
	}
}
