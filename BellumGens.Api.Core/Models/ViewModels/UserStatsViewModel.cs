using StarCraft2Models;
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
            _csgoDetails = user.CSGODetails;
            _sc2Details = user.StarCraft2Details;
        }

        private CSGODetails _csgoDetails;

        private StarCraft2Details _sc2Details;

        public SteamUser SteamUser { get; set; }
        public Player Player { get; set; }
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
                return _csgoDetails?.SteamPrivate;
            }
        }
        public decimal? HeadshotPercentage
        {
            get
            {
                return _csgoDetails?.HeadshotPercentage;
            }
        }
        public decimal? KillDeathRatio
        {
            get
            {
                return _csgoDetails?.KillDeathRatio;
            }
        }
        public decimal? Accuracy
        {
            get
            {
                return _csgoDetails?.Accuracy;
            }
        }
        public string Country
        {
            get
            {
                return _csgoDetails?.Country;
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
            _csgoDetails = user.CSGODetails;
            _sc2Details = user.StarCraft2Details;
            RefreshAppUserValues(context);
        }

        public void RefreshAppUserValues(BellumGensDbContext context)
        {
            bool changes = false;
            bool userchange = false;
            bool sc2change = false;
            if (_csgoDetails != null)
            {
                if (SteamUser?.avatarFull != _csgoDetails?.AvatarFull)
                {
                    _csgoDetails.AvatarFull = SteamUser.avatarFull;
                    changes = true;
                }
                if (SteamUser?.steamID != user.UserName)
                {
                    user.UserName = SteamUser.steamID;
                    userchange = true;
                }
                if (SteamUser?.avatarIcon != _csgoDetails.AvatarIcon)
                {
                    _csgoDetails.AvatarIcon = SteamUser.avatarIcon;
                    changes = true;
                }
                if (SteamUser?.realname != _csgoDetails.RealName)
                {
                    _csgoDetails.RealName = SteamUser.realname;
                    changes = true;
                }
                if (SteamUser?.avatarMedium != _csgoDetails.AvatarMedium)
                {
                    _csgoDetails.AvatarMedium = SteamUser.avatarMedium;
                    changes = true;
                }
                if (SteamUser?.customURL != _csgoDetails.CustomUrl)
                {
                    _csgoDetails.CustomUrl = SteamUser.customURL;
                    changes = true;
                }
                if (SteamUser?.country != _csgoDetails.Country)
                {
                    _csgoDetails.Country = SteamUser.country;
                    changes = true;
                }
                if (UserStatsException != _csgoDetails.SteamPrivate)
                {
                    _csgoDetails.SteamPrivate = UserStatsException;
                    changes = true;
                }
                if (!UserStatsException)
                {
                    if (UserStats?.headshotPercentage != _csgoDetails.HeadshotPercentage)
                    {
                        _csgoDetails.HeadshotPercentage = UserStats.headshotPercentage;
                        changes = true;
                    }
                    if (UserStats?.killDeathRatio != _csgoDetails.KillDeathRatio)
                    {
                        _csgoDetails.KillDeathRatio = UserStats.killDeathRatio;
                        changes = true;
                    }
                    if (UserStats?.accuracy != _csgoDetails.Accuracy)
                    {
                        _csgoDetails.Accuracy = UserStats.accuracy;
                        changes = true;
                    }
                    // Populate weapons and don't serialize stats again...
                    if (UserStats.weapons != null)
                        UserStats.playerstats = null;
                }
            }
            if (_sc2Details != null && Player != null)
            {
                if (Player.avatarUrl != _sc2Details.AvatarUrl)
                {
                    _sc2Details.AvatarUrl = Player.avatarUrl;
                    sc2change = true;
                }
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
                    context.Entry(csgouser).CurrentValues.SetValues(_csgoDetails);
                    context.SaveChanges();
                }
                catch
                {

                }
            }
            if (sc2change) {
                try
                {
                    StarCraft2Details sc2user = context.StarCraft2Details.Find(user.BattleNetId);
                    context.Entry(sc2user).CurrentValues.SetValues(_sc2Details);
                    context.SaveChanges();
                }
                catch
                {

                }
            }
        }
	}
}