using System.ComponentModel.DataAnnotations;

namespace BellumGens.Api.Core.Models
{
    public class CSGODetails
    {
        [Key]
        public string SteamId { get; set; }
        public string AvatarFull { get; set; }
        public string AvatarMedium { get; set; }
        public string AvatarIcon { get; set; }
        public string RealName { get; set; }
        public string CustomUrl { get; set; }
        public string Country { get; set; }
        public decimal HeadshotPercentage { get; set; }
        public decimal KillDeathRatio { get; set; }
        public decimal Accuracy { get; set; }

        public bool SteamPrivate { get; set; } = false;
    }
}
