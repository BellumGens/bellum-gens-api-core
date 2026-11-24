using BellumGens.Api.Core.Models;
using SteamModels;
using SteamModels.CSGO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
	public interface ISteamService
	{
		public Task<CSGOPlayerStats> GetStatsForCSGOUser(string username);
		public Task<SteamUser> GetSteamUser(string name);
		public Task<List<SteamUserSummary>> GetSteamUsersSummary(string users);
		public Task<UserStatsViewModel> GetSteamUserDetails(string name);
		public Task<SteamGroup> GetSteamGroup(string groupid);
		public Task<bool> VerifyUserIsGroupAdmin(string userid, string groupid);
		public Uri NormalizeUsername(string name);
		public string SteamUserId(string userUri);

		/// <summary>
		/// Builds the Dota 2 profile of a Steam account from its most recent matches.
		/// </summary>
		/// <param name="steamId64">The 64 bit steam id of the player.</param>
		/// <returns>The profile, or null when the id is not a steam id or the Steam API could not be reached.</returns>
		public Task<Dota2ProfileViewModel> GetDota2Profile(string steamId64);
	}
}
