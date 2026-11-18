using System;
using System.Collections.Generic;

namespace BellumGens.Api.Core.Models
{
	/// <summary>
	/// A team registered for a Dota 2 tournament, with its record across its series.
	/// </summary>
	public class TournamentDota2Participant : TournamentParticipant
	{
		public TournamentDota2Participant(TournamentApplication application, List<TournamentDota2Match> matches)
			: base(application)
		{
			TeamId = application.TeamId;
			Team = application.Team != null ? new CSGOTeamSummaryViewModel(application.Team) : null;
			if (matches == null || TeamId == null)
			{
				return;
			}

			foreach (TournamentDota2Match match in matches)
			{
				int won, lost;
				if (match.Team1Id == TeamId)
				{
					(won, lost) = (match.Team1Points, match.Team2Points);
				}
				else if (match.Team2Id == TeamId)
				{
					(won, lost) = (match.Team2Points, match.Team1Points);
				}
				else
				{
					continue;
				}

				// A series without a single game played hasn't happened yet.
				if (won + lost == 0)
				{
					continue;
				}

				Matches++;
				TeamPoints += won;
				GameDifference += won - lost;
				if (won > lost)
				{
					Wins++;
				}
				else if (won < lost)
				{
					Losses++;
				}
				else
				{
					Draws++;
				}
			}
		}

		public Guid? TeamId { get; set; }

		public CSGOTeamSummaryViewModel Team { get; set; }

		/// <summary>The games the team won, which is what the group standings are ranked by.</summary>
		public int TeamPoints { get; set; } = 0;

		/// <summary>Games won minus games lost; the tiebreaker for teams on the same points.</summary>
		public int GameDifference { get; set; } = 0;

		/// <summary>Series that ended level, possible in a best of two.</summary>
		public int Draws { get; set; } = 0;
	}
}
