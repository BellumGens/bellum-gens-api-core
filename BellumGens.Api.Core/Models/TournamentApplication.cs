using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BellumGens.Api.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace BellumGens.Api.Core.Models
{
	public class TournamentApplication
	{
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }

		public Guid TournamentId { get; set; }

        public string UserId { get; set; }

		public Guid? TeamId { get; set; }

		public string CompanyId { get; set; }

		public DateTimeOffset DateSubmitted { get; set; } = DateTimeOffset.Now;

		public Game Game { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        public string Hash { get; set; }

        public string BattleNetId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Discord { get; set; }

        public string Country { get; set; }

        public TournamentApplicationState State { get; set; } = TournamentApplicationState.Pending;

        [NotMapped]
        public string TournamentName {
            get
            {
                return Tournament?.Name;
            }
        }

        [JsonIgnore]
        public virtual ICollection<TournamentGroupParticipant> GroupsPoints { get; set; }

        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("CompanyId")]
        [JsonIgnore]
        public virtual Company Company { get; set; }

		[ForeignKey("TeamId")]
        [JsonIgnore]
        public virtual CSGOTeam Team { get; set; }

        [ForeignKey("TournamentId")]
        [JsonIgnore]
        public virtual Tournament Tournament { get; set; }

        public async Task UniqueHash(BellumGensDbContext context)
        {
            if (string.IsNullOrEmpty(Hash))
            {
                Hash = Util.GenerateHashString(8);
                while (await context.TournamentApplications.Where(t => t.Hash == Hash).SingleOrDefaultAsync() != null)
                {
                    Hash = Util.GenerateHashString(8);
                }
            }
        }
    }
}