using Microsoft.Extensions.Configuration;

namespace BellumGens.Api.Core
{
	public class AppKeysDescriptior
	{
		public string SteamApiKey { get; set; }
		public string BattleNetClientId { get; set; }
		public string BattleNetClientSecret { get; set; }
		public string CSGOGameId { get; set; } = "730";
		public string TwitchClientId { get; set; }
		public string TwitchSecret { get; set; }
		public string PublicVapidKey { get; set; }
		public string PrivateVapidKey { get; set; }
		public string Email { get; set; }
		public string EmailUsername { get; set; }
		public string EmailPassword { get; set; }
        public string Bank { get; set; }
        public string BankAccountOwner { get; set; }
        public string BIC { get; set; }
        public string BankAccount { get; set; }
	}

	public class AppConfiguration
	{
		private readonly AppKeysDescriptior _config;

		public AppConfiguration(IConfiguration configuration)
        {
			_config = new AppKeysDescriptior()
			{
				SteamApiKey = configuration["steamApiKey"],
				BattleNetClientId = configuration.GetValue<string>("battleNet:clientId"),
				BattleNetClientSecret = configuration.GetValue<string>("battleNet:secret"),
				TwitchClientId = configuration.GetValue<string>("twitch:clientId"),
				TwitchSecret = configuration.GetValue<string>("twitch:secret"),
				PublicVapidKey = configuration.GetValue<string>("vapid:public"),
				PrivateVapidKey = configuration.GetValue<string>("vapid:private"),
				Email = configuration.GetValue<string>("email:username"),
				EmailUsername = configuration.GetValue<string>("email:username"),
				EmailPassword = configuration.GetValue<string>("email:password"),
				Bank = configuration.GetValue<string>("bank:name"),
				BankAccountOwner = configuration.GetValue<string>("bank:owner"),
				BIC = configuration.GetValue<string>("bank:bic"),
				BankAccount = configuration.GetValue<string>("bank:account")
			};
		}

		public AppKeysDescriptior Config
		{
			get
			{
				return _config;
			}
		}
	}
}
