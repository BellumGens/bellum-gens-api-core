using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
    public class PlayerTournamentViewModel
    {
        private readonly Tournament _tournament;
        private readonly string _userid;
        private List<TournamentSC2Match> _matches;
        public PlayerTournamentViewModel(Tournament tournament, string playerid)
        {
            _tournament = tournament;
            _userid = playerid;
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
        [JsonPropertyName("sc2Matches")]
        public List<TournamentSC2Match> SC2Matches
        {
            get
            {
                if (_matches == null)
                {
                    _matches = _tournament.SC2Matches.Where(m => m.Player1Id == _userid || m.Player2Id == _userid).OrderByDescending(m => m.StartTime).ToList();
                }
                return _matches;
            }
        }
    }
}
