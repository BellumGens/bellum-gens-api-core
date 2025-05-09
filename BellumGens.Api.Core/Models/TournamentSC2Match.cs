﻿using System;
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

        public SC2Race? Player1Race { get; set; }

        public SC2Race? Player2Race { get; set; }

        public int Player1Points { get; set; }

        public int Player2Points { get; set; }

        [NotMapped]
        public string WinnerPlayerId
        {
            get
            {
                return Player1Points == Player2Points ? string.Empty : Player1Points > Player2Points ? Player1Id : Player2Id;
            }
        }

        [NotMapped]
        [JsonPropertyName("player1")]
        public UserInfoViewModel Player1Summary
        {
            get
            {
                return Player1 != null ? new UserInfoViewModel(Player1) : null;
            }
        }

        [NotMapped]
        [JsonPropertyName("player2")]
        public UserInfoViewModel Player2Summary
        {
            get
            {
                return Player2 != null ? new UserInfoViewModel(Player2) : null;
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