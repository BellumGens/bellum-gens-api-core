using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellumGens.Api.Core.Models
{
    public class StarCraft2Details
    {
        [Key]
        public string BattleNetId { get; set; }
        public string BattleNetBattleTag { get; set; }
        public string AvatarUrl { get; set; }
    }
}
