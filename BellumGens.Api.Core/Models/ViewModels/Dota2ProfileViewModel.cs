using SteamModels.Dota2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BellumGens.Api.Core.Models
{
	/// <summary>
	/// The Dota 2 profile of a Steam account, aggregated from its most recent public matches.
	/// </summary>
	public class Dota2ProfileViewModel
	{
		/// <summary>
		/// The steam cdn url hero portraits are served from, formatted with <see cref="Dota2Hero.shortName"/>.
		/// </summary>
		public const string HeroImageUrl = "https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/{0}.png";

		/// <summary>
		/// The number of most played heroes listed on the profile.
		/// </summary>
		public const int TopHeroCount = 5;

		public string SteamId { get; set; }
		public uint AccountId { get; set; }

		/// <summary>
		/// The player has hidden their match data in the Dota 2 client, so no stats are available.
		/// </summary>
		public bool MatchHistoryPrivate { get; set; }

		public int Matches { get; set; }
		public int Wins { get; set; }
		public int Losses { get; set; }
		public int Abandons { get; set; }
		public decimal WinRate { get; set; }
		public decimal Kda { get; set; }
		public decimal KillDeathRatio { get; set; }
		public decimal AverageKills { get; set; }
		public decimal AverageDeaths { get; set; }
		public decimal AverageAssists { get; set; }
		public decimal AverageLastHits { get; set; }
		public decimal AverageGoldPerMinute { get; set; }
		public decimal AverageExperiencePerMinute { get; set; }

		/// <summary>
		/// The most played heroes, most played first.
		/// </summary>
		public List<Dota2HeroStatsViewModel> Heroes { get; set; } = new();

		/// <summary>
		/// The matches the stats were built from, newest first.
		/// </summary>
		public List<Dota2RecentMatchViewModel> RecentMatches { get; set; } = new();

		/// <summary>
		/// The profile of a player who has hidden their match data.
		/// </summary>
		public static Dota2ProfileViewModel Private(string steamId, uint accountId)
		{
			return new Dota2ProfileViewModel
			{
				SteamId = steamId,
				AccountId = accountId,
				MatchHistoryPrivate = true
			};
		}

		/// <summary>
		/// Builds the profile from the details of the matches of the player.
		/// </summary>
		/// <param name="steamId">The 64 bit steam id of the player.</param>
		/// <param name="accountId">The 32 bit Dota 2 account id of the player.</param>
		/// <param name="matches">The match details, newest first. Failed lookups (null) and matches without the player are skipped.</param>
		/// <param name="heroes">The hero list, by hero id. A missing hero leaves its name and image empty.</param>
		public static Dota2ProfileViewModel Create(string steamId, uint accountId, IReadOnlyList<Dota2MatchDetailsResult> matches, IReadOnlyDictionary<int, Dota2Hero> heroes)
		{
			ArgumentNullException.ThrowIfNull(matches);
			heroes ??= new Dictionary<int, Dota2Hero>();

			Dota2PlayerSummary summary = new(accountId, matches);
			Dota2ProfileViewModel model = new()
			{
				SteamId = steamId,
				AccountId = accountId,
				Matches = summary.matches,
				Wins = summary.wins,
				Losses = summary.losses,
				Abandons = summary.abandons,
				WinRate = summary.winRate,
				Kda = summary.kda,
				KillDeathRatio = summary.killDeathRatio,
				AverageKills = summary.averageKills,
				AverageDeaths = summary.averageDeaths,
				AverageAssists = summary.averageAssists,
				AverageLastHits = summary.averageLastHits,
				AverageGoldPerMinute = summary.averageGoldPerMinute,
				AverageExperiencePerMinute = summary.averageExperiencePerMinute,
				Heroes = summary.heroes
					.Take(TopHeroCount)
					.Select(h => new Dota2HeroStatsViewModel(h, heroes.GetValueOrDefault(h.heroId)))
					.ToList()
			};

			foreach (Dota2MatchDetailsResult match in matches)
			{
				if (match == null || !match.success)
				{
					continue;
				}
				Dota2MatchPlayer player = match.GetPlayer(accountId);
				if (player == null)
				{
					continue;
				}
				model.RecentMatches.Add(new Dota2RecentMatchViewModel(match, player, heroes.GetValueOrDefault(player.hero_id)));
			}

			return model;
		}

		internal static string HeroName(Dota2Hero hero) => hero?.localized_name;

		internal static string HeroImage(Dota2Hero hero)
		{
			string shortName = hero?.shortName;
			return string.IsNullOrEmpty(shortName) ? null : string.Format(HeroImageUrl, shortName);
		}
	}

	/// <summary>
	/// How a player did on a single hero.
	/// </summary>
	public class Dota2HeroStatsViewModel
	{
		public Dota2HeroStatsViewModel() { }

		public Dota2HeroStatsViewModel(Dota2HeroPlayCount playCount, Dota2Hero hero)
		{
			HeroId = playCount.heroId;
			HeroName = Dota2ProfileViewModel.HeroName(hero);
			HeroImageUrl = Dota2ProfileViewModel.HeroImage(hero);
			Matches = playCount.matches;
			Wins = playCount.wins;
			Losses = playCount.losses;
			WinRate = playCount.winRate;
		}

		public int HeroId { get; set; }
		public string HeroName { get; set; }
		public string HeroImageUrl { get; set; }
		public int Matches { get; set; }
		public int Wins { get; set; }
		public int Losses { get; set; }
		public decimal WinRate { get; set; }
	}

	/// <summary>
	/// A single match, from the perspective of the player the profile belongs to.
	/// </summary>
	public class Dota2RecentMatchViewModel
	{
		public Dota2RecentMatchViewModel() { }

		public Dota2RecentMatchViewModel(Dota2MatchDetailsResult match, Dota2MatchPlayer player, Dota2Hero hero)
		{
			MatchId = match.match_id;
			StartTime = match.startTime;
			Duration = match.duration;
			GameMode = match.game_mode;
			LobbyType = match.lobby_type;
			Won = match.DidWin(player);
			HeroId = player.hero_id;
			HeroName = Dota2ProfileViewModel.HeroName(hero);
			HeroImageUrl = Dota2ProfileViewModel.HeroImage(hero);
			Kills = player.kills;
			Deaths = player.deaths;
			Assists = player.assists;
			LastHits = player.last_hits;
			Denies = player.denies;
			GoldPerMinute = player.gold_per_min;
			ExperiencePerMinute = player.xp_per_min;
			Level = player.level;
			Abandoned = player.abandoned;
		}

		public long MatchId { get; set; }
		public DateTimeOffset StartTime { get; set; }

		/// <summary>
		/// The length of the match in seconds.
		/// </summary>
		public int Duration { get; set; }
		public int GameMode { get; set; }
		public int LobbyType { get; set; }
		public bool Won { get; set; }
		public int HeroId { get; set; }
		public string HeroName { get; set; }
		public string HeroImageUrl { get; set; }
		public int Kills { get; set; }
		public int Deaths { get; set; }
		public int Assists { get; set; }
		public int LastHits { get; set; }
		public int Denies { get; set; }
		public int GoldPerMinute { get; set; }
		public int ExperiencePerMinute { get; set; }
		public int Level { get; set; }
		public bool Abandoned { get; set; }
	}
}
