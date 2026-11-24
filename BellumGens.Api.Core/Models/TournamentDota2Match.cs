using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	/// <summary>
	/// A Dota 2 series between two teams. The points are the games each team won, so a best of two
	/// can end in a draw.
	/// </summary>
	public class TournamentDota2Match : TournamentMatch
	{
		public Guid Team1Id { get; set; }

		public Guid Team2Id { get; set; }

		public Guid? GroupId { get; set; }

		public int Team1Points { get; set; }

		public int Team2Points { get; set; }

		/// <summary>The team that won the series; <see cref="Guid.Empty"/> while it is undecided or drawn.</summary>
		[NotMapped]
		public Guid WinnerTeamId
		{
			get
			{
				return Team1Points == Team2Points ? Guid.Empty : Team1Points > Team2Points ? Team1Id : Team2Id;
			}
		}

		[NotMapped]
		[JsonPropertyName("team1")]
		public CSGOTeamSummaryViewModel Team1Summary
		{
			get
			{
				return Team1 != null ? new CSGOTeamSummaryViewModel(Team1) : null;
			}
		}

		[NotMapped]
		[JsonPropertyName("team2")]
		public CSGOTeamSummaryViewModel Team2Summary
		{
			get
			{
				return Team2 != null ? new CSGOTeamSummaryViewModel(Team2) : null;
			}
		}

		public virtual ICollection<Dota2MatchGame> Games { get; set; } = new HashSet<Dota2MatchGame>();

		[JsonIgnore]
		[ForeignKey("Team1Id")]
		public virtual CSGOTeam Team1 { get; set; }

		[JsonIgnore]
		[ForeignKey("Team2Id")]
		public virtual CSGOTeam Team2 { get; set; }

		[JsonIgnore]
		[ForeignKey("GroupId")]
		public virtual TournamentDota2Group Group { get; set; }
	}
}
