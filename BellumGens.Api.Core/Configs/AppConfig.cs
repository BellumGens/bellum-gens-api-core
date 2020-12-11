using Microsoft.Extensions.Configuration;

namespace BellumGens.Api.Core
{
	public class AppKeysDescriptior
	{
		public string steamApiKey { get; set; }
		public string battleNetClientId { get; set; }
		public string battleNetClientSecret { get; set; }
		public string csgoGameId { get; set; } = "730";
		public string twitchClientId { get; set; }
		public string twitchSecret { get; set; }
		public string publicVapidKey { get; set; }
		public string privateVapidKey { get; set; }
		public string email { get; set; }
		public string emailUsername { get; set; }
		public string emailPassword { get; set; }
        public string bank { get; set; }
        public string bankAccountOwner { get; set; }
        public string bic { get; set; }
        public string bankAccount { get; set; }
	}

	public class AppConfiguration
	{
		private AppKeysDescriptior _config;

		public AppConfiguration(IConfiguration configuration)
        {
			_config = new AppKeysDescriptior()
			{
				steamApiKey = configuration["steamApiKey"],
				battleNetClientId = configuration.GetValue<string>("battleNet:clientId"),
				battleNetClientSecret = configuration.GetValue<string>("battleNet:secret"),
				twitchClientId = configuration.GetValue<string>("twitch:clientId"),
				twitchSecret = configuration.GetValue<string>("twitch:secret"),
				publicVapidKey = configuration.GetValue<string>("vapid:public"),
				privateVapidKey = configuration.GetValue<string>("vapid:private"),
				email = configuration.GetValue<string>("email:username"),
				emailUsername = configuration.GetValue<string>("email:username"),
				emailPassword = configuration.GetValue<string>("email:password"),
				bank = configuration.GetValue<string>("bank:name"),
				bankAccountOwner = configuration.GetValue<string>("bank:owner"),
				bic = configuration.GetValue<string>("bank:bic"),
				bankAccount = configuration.GetValue<string>("bank:account")
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
