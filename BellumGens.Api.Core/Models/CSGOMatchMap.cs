﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
    public class CSGOMatchMap
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public CSGOMap Map { get; set; }

        public Guid CsgoMatchId { get; set; }

        public Guid? TeamPickId { get; set; }

        public Guid? TeamBanId { get; set; }

        public int Team1Score { get; set; }

        public int Team2Score { get; set; }

        [JsonIgnore]
        [ForeignKey("CsgoMatchId")]
        public virtual TournamentCSGOMatch Match { get; set; }

        [JsonIgnore]
        [ForeignKey("TeamPickId")]
        public virtual CSGOTeam PickingTeam { get; set; }

        [JsonIgnore]
        [ForeignKey("TeamBanId")]
        public virtual CSGOTeam BanningTeam { get; set; }
    }
}