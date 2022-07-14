using StarCraft2Models;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public interface IBattleNetService
    {
        public Task<Player> GetStarCraft2Player(string playerid, string region = "eu");
        //public Task<PlayerProfile> GetStarCraft2PlayerProfile(string playerid, string region, int regionid = 2, int realmid = 1);
    }
}
