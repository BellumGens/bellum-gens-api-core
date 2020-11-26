using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
	[Authorize]
	public class UsersController : BaseController
    {
		private readonly ISteamService _steamService;
		private readonly INotificationService _notificationService;
		public UsersController(ISteamService steamService,
							   INotificationService notificationService,
							   UserManager<ApplicationUser> userManager,
							   RoleManager<IdentityRole> roleManager,
							   SignInManager<ApplicationUser> signInManager,
							   IEmailSender sender,
							   BellumGensDbContext context,
							   ILogger<UsersController> logger) : base(userManager, roleManager, signInManager, sender, context, logger)
		{
			_steamService = steamService;
			_notificationService = notificationService;
		}

		[HttpGet]
		[AllowAnonymous]
		public async Task<IActionResult> Get(string userid)
		{
			UserStatsViewModel user = await _steamService.GetSteamUserDetails(userid);
            ApplicationUser registered = null;
            if (user.steamUser != null)
            {
				registered = _dbContext.Users.Include(u => u.MemberOf).FirstOrDefault(u => u.SteamID == user.steamUser.steamID64);
            }
			if (registered != null)
			{
				user.SetUser(registered, _dbContext);
			}
			return Ok(user);
		}

        [Route("UserGroups")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserGroups(string userid)
        {
            UserStatsViewModel user = await _steamService.GetSteamUserDetails(userid);
            return Ok(user?.steamUser?.groups);
        }

        [Route("Availability")]
		[HttpPut]
		public async Task<IActionResult> SetAvailability(UserAvailability newAvailability)
		{
			ApplicationUser user = await GetAuthUser();
			UserAvailability entity = _dbContext.Users.Find(user.Id).Availability.First(a => a.Day == newAvailability.Day);
			_dbContext.Entry(entity).CurrentValues.SetValues(newAvailability);
			try
			{
				_dbContext.SaveChanges();
			}
            catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User availability error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(entity);
		}
		
		[Route("mapPool")]
		[HttpPut]
		public async Task<IActionResult> SetMapPool(UserMapPool mapPool)
		{
			ApplicationUser user = await GetAuthUser();
			UserMapPool userMap = _dbContext.Users.Find(user.Id).MapPool.First(m => m.Map == mapPool.Map);
			_dbContext.Entry(userMap).CurrentValues.SetValues(mapPool);
			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User map pool error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(userMap);
		}
		
		[Route("PrimaryRole")]
		[HttpPut]
		public async Task<IActionResult> SetPrimaryRole(Role role)
		{
			ApplicationUser user = await GetAuthUser();
			_dbContext.Users.Find(user.Id).PreferredPrimaryRole = role.Id;
			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User primary role error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok("success");
		}
		
		[Route("SecondaryRole")]
		[HttpPut]
		public async Task<IActionResult> SetSecondaryRole(Role role)
		{
			ApplicationUser user = await GetAuthUser();
			_dbContext.Users.Find(user.Id).PreferredSecondaryRole = role.Id;
			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User secondary role error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok("success");
		}

		[Route("AcceptTeamInvite")]
		[HttpPut]
		public async Task<IActionResult> AcceptTeamInvite(TeamInvite invite)
		{
			TeamInvite entity = _dbContext.TeamInvites.Find(invite.InvitingUserId, invite.InvitedUserId, invite.TeamId);
			if (entity == null)
			{
				return NotFound();
			}

			ApplicationUser user = await GetAuthUser();
			if (invite.InvitedUserId != user.Id)
			{
				return BadRequest("This invite was not sent to you...");
			}
			CSGOTeam team = _dbContext.Teams.Find(invite.TeamId);
			team.Members.Add(new TeamMember()
			{
				UserId = user.Id,
				IsActive = true,
                IsAdmin = false,
				IsEditor = false
			});
			entity.State = NotificationState.Accepted;
			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User team invite accept error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}
			List<BellumGensPushSubscription> subs = _dbContext.PushSubscriptions.Where(s => s.userId == entity.InvitingUser.Id).ToList();
			_notificationService.SendNotificationAsync(subs, entity, NotificationState.Accepted);
			return Ok(entity);
		}

		[Route("RejectTeamInvite")]
		[HttpPut]
		public IActionResult RejectTeamInvite(TeamInvite invite)
		{
			TeamInvite entity = _dbContext.TeamInvites.Find(invite.InvitingUserId, invite.InvitedUserId, invite.TeamId);
			if (entity == null)
			{
				return NotFound();
			}

			entity.State = NotificationState.Rejected;
			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User team invite reject error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(entity);
		}
	}
}
