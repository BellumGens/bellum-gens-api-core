using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BellumGens.Api.Core.Models
{
	public class TeamTournamentViewModel
	{
		private readonly Tournament _tournament;
		private readonly Guid _teamid;
		private List<TournamentCSGOMatch> _matches;
		private List<TournamentDota2Match> _dota2Matches;
		public TeamTournamentViewModel(Tournament tournament, Guid teamid)
		{
			_tournament = tournament;
			_teamid = teamid;
		}

		public Guid ID
		{
			get
			{
				return _tournament.ID;
			}
		}
		public string Name
		{
			get
			{
				return _tournament.Name;
			}
		}
		public string Logo
		{
			get
			{
				return _tournament.Logo;
			}
		}
		[JsonProperty("csgoMatches")]
		public List<TournamentCSGOMatch> CSGOMatches
		{
			get
			{
				if (_matches == null)
				{
					_matches = _tournament.CSGOMatches.Where(m => m.Team1Id == _teamid || m.Team2Id == _teamid).OrderByDescending(m => m.StartTime).ToList();
				}
				return _matches;
			}
		}
		[JsonProperty("dota2Matches")]
		public List<TournamentDota2Match> Dota2Matches
		{
			get
			{
				if (_dota2Matches == null)
				{
					_dota2Matches = (_tournament.Dota2Matches ?? new List<TournamentDota2Match>())
						.Where(m => m.Team1Id == _teamid || m.Team2Id == _teamid)
						.OrderByDescending(m => m.StartTime)
						.ToList();
				}
				return _dota2Matches;
			}
		}
	}
}