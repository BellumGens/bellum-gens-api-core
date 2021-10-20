using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using StarCraft2Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public class BattleNetServiceProvider : IBattleNetService
    {
        private readonly IMemoryCache _cache;

        private static readonly string _profileEndpoint = "https://{0}.api.blizzard.com/sc2/metadata/profile/{1}/{2}/{3}?locale=en_US&access_token={4}";
        private static readonly string _tokenEndpoint = "https://eu.battle.net/oauth/token";

        private static OAuthResponse _oauth;
        private static DateTimeOffset _tokenIssueStamp;
        private readonly IConfiguration _configuration;

        public BattleNetServiceProvider(IMemoryCache cache, IConfiguration config)
        {
            _cache = cache;
            _configuration = config;
        }

        public async Task<Player> GetStarCraft2Player(string playerid, string region = "eu", int regionid = 2, int realmid = 1)
        {
            Player player;
            if (_cache.Get(playerid) is Player)
            {
                return _cache.Get(playerid) as Player;
            }

            if (_oauth == null || DateTimeOffset.Now > _tokenIssueStamp.AddSeconds(_oauth.expires_in))
            {
                _oauth = await this.GetAccessToken();
            }

            using HttpClient client = new();
            Uri endpoint = new(string.Format(_profileEndpoint, region, regionid, realmid, playerid, _oauth.access_token));
            var response = await client.GetStringAsync(endpoint);
            player = JsonSerializer.Deserialize<Player>(response);
            _cache.Set(playerid, player, DateTime.Now.AddDays(1));
            return player;
        }

        //public Task<PlayerProfile> GetStarCraft2PlayerProfile(string playerid, string region = "eu", int regionid = 2, int realmid = 1)
        //{

        //}

        private async Task<OAuthResponse> GetAccessToken()
        {
            using HttpClient client = new();
            Uri endpoint = new(_tokenEndpoint);
            var values = new List<KeyValuePair<string, string>>();

            values.Add(new KeyValuePair<string, string>("grant_type", "client_credentials"));
            var content = new FormUrlEncodedContent(values);

            var authenticationString = $"{_configuration.GetValue<string>("battleNet:clientId")}:{_configuration.GetValue<string>("battleNet:secret")}";
            var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString));

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
            requestMessage.Content = content;

            var response = await client.SendAsync(requestMessage);
            if (response.IsSuccessStatusCode)
            {
                _tokenIssueStamp = DateTimeOffset.Now;
                var responseString = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OAuthResponse>(responseString);
            }
            return null;
        }

        private class OAuthResponse
        {
#pragma warning disable IDE1006 // Naming Styles
            public string access_token { get; set; }
            public string token_type { get; set; }
            public int expires_in { get; set; }
            public string scope { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        }
    }
}
