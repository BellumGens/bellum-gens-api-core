using SteamModels;
using SteamModels.CSGO;

namespace BellumGens.Api.Core.Models
{
	public class UserStatsViewModel : UserInfoViewModel
	{
		public UserStatsViewModel() : base() { }

		public UserStatsViewModel(ApplicationUser user, bool isAuthUser = false)
			: base(user, isAuthUser) { }

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
                return user?.SteamPrivate;
            }
        }
        public decimal? HeadshotPercentage
        {
            get
            {
                return user?.HeadshotPercentage;
            }
        }
        public decimal? KillDeathRatio
        {
            get
            {
                return user?.KillDeathRatio;
            }
        }
        public decimal? Accuracy
        {
            get
            {
                return user?.Accuracy;
            }
        }
        public string Country
        {
            get
            {
                return user?.Country;
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
            RefreshAppUserValues(context);
        }

        public void RefreshAppUserValues(BellumGensDbContext context)
        {
            bool changes = false;
            if (SteamUser?.avatarFull != user.AvatarFull)
            {
                user.AvatarFull = SteamUser.avatarFull;
                changes = true;
            }
            if (SteamUser?.steamID != user.UserName)
            {
                user.UserName = SteamUser.steamID;
                changes = true;
            }
            if (SteamUser?.avatarIcon != user.AvatarIcon)
            {
                user.AvatarIcon = SteamUser.avatarIcon;
                changes = true;
            }
            if (SteamUser?.realname != user.RealName)
            {
                user.RealName = SteamUser.realname;
                changes = true;
            }
            if (SteamUser?.avatarMedium != user.AvatarMedium)
            {
                user.AvatarMedium = SteamUser.avatarMedium;
                changes = true;
            }
            if (SteamUser?.customURL != user.CustomUrl)
            {
                user.CustomUrl = SteamUser.customURL;
                changes = true;
            }
            if (SteamUser?.country != user.Country)
            {
                user.Country = SteamUser.country;
                changes = true;
            }
            if (UserStatsException != user.SteamPrivate)
            {
                user.SteamPrivate = UserStatsException;
                changes = true;
            }
            if (!UserStatsException)
            {
                if (UserStats?.headshotPercentage != user.HeadshotPercentage)
                {
                    user.HeadshotPercentage = UserStats.headshotPercentage;
                    changes = true;
                }
                if (UserStats?.killDeathRatio != user.KillDeathRatio)
                {
                    user.KillDeathRatio = UserStats.killDeathRatio;
                    changes = true;
                }
                if (UserStats?.accuracy != user.Accuracy)
                {
                    user.Accuracy = UserStats.accuracy;
                    changes = true;
                }
                // Populate weapons and don't serialize stats again...
                if (UserStats.weapons != null)
                    UserStats.playerstats = null;
            }
            if (changes)
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
        }
	}
}