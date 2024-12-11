using System.Collections.Generic;

namespace BellumGens.Api.Core.Models
{
    // Models returned by AccountController actions.

    public class ExternalLoginViewModel
    {
        public string Name { get; set; }

        public string Url { get; set; }

        public string State { get; set; }
    }

    public class UserInfoViewModel
    {
        protected bool _isAuthUser; 
        protected ApplicationUser user;

        public UserInfoViewModel() : base() { }

        public UserInfoViewModel(ApplicationUser user, bool isAuthUser = false)
        {
            this.user = user;
            _isAuthUser = isAuthUser;
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
        public string BattleNetId
        {
            get
            {
                return user?.BattleNetId;
            }
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
    }
}
