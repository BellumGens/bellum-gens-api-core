using Microsoft.Extensions.Caching.Memory;
using StarCraft2Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public class BattleNetServiceProvider : IBattleNetService
    {
        private readonly IMemoryCache _cache;

        private static readonly string _profileEndpoint = "https://{0}.api.blizzard.com/sc2/metadata/profile/{1}/{2}/{3}";

        public BattleNetServiceProvider(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<Player> GetStarCraft2Player(string playerid, string region = "eu", int regionid = 2, int realmid = 1)
        {
            Player player;
            using (HttpClient client = new())
            {
                Uri endpoint = new(string.Format(_profileEndpoint, region, regionid, realmid, playerid));
                var response = await client.GetStringAsync(endpoint);
                player = JsonSerializer.Deserialize<Player>(response);

            }
            return player;
        }

        //public Task<PlayerProfile> GetStarCraft2PlayerProfile(string playerid, string region = "eu", int regionid = 2, int realmid = 1)
        //{

        //}
    }
}
