using System.Collections.Generic;
using System.Linq;

namespace BellumGens.Api.Core.Models
{
    // Models returned by AccountController actions.

    public class ExternalLoginViewModel
    {
        public string Name { get; set; }

        public string Url { get; set; }

        public string State { get; set; }
    }

    public class UserSummaryViewModel
    {
        protected ApplicationUser user;

        public UserSummaryViewModel() { }

        public UserSummaryViewModel(ApplicationUser user)
        {
            this.user = user;
        }

        public string Id
        {
            get
            {
                return user?.Id;
            }
        }
        public string SteamId
        {
            get
            {
                return user?.SteamID;
            }
        }
        public string Username
        {
            get
            {
                return user?.UserName;
            }
        }
        public string AvatarMedium
        {
            get
            {
                return !string.IsNullOrEmpty(user?.CSGODetails?.AvatarMedium) ? user.CSGODetails.AvatarMedium : user?.StarCraft2Details?.AvatarUrl;
            }
        }
        public string CustomURL
        {
            get
            {
                return user?.CSGODetails?.CustomUrl;
            }
        }
        public string BattleNetId
        {
            get
            {
                return user?.BattleNetId;
            }
        }
    }

    public class UserInfoViewModel : UserSummaryViewModel
    {
        protected bool _isAuthUser;

		public UserInfoViewModel() : base() { }

        public UserInfoViewModel(ApplicationUser user, bool isAuthUser = false) : base(user)
        {
            _isAuthUser = isAuthUser;
        }
        public List<string> ExternalLogins { get; set; }
        public string Email
        {
            get
            {
                return _isAuthUser ? user?.Email : null;
            }
        }
        public bool? SearchVisible
        {
            get
            {
                return user?.SearchVisible;
            }
        }
        public string AvatarIcon
        {
            get
            {
                return user?.CSGODetails?.AvatarIcon;
            }
        }
        public string AvatarFull
        {
            get
            {
                return !string.IsNullOrEmpty(user?.CSGODetails?.AvatarFull) ? user.CSGODetails.AvatarFull : user?.StarCraft2Details?.AvatarUrl;
            }
        }

        public string Realname
        {
            get
            {
                return user?.CSGODetails?.RealName;
            }
        }
    }
}
