using SteamModels;
using SteamModels.CSGO;

namespace BellumGens.Api.Core.Models
{
	public class UserStatsViewModel : UserInfoViewModel
	{
		public UserStatsViewModel() : base() { }

		public UserStatsViewModel(ApplicationUser user, bool isAuthUser = false)
			: base(user, isAuthUser)
        {
            _details = user.CSGODetails;
        }

        private CSGODetails _details;

        public SteamUser SteamUser { get; set; }
        public bool SteamUserException { get; set; }
		public CSGOPlayerStats UserStats { get; set; }
		public bool UserStatsException { get; set; }
        public bool Registered
        {
            get { return user != null; }
        }
        public bool? SteamPrivate
        {
            get
            {
                return _details?.SteamPrivate;
            }
        }
        public decimal? HeadshotPercentage
        {
            get
            {
                return _details?.HeadshotPercentage;
            }
        }
        public decimal? KillDeathRatio
        {
            get
            {
                return _details?.KillDeathRatio;
            }
        }
        public decimal? Accuracy
        {
            get
            {
                return _details?.Accuracy;
            }
        }
        public string Country
        {
            get
            {
                return _details?.Country;
            }
        }
        public PlaystyleRole? PrimaryRole
        {
            get
            {
                return user?.PreferredPrimaryRole;
            }
        }
        public PlaystyleRole? SecondaryRole
        {
            get
            {
                return user?.PreferredSecondaryRole;
            }
        }

        public void SetUser(ApplicationUser user, BellumGensDbContext context)
        {
            this.user = user;
            _details = user.CSGODetails;
            RefreshAppUserValues(context);
        }

        public void RefreshAppUserValues(BellumGensDbContext context)
        {
            bool changes = false;
            bool userchange = false;
            if (SteamUser?.avatarFull != _details.AvatarFull)
            {
                _details.AvatarFull = SteamUser.avatarFull;
                changes = true;
            }
            if (SteamUser?.steamID != user.UserName)
            {
                user.UserName = SteamUser.steamID;
                userchange = true;
            }
            if (SteamUser?.avatarIcon != _details.AvatarIcon)
            {
                _details.AvatarIcon = SteamUser.avatarIcon;
                changes = true;
            }
            if (SteamUser?.realname != _details.RealName)
            {
                _details.RealName = SteamUser.realname;
                changes = true;
            }
            if (SteamUser?.avatarMedium != _details.AvatarMedium)
            {
                _details.AvatarMedium = SteamUser.avatarMedium;
                changes = true;
            }
            if (SteamUser?.customURL != _details.CustomUrl)
            {
                _details.CustomUrl = SteamUser.customURL;
                changes = true;
            }
            if (SteamUser?.country != _details.Country)
            {
                _details.Country = SteamUser.country;
                changes = true;
            }
            if (UserStatsException != _details.SteamPrivate)
            {
                _details.SteamPrivate = UserStatsException;
                changes = true;
            }
            if (!UserStatsException)
            {
                if (UserStats?.headshotPercentage != _details.HeadshotPercentage)
                {
                    _details.HeadshotPercentage = UserStats.headshotPercentage;
                    changes = true;
                }
                if (UserStats?.killDeathRatio != _details.KillDeathRatio)
                {
                    _details.KillDeathRatio = UserStats.killDeathRatio;
                    changes = true;
                }
                if (UserStats?.accuracy != _details.Accuracy)
                {
                    _details.Accuracy = UserStats.accuracy;
                    changes = true;
                }
                // Populate weapons and don't serialize stats again...
                if (UserStats.weapons != null)
                    UserStats.playerstats = null;
            }
            if (userchange)
            {
                try
                {
                    ApplicationUser appuser = context.Users.Find(user.Id);
                    context.Entry(appuser).CurrentValues.SetValues(user);
                    context.SaveChanges();
                }
                catch
                {

                }
            }
            if (changes)
            {
                try
                {
                    CSGODetails csgouser = context.CSGODetails.Find(user.SteamID);
                    context.Entry(csgouser).CurrentValues.SetValues(_details);
                    context.SaveChanges();
                }
                catch
                {

                }
            }
        }
	}
}