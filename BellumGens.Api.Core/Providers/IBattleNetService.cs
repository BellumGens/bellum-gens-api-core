using StarCraft2Models;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    interface IBattleNetService
    {
        public Task<Player> GetStarCraft2Player(string playerid, string region = "eu", int regionid = 2, int realmid = 1);
        //public Task<PlayerProfile> GetStarCraft2PlayerProfile(string playerid, string region, int regionid, int realmid);
    }
}
