using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
    public class TournamentCSGOGroup : TournamentGroup
    {
        private List<TournamentCSGOParticipant> _participants;

        [NotMapped]
        [JsonPropertyName("participants")]
        public List<TournamentCSGOParticipant> PublicParticipants
        {
            get
            {
                if (_participants == null)
                {
                    _participants = new List<TournamentCSGOParticipant>();
                    foreach (TournamentGroupParticipant app in Participants)
                    {
                        _participants.Add(
                            new TournamentCSGOParticipant(
                                app.TournamentApplication, 
                                Matches.Where(m => m.Team1Id == app.TournamentApplication.TeamId || m.Team2Id == app.TournamentApplication.TeamId).ToList()
                            )
                        );
                    }
                }
                return _participants.OrderByDescending(p => p.TeamPoints).ThenByDescending(p => p.RoundDifference).ToList();
            }
        }

        [JsonIgnore]
        public virtual ICollection<TournamentCSGOMatch> Matches { get; set; }
    }
}