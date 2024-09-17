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
            CSGODetails = user.CSGODetails;
            Sc2Details = user.StarCraft2Details;
        }

        public CSGODetails CSGODetails { get; set; }
        public StarCraft2Details Sc2Details { get; set; }
        public SteamUser SteamUser { get; set; }
        public Player SC2Player { get; set; }
        public bool SteamUserException { get; set; }
		public CSGOPlayerStats UserStats { get; set; }
		public bool UserStatsException { get; set; }
        public bool Registered
        {
            get { return user != null; }
        }

        public void SetUser(ApplicationUser user, BellumGensDbContext context)
        {
            this.user = user;
            CSGODetails = user.CSGODetails;
            Sc2Details = user.StarCraft2Details;
            RefreshAppUserValues(context);
        }

        public void RefreshAppUserValues(BellumGensDbContext context)
        {
            bool changes = false;
            bool userchange = false;
            bool sc2change = false;
            if (CSGODetails != null && SteamUser != null)
            {
                if (SteamUser?.avatarFull != CSGODetails?.AvatarFull)
                {
                    CSGODetails.AvatarFull = SteamUser.avatarFull;
                    changes = true;
                }
                if (SteamUser?.steamID != user.UserName)
                {
                    user.UserName = SteamUser.steamID;
                    userchange = true;
                }
                if (SteamUser?.avatarIcon != CSGODetails.AvatarIcon)
                {
                    CSGODetails.AvatarIcon = SteamUser.avatarIcon;
                    changes = true;
                }
                if (SteamUser?.realname != CSGODetails.RealName)
                {
                    CSGODetails.RealName = SteamUser.realname;
                    changes = true;
                }
                if (SteamUser?.avatarMedium != CSGODetails.AvatarMedium)
                {
                    CSGODetails.AvatarMedium = SteamUser.avatarMedium;
                    changes = true;
                }
                if (SteamUser?.customURL != CSGODetails.CustomUrl)
                {
                    CSGODetails.CustomUrl = SteamUser.customURL;
                    changes = true;
                }
                if (SteamUser?.country != CSGODetails.Country)
                {
                    CSGODetails.Country = SteamUser.country;
                    changes = true;
                }
                if (UserStatsException != CSGODetails.SteamPrivate)
                {
                    CSGODetails.SteamPrivate = UserStatsException;
                    changes = true;
                }
                if (!UserStatsException)
                {
                    if (UserStats?.headshotPercentage != CSGODetails.HeadshotPercentage)
                    {
                        CSGODetails.HeadshotPercentage = UserStats.headshotPercentage;
                        changes = true;
                    }
                    if (UserStats?.killDeathRatio != CSGODetails.KillDeathRatio)
                    {
                        CSGODetails.KillDeathRatio = UserStats.killDeathRatio;
                        changes = true;
                    }
                    if (UserStats?.accuracy != CSGODetails.Accuracy)
                    {
                        CSGODetails.Accuracy = UserStats.accuracy;
                        changes = true;
                    }
                    // Populate weapons and don't serialize stats again...
                    if (UserStats.weapons != null)
                        UserStats.playerstats = null;
                }
            }
            if (Sc2Details != null && SC2Player != null)
            {
                if (SC2Player.avatarUrl != Sc2Details.AvatarUrl)
                {
                    Sc2Details.AvatarUrl = SC2Player.avatarUrl;
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
                    context.Entry(csgouser).CurrentValues.SetValues(CSGODetails);
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
                    context.Entry(sc2user).CurrentValues.SetValues(Sc2Details);
                    context.SaveChanges();
                }
                catch
                {

                }
            }
        }
	}
}