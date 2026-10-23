using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StarCraft2Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
	public class BattleNetServiceProvider : IBattleNetService
	{
		private readonly HttpClient _client;
		private readonly IMemoryCache _cache;
		private readonly BattleNetTokenCache _tokenCache;
		private readonly ILogger<BattleNetServiceProvider> _logger;
		private readonly string _clientId;
		private readonly string _secret;

		private static readonly string _playerAccountEndpoint = "https://{0}.api.blizzard.com/sc2/player/{1}";
		private static readonly string _profileEndpoint = "https://{0}.api.blizzard.com/sc2/metadata/profile/{1}/{2}/{3}?locale=en_US";
		private static readonly string _tokenEndpoint = "https://oauth.battle.net/token";

		public BattleNetServiceProvider(HttpClient client, IMemoryCache cache, IConfiguration config, BattleNetTokenCache tokenCache, ILogger<BattleNetServiceProvider> logger)
		{
			_client = client;
			_cache = cache;
			_tokenCache = tokenCache;
			_logger = logger;
			_clientId = config.GetValue<string>("battleNet:clientId");
			_secret = config.GetValue<string>("battleNet:secret");
		}

		public static string PlayerCacheKey(string playerid, string region) => $"battlenet:sc2:player:{region}:{playerid}";

		public static string PlayerProfileCacheKey(string playerid, string region, int regionid, int realmid) => $"battlenet:sc2:profile:{region}:{regionid}:{realmid}:{playerid}";

		public async Task<Player> GetStarCraft2Player(string playerid, string region = "eu")
		{
			string cacheKey = PlayerCacheKey(playerid, region);
			if (_cache.TryGetValue(cacheKey, out Player cached))
			{
				return cached;
			}

			string accessToken = await _tokenCache.GetAccessTokenAsync(RequestAccessTokenAsync);
			if (accessToken == null)
			{
				_logger.LogWarning("Could not obtain a battle.net access token; StarCraft 2 player {PlayerId} was not retrieved.", playerid);
				return null;
			}

			Uri endpoint = new(string.Format(_playerAccountEndpoint, region, playerid));
			using var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
			requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			using var response = await _client.SendAsync(requestMessage);
			if (!response.IsSuccessStatusCode)
			{
				HandleFailedApiCall(response.StatusCode, playerid);
				return null;
			}

			var responseString = await response.Content.ReadAsStringAsync();
			Player[] players = JsonSerializer.Deserialize<Player[]>(responseString);
			Player player = players is { Length: > 0 } ? players[0] : null;
			if (player != null)
			{
				_cache.Set(cacheKey, player, DateTime.Now.AddDays(1));
			}
			return player;
		}

		public async Task<PlayerProfile> GetStarCraft2PlayerProfile(string playerid, string region = "eu", int regionid = 2, int realmid = 1)
		{
			string cacheKey = PlayerProfileCacheKey(playerid, region, regionid, realmid);
			if (_cache.TryGetValue(cacheKey, out PlayerProfile cached))
			{
				return cached;
			}

			string accessToken = await _tokenCache.GetAccessTokenAsync(RequestAccessTokenAsync);
			if (accessToken == null)
			{
				_logger.LogWarning("Could not obtain a battle.net access token; StarCraft 2 profile {PlayerId} was not retrieved.", playerid);
				return null;
			}

			Uri endpoint = new(string.Format(_profileEndpoint, region, regionid, realmid, playerid));
			using var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
			requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			using var response = await _client.SendAsync(requestMessage);
			if (!response.IsSuccessStatusCode)
			{
				HandleFailedApiCall(response.StatusCode, playerid);
				return null;
			}

			var responseString = await response.Content.ReadAsStringAsync();
			PlayerProfile profile = JsonSerializer.Deserialize<PlayerProfile>(responseString);
			if (profile != null)
			{
				_cache.Set(cacheKey, profile, DateTime.Now.AddDays(1));
			}
			return profile;
		}

		private void HandleFailedApiCall(HttpStatusCode statusCode, string playerid)
		{
			if (statusCode == HttpStatusCode.Unauthorized)
			{
				// The token was rejected (revoked or expired early); force a new one on the next call.
				_tokenCache.Invalidate();
			}
			_logger.LogWarning("battle.net API returned {StatusCode} for StarCraft 2 player {PlayerId}.", (int)statusCode, playerid);
		}

		private async Task<BattleNetAccessToken> RequestAccessTokenAsync()
		{
			using var requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(_tokenEndpoint));
			requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_secret}")));
			requestMessage.Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("grant_type", "client_credentials")
			});

			try
			{
				using var response = await _client.SendAsync(requestMessage);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogWarning("battle.net token endpoint returned {StatusCode}.", (int)response.StatusCode);
					return null;
				}

				var responseString = await response.Content.ReadAsStringAsync();
				OAuthResponse oauth = JsonSerializer.Deserialize<OAuthResponse>(responseString);
				if (string.IsNullOrEmpty(oauth?.access_token))
				{
					_logger.LogWarning("battle.net token endpoint returned a response without an access token.");
					return null;
				}
				return new BattleNetAccessToken(oauth.access_token, TimeSpan.FromSeconds(oauth.expires_in));
			}
			catch (HttpRequestException exception)
			{
				_logger.LogWarning(exception, "battle.net token request failed.");
				return null;
			}
			catch (JsonException exception)
			{
				_logger.LogWarning(exception, "battle.net token response could not be parsed.");
				return null;
			}
		}

		private class OAuthResponse
		{
#pragma warning disable IDE1006 // Naming Styles
			public string access_token { get; set; }
			public string token_type { get; set; }
			public int expires_in { get; set; }
			public string scope { get; set; }
#pragma warning restore IDE1006 // Naming Styles
		}
	}

	/// <summary>An OAuth access token and how long it is valid for from the moment it was requested.</summary>
	public sealed record BattleNetAccessToken(string AccessToken, TimeSpan ExpiresIn);

	/// <summary>
	/// Holds the battle.net client-credentials token for the lifetime of the application (register as a
	/// singleton). Refreshes are serialized so concurrent requests share one token request.
	/// </summary>
	public sealed class BattleNetTokenCache
	{
		// Treat the token as expired slightly early so it isn't sent while expiring in flight.
		public static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromSeconds(60);

		private readonly TimeProvider _timeProvider;
		private readonly SemaphoreSlim _refreshLock = new(1, 1);
		private volatile CachedToken _current;

		public BattleNetTokenCache(TimeProvider timeProvider)
		{
			_timeProvider = timeProvider ?? TimeProvider.System;
		}

		/// <summary>
		/// Returns the cached token while it is valid; otherwise calls <paramref name="requestToken"/> for a new one.
		/// Returns null when a new token was needed but could not be obtained.
		/// </summary>
		public async Task<string> GetAccessTokenAsync(Func<Task<BattleNetAccessToken>> requestToken)
		{
			CachedToken current = _current;
			if (IsValid(current))
			{
				return current.AccessToken;
			}

			await _refreshLock.WaitAsync();
			try
			{
				current = _current;
				if (IsValid(current))
				{
					return current.AccessToken;
				}

				// Expiry is measured from when the token was requested, never from later API calls.
				DateTimeOffset requestedAt = _timeProvider.GetUtcNow();
				BattleNetAccessToken token = await requestToken();
				if (token == null || string.IsNullOrEmpty(token.AccessToken))
				{
					return null;
				}

				_current = new CachedToken(token.AccessToken, requestedAt + token.ExpiresIn - ExpirySafetyMargin);
				return token.AccessToken;
			}
			finally
			{
				_refreshLock.Release();
			}
		}

		public void Invalidate()
		{
			_current = null;
		}

		private bool IsValid(CachedToken token) => token != null && _timeProvider.GetUtcNow() < token.ExpiresAt;

		private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
	}
}
