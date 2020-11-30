using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
    public class TournamentSC2Match : TournamentMatch
    {
        public string Player1Id { get; set; }
        public string Player2Id { get; set; }

        public Guid? GroupId { get; set; }

        public virtual ICollection<SC2MatchMap> Maps { get; set; }

        public int Player1Points { get; set; }

        public int Player2Points { get; set; }

        [NotMapped]
        [JsonPropertyName("player1")]
        public UserSummaryViewModel Player1Summary
        {
            get
            {
                return Player1 != null ? new UserSummaryViewModel(Player1) : null;
            }
        }

        [NotMapped]
        [JsonPropertyName("player2")]
        public UserSummaryViewModel Player2Summary
        {
            get
            {
                return Player2 != null ? new UserSummaryViewModel(Player2) : null;
            }
        }

        [JsonIgnore]
        [ForeignKey("Player1Id")]
        public virtual ApplicationUser Player1 { get; set; }

        [JsonIgnore]
        [ForeignKey("Player2Id")]
        public virtual ApplicationUser Player2 { get; set; }

        [JsonIgnore]
        [ForeignKey("GroupId")]
        public virtual TournamentSC2Group Group { get; set; }
    }
}