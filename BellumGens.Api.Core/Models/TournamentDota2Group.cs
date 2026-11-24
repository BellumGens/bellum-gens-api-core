using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public class TournamentDota2Group : TournamentGroup
	{
		private List<TournamentDota2Participant> _participants;

		/// <summary>The teams in the group, ranked by games won and then by game difference.</summary>
		[NotMapped]
		[JsonPropertyName("participants")]
		public List<TournamentDota2Participant> PublicParticipants
		{
			get
			{
				if (_participants == null)
				{
					IEnumerable<TournamentDota2Match> matches = Matches ?? Enumerable.Empty<TournamentDota2Match>();
					_participants = Participants
						.Select(p => new TournamentDota2Participant(
							p.TournamentApplication,
							matches.Where(m => m.Team1Id == p.TournamentApplication.TeamId || m.Team2Id == p.TournamentApplication.TeamId).ToList()))
						.ToList();
				}
				return _participants.OrderByDescending(p => p.TeamPoints).ThenByDescending(p => p.GameDifference).ToList();
			}
		}

		[JsonIgnore]
		public virtual ICollection<TournamentDota2Match> Matches { get; set; }
	}
}
