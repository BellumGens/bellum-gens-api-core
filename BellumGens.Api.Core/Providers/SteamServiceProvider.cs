using SteamModels;
using System.Net.Http;
using System.Xml.Serialization;
using System.Collections.Generic;
using SteamModels.CSGO;
using BellumGens.Api.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace BellumGens.Api.Core.Providers
{
	public class SteamServiceProvider : ISteamService
	{
		private readonly HttpClient _client;
		private readonly IMemoryCache _cache;
		private readonly AppConfiguration _appInfo;

		private static readonly string _statsForGameUrl = "https://api.steampowered.com/ISteamUserStats/GetUserStatsForGame/v0002/?appid={0}&key={1}&steamid={2}&format=json";
		private static readonly string _steamUserUrl = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}";
		private static readonly string _playerDetailsById = "https://steamcommunity.com/profiles/{0}/?xml=1";
		private static readonly string _playerDetailsByUrl = "https://steamcommunity.com/id/{0}/?xml=1";
		private static readonly string _groupMembersUrl = "https://steamcommunity.com/gid/{0}/memberslistxml/?xml=1";

		//private static readonly string _steamAppNewsUrl = "https://api.steampowered.com/ISteamNews/GetNewsForApp/v0002/?appid={0}&maxlength=300&format=json";

		public SteamServiceProvider(HttpClient client, IMemoryCache cache, AppConfiguration appInfo)
		{
			_client = client;
			_cache = cache;
			_appInfo = appInfo;
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
