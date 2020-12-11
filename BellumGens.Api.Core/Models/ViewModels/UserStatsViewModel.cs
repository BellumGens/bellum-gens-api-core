﻿using SteamModels;
using SteamModels.CSGO;

namespace BellumGens.Api.Core.Models
{
	public class UserStatsViewModel : UserInfoViewModel
	{
		public UserStatsViewModel() : base() { }

		public UserStatsViewModel(ApplicationUser user, bool isAuthUser = false)
			: base(user, isAuthUser) { }

        public SteamUser steamUser { get; set; }
        public bool steamUserException { get; set; }
		public CSGOPlayerStats userStats { get; set; }
		public bool userStatsException { get; set; }
        public bool registered
        {
            get { return user != null; }
        }
        public bool? steamPrivate
        {
            get
            {
                return user?.SteamPrivate;
            }
        }
        public decimal? headshotPercentage
        {
            get
            {
                return user?.HeadshotPercentage;
            }
        }
        public decimal? killDeathRatio
        {
            get
            {
                return user?.KillDeathRatio;
            }
        }
        public decimal? accuracy
        {
            get
            {
                return user?.Accuracy;
            }
        }
        public string country
        {
            get
            {
                return user?.Country;
            }
        }
        public PlaystyleRole? primaryRole
        {
            get
            {
                return user?.PreferredPrimaryRole;
            }
        }
        public PlaystyleRole? secondaryRole
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
            if (steamUser?.avatarFull != user.AvatarFull)
            {
                user.AvatarFull = steamUser.avatarFull;
                changes = true;
            }
            if (steamUser?.steamID != user.UserName)
            {
                user.UserName = steamUser.steamID;
                changes = true;
            }
            if (steamUser?.avatarIcon != user.AvatarIcon)
            {
                user.AvatarIcon = steamUser.avatarIcon;
                changes = true;
            }
            if (steamUser?.realname != user.RealName)
            {
                user.RealName = steamUser.realname;
                changes = true;
            }
            if (steamUser?.avatarMedium != user.AvatarMedium)
            {
                user.AvatarMedium = steamUser.avatarMedium;
                changes = true;
            }
            if (steamUser?.customURL != user.CustomUrl)
            {
                user.CustomUrl = steamUser.customURL;
                changes = true;
            }
            if (steamUser?.country != user.Country)
            {
                user.Country = steamUser.country;
                changes = true;
            }
            if (userStatsException != user.SteamPrivate)
            {
                user.SteamPrivate = userStatsException;
                changes = true;
            }
            if (!userStatsException)
            {
                if (userStats?.headshotPercentage != user.HeadshotPercentage)
                {
                    user.HeadshotPercentage = userStats.headshotPercentage;
                    changes = true;
                }
                if (userStats?.killDeathRatio != user.KillDeathRatio)
                {
                    user.KillDeathRatio = userStats.killDeathRatio;
                    changes = true;
                }
                if (userStats?.accuracy != user.Accuracy)
                {
                    user.Accuracy = userStats.accuracy;
                    changes = true;
                }
                // Populate weapons and don't serialize stats again...
                if (userStats.weapons != null)
                    userStats.playerstats = null;
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