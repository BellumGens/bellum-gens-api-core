using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using BellumGens.Api.Core.Models.Extensions;
using Microsoft.EntityFrameworkCore;
using BellumGens.Api.Core;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace BellumGens.Api.Controllers
{
	[Authorize]
    public class AccountController : BaseController
    {
		private const string emailConfirmation = "Greetings,<br /><br />You have updated your account information on <a href='https://bellumgens.com' target='_blank'>bellumgens.com</a> with your email address.<br /><br />To confirm your email address click on this <a href='{0}' target='_blank'>link</a>.<br /><br />The Bellum Gens team<br /><br /><a href='https://bellumgens.com' target='_blank'>https://bellumgens.com</a>";
        private readonly ISteamService _steamService;

		public AccountController(ISteamService steamService, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, IEmailSender sender, BellumGensDbContext context, ILogger<AccountController> logger)
            : base(userManager, roleManager, signInManager, sender, context, logger)
        {
            _steamService = steamService;
        }

        // GET api/Account/Username
        [AllowAnonymous]
        [Route("Username")]
        public IActionResult GetUsername(string username)
        {
            return Ok(_dbContext.Users.Any(u => u.UserName == username));
        }

        // GET api/Account/UserInfo
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Get()
        {
			if (User.Identity.IsAuthenticated)
			{
                string userId = User.GetResolvedUserId();

                ApplicationUser user = _dbContext.Users.FirstOrDefault(e => e.Id == userId);
                if (user == null)
                {
                    await _signInManager.SignOutAsync();
                    return null;
                }

                UserInfoViewModel model = new UserInfoViewModel(user, true);
                var logins = await _userManager.GetLoginsAsync(user);
                model.externalLogins = logins.Select(t => t.LoginProvider).ToList();
				return Ok(model);
			}
            return Unauthorized("Have to login first");
        }

        [Route("UserNotifications")]
        public async Task<IActionResult> GetUserNotifications()
        {
            ApplicationUser user = await GetAuthUser();
            List<TeamInvite> teamInvites = await _dbContext.TeamInvites.Include(i => i.Team).Where(i => i.InvitedUserId == user.Id).ToListAsync();
            return Ok(teamInvites);
        }

        [Route("UserTeamsAdmin")]
        public async Task<IActionResult> GetUserTeams()
        {
            ApplicationUser user = await GetAuthUser();
            List<CSGOTeamSummaryViewModel> teams = new List<CSGOTeamSummaryViewModel>();
            await _dbContext.TeamMembers.Include(m => m.Team).Where(m => m.UserId == user.Id && m.IsAdmin).Select(m => m.Team).ForEachAsync(t => teams.Add(new CSGOTeamSummaryViewModel(t)));
            return Ok(teams);
        }

        [HttpPost]
		[AllowAnonymous]
		[Route("Subscribe")]
		public IActionResult Subscribe(Subscriber sub)
		{
            if (ModelState.IsValid)
            {
                _dbContext.Subscribers.Add(sub);

                try
                {
                    _dbContext.SaveChanges();
                }
                catch (DbUpdateException e)
                {
                    System.Diagnostics.Trace.TraceInformation("Email subscription exception: " + e.Message);
                    return Ok("Already subscribed...");
                }
                return Ok("Subscribed successfully!");
            }
            return BadRequest("Email is not valid");
		}

		[HttpGet]
		[AllowAnonymous]
		[Route("Unsubscribe")]
		public IActionResult Unsubscribe(string email, Guid sub)
		{
			Subscriber subscriber = _dbContext.Subscribers.Find(email);
			if (subscriber?.SubKey == sub)
			{
				subscriber.Subscribed = false;
				try
				{
					_dbContext.SaveChanges();
				}
				catch (DbUpdateException e)
				{
                    System.Diagnostics.Trace.TraceError("Email sub unsubscribe error: " + e.Message);
                    return BadRequest("Something went wrong");
				}
				return Redirect(CORSConfig.returnOrigin + "/emailconfirm/unsubscribed");
			}
			return BadRequest("Couldn't find the subscription...");
		}

		[Route("UserInfo")]
		[HttpPut]
		public async Task<IActionResult> UpdateUserInfo(UserPreferencesViewModel preferences)
		{
            ApplicationUser user = await GetAuthUser();
			bool newEmail = !string.IsNullOrEmpty(preferences.email) && preferences.email != user.Email && !user.EmailConfirmed;
			user.Email = preferences.email;
			user.SearchVisible = preferences.searchVisible;
			try
			{
				_dbContext.SaveChanges();
				if (newEmail)
				{
					string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
					var callbackUrl = Url.Link("ActionApi", new { controller = "Account", action = "ConfirmEmail", userId = user.Id, code });
					await _sender.SendEmailAsync(user.Email, "Confirm your email", string.Format(emailConfirmation, callbackUrl));
				}
			}
			catch (Exception e)
            {
                System.Diagnostics.Trace.TraceError("UserInfo update error: " + e.Message);
                return BadRequest("Something went wrong...");
			}
			return Ok(new { newEmail, preferences.email });
		}

		[AllowAnonymous]
        [HttpGet]
        [Route("ConfirmEmail", Name = "ConfirmEmail")]
		public async Task<IActionResult> ConfirmEmail(string userId, string code)
		{
			if (userId == null || code == null)
			{
				return Redirect(CORSConfig.returnOrigin + "/emailconfirm/error");
			}
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var result = await _userManager.ConfirmEmailAsync(user, code);
                if (result.Succeeded)
                {
                    return Redirect(CORSConfig.returnOrigin + "/emailconfirm");
                }
            }
			return Redirect(CORSConfig.returnOrigin + "/emailconfirm/error");
        }

        // POST api/Account/Login
        [Route("Login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginBindingModel login)
        {
            var result = await _signInManager.PasswordSignInAsync(login.UserName, login.Password, login.RememberMe, false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByNameAsync(login.UserName);
                UserStatsViewModel model = new UserStatsViewModel(user, true);
                if (string.IsNullOrEmpty(user.AvatarFull) && !Guid.TryParse(user.Id, out Guid newguid))
                {
                    model = await _steamService.GetSteamUserDetails(user.Id);
                    model.RefreshAppUserValues(_dbContext);
                }
                var logins = await _userManager.GetLoginsAsync(user);
                model.externalLogins = logins.Select(t => t.LoginProvider).ToList();
                return Ok(model);
            }
            return BadRequest("Invalid username or password, or email not verified.");
        }

        // POST api/Account/Logout
        [Route("Logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok();
        }

		// DELETE api/Account/Delete
		[HttpDelete]
		[Route("Delete")]
		public async Task<IActionResult> Delete(string userid)
		{
            ApplicationUser user = await GetAuthUser();

            if (user.Id != userid)
			{
				return BadRequest("User account mismatch...");
			}
            await _signInManager.SignOutAsync();

            List<BellumGensPushSubscription> subs = _dbContext.PushSubscriptions.Where(s => s.userId == userid).ToList();
            foreach (var sub in subs)
            {
                _dbContext.PushSubscriptions.Remove(sub);
            }
            List<TeamInvite> invites = _dbContext.TeamInvites.Where(i => i.InvitedUserId == userid || i.InvitingUserId == userid).ToList();
            foreach (var invite in invites)
            {
                _dbContext.TeamInvites.Remove(invite);
            }
			_dbContext.Users.Remove(user);

			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
                System.Diagnostics.Trace.TraceError("User account delete error: " + e.Message);
                return BadRequest("Something went wrong...");
			}
			return Ok("Ok");
		}

        // POST api/Account/SetPassword
        [AllowAnonymous]
        [Route("SetPassword")]
        public async Task<IActionResult> SetPassword(RegisterBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!User.Identity.IsAuthenticated)
            {
                IdentityResult register = await Register(model);
                if (!register.Succeeded)
                {
                    return GetErrorResult(register);
                }
                return Ok();
            }

            ApplicationUser user = await GetAuthUser();

            IdentityResult result = await _userManager.AddPasswordAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            if (user.UserName != model.UserName)
                user.UserName = model.UserName;
            if (user.Email != model.Email)
            {
                user.Email = model.Email;
                result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    try
                    {
                        string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var callbackUrl = Url.Link("ActionApi", new { controller = "Account", action = "ConfirmEmail", userId = user.Id, code });
                        await _sender.SendEmailAsync(user.Email, "Confirm your email", string.Format(emailConfirmation, callbackUrl));
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Trace.TraceError("Email confirmation send exception: " + e.Message);
                    }
                }
            }

            return Ok();
        }

        [AllowAnonymous]
        [Route("Twitch", Name = "ExternalCallback")]
        public async Task<IActionResult> ExternalCallback(string error = null, string returnUrl = "", string userId = null)
        {
            if (error != null)
            {
                return Redirect(returnUrl + "/unauthorized");
            }

            ExternalLoginInfo info = await _signInManager.GetExternalLoginInfoAsync();
            Uri returnUri = new Uri(!string.IsNullOrEmpty(returnUrl) ? returnUrl : CORSConfig.returnOrigin);
            string returnHost = returnUri.GetLeftPart(UriPartial.Authority);
            string returnPath = returnUri.AbsolutePath;
            ApplicationUser user;
            IdentityResult result;

            if (userId != null)
            {
                user = await _userManager.FindByIdAsync(userId);
                result = await AddLogin(user, info);
                if (!result.Succeeded)
                {
                    string loginError = result.Errors.First().Description;
                    return Redirect(returnHost + "/unauthorized/" + loginError);
                }
                return Redirect(returnHost + returnPath);
            }

            user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

            if (user == null)
            {
                result = await Register(info);
                if (!result.Succeeded)
                {
                    return Redirect(returnUrl + "/unauthorized");
                }
                returnPath = "/register";
            }
            else if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
            if (!signInResult.Succeeded)
            {
                return Redirect(returnHost + "/unauthorized");
            }
            return Redirect(returnHost + returnPath);
        }

        // GET api/Account/ExternalLogin
        [AllowAnonymous]
        [Route("ExternalLogin", Name = "ExternalLogin")]
        public async Task<IActionResult> GetExternalLogin(string provider, string error = null, string returnUrl = "")
        {
            if (error != null)
            {
                return Redirect(returnUrl + "/unauthorized");
			}

            ApplicationUser user = await GetAuthUser();
            string userId = null;

            if (user != null)
            {
                userId = user.Id;
            }

            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, Url.Action("ExternalCallback", "Account", new { returnUrl, userId }));
            return Challenge(properties, provider);
		}

        // GET api/Account/ExternalLogins?returnUrl=%2F&generateState=true
        [AllowAnonymous]
        [Route("ExternalLogins")]
        public async Task<IEnumerable<ExternalLoginViewModel>> GetExternalLogins(string returnUrl, string routeName = "ExternalLogin", bool generateState = false)
        {
            IEnumerable<AuthenticationScheme> descriptions = await _signInManager.GetExternalAuthenticationSchemesAsync();
            List<ExternalLoginViewModel> logins = new List<ExternalLoginViewModel>();

            string state;

            if (generateState)
            {
                const int strengthInBits = 256;
                state = RandomOAuthStateGenerator.Generate(strengthInBits);
            }
            else
            {
                state = null;
            }

            foreach (AuthenticationScheme description in descriptions)
            {
                ExternalLoginViewModel login = new ExternalLoginViewModel
                {
                    Name = description.DisplayName,
                    Url = Url.RouteUrl(routeName, new
                    {
                        provider = description.Name,
                        response_type = "token",
                        client_id = Startup.PublicClientId,
                        redirect_uri = new Uri(new Uri(Request.GetDisplayUrl()), returnUrl).AbsoluteUri,
                        state
                    }, Request.Scheme),
                    State = state
                };
                logins.Add(login);
            }

            return logins;
        }

        #region Helpers

        private async Task<IdentityResult> Register(ExternalLoginInfo info)
		{
            ApplicationUser user = null;
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var username = info.Principal.FindFirstValue(ClaimTypes.Name);
            var providerId = info.ProviderKey;
            switch (info.LoginProvider)
            {
                case "Twitch":
                    user = new ApplicationUser()
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserName = username,
                        Email = email,
                        EmailConfirmed = true,
                        TwitchId = providerId
                    };
                    break;
                case "Steam":
                    user = new ApplicationUser()
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserName = username,
                        Email = email,
                        EmailConfirmed = true,
                        SteamID = _steamService.SteamUserId(providerId)
                    };
                    break;
                case "BattleNet":
                    user = new ApplicationUser()
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserName = username,
                        Email = email,
                        EmailConfirmed = true,
                        SteamID = _steamService.SteamUserId(providerId)
                    };
                    break;
                default:
                    break;
            }

			IdentityResult result = await _userManager.CreateAsync(user);
			if (!result.Succeeded)
			{
				return result;
			}
		
			return await _userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.LoginProvider));
		}
        private async Task<IdentityResult> AddLogin(ApplicationUser user, ExternalLoginInfo info)
        {
            var providerId = info.ProviderKey;
            IdentityResult result = await _userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
            if (result.Succeeded)
            {
                switch (info.LoginProvider)
                {
                    case "Twitch":
                        if (user.TwitchId != providerId)
                        {
                            user.TwitchId = providerId;
                            await _dbContext.SaveChangesAsync();
                        }
                        break;
                    case "Steam":
                        string steamid = _steamService.SteamUserId(providerId);
                        if (user.SteamID != steamid)
                        {
                            user.SteamID = steamid;
                            await _dbContext.SaveChangesAsync();
                        }
                        break;
                    case "BattleNet":
                        var username = info.Principal.FindFirstValue(ClaimTypes.Name);
                        if (user.BattleNetId != username)
                        {
                            user.BattleNetId = username;
                            await _dbContext.SaveChangesAsync();
                        }
                        break;
                    default:
                        break;
                }
            }

            return result;
        }

        private async Task<IdentityResult> Register(RegisterBindingModel info)
        {
            var user = new ApplicationUser()
            {
                Id = Guid.NewGuid().ToString(),
                UserName = info.UserName,
                Email = info.Email
            };

            IdentityResult result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                result = await _userManager.AddPasswordAsync(user, info.Password);
                if (!result.Succeeded)
                {
                    await _userManager.DeleteAsync(user);
                }
                else
                {
                    string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var callbackUrl = Url.RouteUrl("ConfirmEmail", new { userId = user.Id, code }, Request.Scheme);
                    await _sender.SendEmailAsync(info.Email, "Confirm your email", string.Format(emailConfirmation, callbackUrl));
                }
            }
            return result;
        }

        private IActionResult GetErrorResult(IdentityResult result)
        {
            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (IdentityError error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }

        private class ExternalLoginData
        {
            public string LoginProvider { get; set; }
            public string ProviderKey { get; set; }
            public string UserName { get; set; }

            public IList<Claim> GetClaims()
            {
                IList<Claim> claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, ProviderKey, null, LoginProvider)
                };

                if (UserName != null)
                {
                    claims.Add(new Claim(ClaimTypes.Name, UserName, null, LoginProvider));
                }

                return claims;
            }

            public static ExternalLoginData FromIdentity(ClaimsIdentity identity)
            {
                if (identity == null)
                {
                    return null;
                }

                Claim providerKeyClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

                if (providerKeyClaim == null || String.IsNullOrEmpty(providerKeyClaim.Issuer)
                    || String.IsNullOrEmpty(providerKeyClaim.Value))
                {
                    return null;
                }

                if (providerKeyClaim.Issuer == ClaimsIdentity.DefaultIssuer)
                {
                    return null;
                }

                return new ExternalLoginData
                {
                    LoginProvider = providerKeyClaim.Issuer,
                    ProviderKey = providerKeyClaim.Value,
                    UserName = identity.FindFirst(ClaimTypes.Name).Value
                };
            }
        }

        private static class RandomOAuthStateGenerator
        {
            private static RandomNumberGenerator _random = new RNGCryptoServiceProvider();

            public static string Generate(int strengthInBits)
            {
                const int bitsPerByte = 8;

                if (strengthInBits % bitsPerByte != 0)
                {
                    throw new ArgumentException("strengthInBits must be evenly divisible by 8.", nameof(strengthInBits));
                }

                int strengthInBytes = strengthInBits / bitsPerByte;

                byte[] data = new byte[strengthInBytes];
                _random.GetBytes(data);
                return Base64UrlTextEncoder.Encode(data);
            }
        }

        #endregion
    }
}
