using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	/// <summary>
	/// A single game of a Dota 2 series.
	/// </summary>
	public class Dota2MatchGame
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }

		public Guid Dota2MatchId { get; set; }

		/// <summary>The position of the game in the series, starting at 1.</summary>
		public int GameNumber { get; set; }

		/// <summary>The id of the game on Steam, so it can be found in the Dota 2 client and the Steam API.</summary>
		public long? SteamMatchId { get; set; }

		/// <summary>The team that played on the Radiant side; the other team played on the Dire side.</summary>
		public Guid? RadiantTeamId { get; set; }

		public Guid? WinnerTeamId { get; set; }

		[JsonIgnore]
		[ForeignKey("Dota2MatchId")]
		public virtual TournamentDota2Match Match { get; set; }

		[JsonIgnore]
		[ForeignKey("RadiantTeamId")]
		public virtual CSGOTeam RadiantTeam { get; set; }

		[JsonIgnore]
		[ForeignKey("WinnerTeamId")]
		public virtual CSGOTeam WinnerTeam { get; set; }
	}
}
