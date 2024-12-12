using StarCraft2Models;
using System.ComponentModel.DataAnnotations;

namespace BellumGens.Api.Core.Models
{
    public class StarCraft2Details
    {
        [Key]
        public string BattleNetId { get; set; }
        public string BattleNetBattleTag { get; set; }
        public string AvatarUrl { get; set; }
        public string ProfileUrl { get; set; }
        public int RegionId { get; set; }
        public int RealmId { get; set; }
    }
}
