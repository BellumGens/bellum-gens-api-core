using System;
using System.Collections.Generic;
using System.Linq;

namespace BellumGens.Api.Core.Models
{
    public class TournamentSC2Participant : TournamentParticipant
    {
        public TournamentSC2Participant(TournamentApplication application, List<TournamentSC2Match> matches)
            : base(application)
        {
            BattleTag = application.BattleNetId;
            Country = application.Country;
            User = new UserInfoViewModel(application.User);
            TournamentSC2GroupId = application.TournamentSC2GroupId;
            if (matches != null)
            {
                foreach (TournamentSC2Match match in matches)
                {
                    if (match.Player1Id == UserId)
                    {
                        PlayerPoints += match.Player1Points;
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
                        PlayerPoints += match.Player2Points;
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
        }

        public Guid? TournamentSC2GroupId { get; set; }
        public string BattleTag { get; set; }
        public string Country { get; set; }
        public int PlayerPoints { get; set; } = 0;
        public UserInfoViewModel User { get; set; }
    }
}