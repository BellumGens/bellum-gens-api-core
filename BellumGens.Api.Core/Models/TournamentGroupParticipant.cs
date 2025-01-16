using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellumGens.Api.Core.Models
{
    [PrimaryKey("TournamentGroupId", "TournamentApplicationId")]
    public class TournamentGroupParticipant
    {
        [Key]
        [Column(Order = 0)]
        public Guid TournamentGroupId { get; set; }

        [Key]
        [Column(Order = 1)]
        public Guid TournamentApplicationId { get; set; }

        [ForeignKey("TournamentGroupId")]
        public virtual TournamentGroup TournamentGroup { get; set; }

        [ForeignKey("TournamentApplicationId")]
        public virtual TournamentApplication TournamentApplication { get; set; }

        public int Points { get; set; } = 0;
    }
}