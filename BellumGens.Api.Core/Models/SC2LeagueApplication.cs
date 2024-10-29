using BellumGens.Api.Core.Common;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace BellumGens.Api.Core.Models
{
    public class SC2LeagueApplication
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public string UserId { get; set; }

        public string TeamId { get; set; }

        public string BattleNetId { get; set; }

        public DateTimeOffset DateSubmitted { get; set; } = DateTimeOffset.Now;

        [EmailAddress]
        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Discord { get; set; }

        public string Country { get; set; }

        public string Hash { get; set; }

        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("TeamId")]
        [JsonIgnore]
        public virtual Company Team { get; set; }

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
