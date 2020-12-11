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

        public string id
        {
            get
            {
                return user?.Id;
            }
        }
        public string steamId
        {
            get
            {
                return user?.SteamID;
            }
        }
        public string username
        {
            get
            {
                return user?.UserName;
            }
        }
        public string avatarMedium
        {
            get
            {
                return user?.AvatarMedium;
            }
        }
        public string customURL
        {
            get
            {
                return user?.CustomUrl;
            }
        }
        public string battleNetId
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
        public List<string> externalLogins { get; set; }
        public string email
        {
            get
            {
                return _isAuthUser ? user?.Email : null;
            }
        }
        public bool? searchVisible
        {
            get
            {
                return user?.SearchVisible;
            }
        }
        public string avatarIcon
        {
            get
            {
                return user?.AvatarIcon;
            }
        }
        public string avatarFull
        {
            get
            {
                return user?.AvatarFull;
            }
        }

        public string realname
        {
            get
            {
                return user?.RealName;
            }
        }
    }
}
