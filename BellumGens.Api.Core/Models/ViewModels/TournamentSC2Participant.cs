using System;
using System.Collections.Generic;

namespace BellumGens.Api.Core.Models
{
    public class TournamentSC2Participant : TournamentParticipant
    {
        public TournamentSC2Participant(TournamentApplication application, List<TournamentSC2Match> matches, int points = 0)
            : base(application)
        {
            BattleTag = application.BattleNetId;
            Country = application.Country;
            User = new UserStatsViewModel(application.User);
            PlayerPoints = points;
            foreach (TournamentSC2Match match in matches)
            {
                if (match.Player1Id == UserId)
                {
                    // PlayerPoints += match.Player1Points;
                    if (match.Player1Points + match.Player2Points > 0)
                    {
                        Matches++;
                        if (match.Player1Points > match.Player2Points)
                            Wins++;
                        else
                            Losses++;
                    }
                }
                else if (match.Player2Id == UserId)
                {
                    // PlayerPoints += match.Player2Points;
                    if (match.Player1Points + match.Player2Points > 0)
                    {
                        Matches++;
                        if (match.Player2Points > match.Player1Points)
                            Wins++;
                        else
                            Losses++;
                    }
                }
            }
        }
        public string BattleTag { get; set; }
        public string Country { get; set; }
        public int PlayerPoints { get; set; } = 0;
        public UserStatsViewModel User { get; set; }
    }
}