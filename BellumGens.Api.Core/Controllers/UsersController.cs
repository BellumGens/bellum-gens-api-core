using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
                               EmailServiceProvider sender,
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
            if (user.SteamUser != null)
            {
				registered = await _dbContext.Users.Include(u => u.CSGODetails).Include(u => u.StarCraft2Details).Include(u => u.MemberOf).ThenInclude(m => m.Team).FirstOrDefaultAsync(u => u.SteamID == user.SteamUser.steamID64);
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
            return Ok(user?.SteamUser?.groups);
        }

		[Route("UserTeams")]
		[AllowAnonymous]
		public async Task<IActionResult> GetUserTeams(string userid)
		{
			List<CSGOTeamSummaryViewModel> teams = new();
			await _dbContext.TeamMembers.Where(m => m.UserId == userid).Include(m => m.Team).Select(m => m.Team)
				.ForEachAsync(team =>
				{
					teams.Add(new CSGOTeamSummaryViewModel(team));
				});
			return Ok(teams);
		}

		[Route("Availability")]
		[AllowAnonymous]
		[HttpGet]
		public async Task<IActionResult> GetAvailability(string userid)
		{
			List<UserAvailability> availabilities = await _dbContext.UserAvailabilities.Where(u => u.UserId == userid).ToListAsync();
			return Ok(availabilities);
		}

		[Route("Availability")]
		[HttpPut]
		public async Task<IActionResult> SetAvailability(UserAvailability newAvailability)
		{
			if (newAvailability.Available)
            {
				bool exists = _dbContext.UserAvailabilities.Any(a => a.UserId == newAvailability.UserId && a.Day == newAvailability.Day);
				if (exists)
                {
					_dbContext.UserAvailabilities.Update(newAvailability);
                }
				else
                {
					_dbContext.UserAvailabilities.Add(newAvailability);
				}
            }
			else
            {
				_dbContext.UserAvailabilities.Remove(newAvailability);
            }

			try
			{
				await _dbContext.SaveChangesAsync();
			}
            catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User availability error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(newAvailability);
		}

		[Route("MapPool")]
		[AllowAnonymous]
		[HttpGet]
		public async Task<IActionResult> GetMapPool(string userid)
		{
			List<UserMapPool> mappool = await _dbContext.UserMapPool.Where(u => u.UserId == userid).ToListAsync();
			return Ok(mappool);
		}

		[Route("mapPool")]
		[HttpPut]
		public async Task<IActionResult> SetMapPool(UserMapPool mapPool)
		{
			if (mapPool.IsPlayed)
            {
				_dbContext.UserMapPool.Add(mapPool);
            }
			else if (_dbContext.UserMapPool.Contains(mapPool))
            {
				_dbContext.UserMapPool.Remove(mapPool);
            }

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User map pool error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(mapPool);
		}
		
		[Route("PrimaryRole")]
		[HttpPut]
		public async Task<IActionResult> SetPrimaryRole(PlaystyleRole id, Role role)
		{
			ApplicationUser user = await GetAuthUser();
			user.CSGODetails.PreferredPrimaryRole = id;
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User primary role error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(role);
		}
		
		[Route("SecondaryRole")]
		[HttpPut]
		public async Task<IActionResult> SetSecondaryRole(PlaystyleRole id, Role role)
		{
			ApplicationUser user = await GetAuthUser();
			user.CSGODetails.PreferredSecondaryRole = id;
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User secondary role error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(role);
		}

		[Route("AcceptTeamInvite")]
		[HttpPut]
		public async Task<IActionResult> AcceptTeamInvite(TeamInvite invite)
		{
			TeamInvite entity = await _dbContext.TeamInvites.FindAsync(invite.InvitingUserId, invite.InvitedUserId, invite.TeamId);
			if (entity == null)
			{
				return NotFound();
			}

			ApplicationUser user = await GetAuthUser();
			if (invite.InvitedUserId != user.Id)
			{
				return BadRequest("This invite was not sent to you...");
			}
			CSGOTeam team = await _dbContext.CSGOTeams.FindAsync(invite.TeamId);
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
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User team invite accept error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}
			List<BellumGensPushSubscription> subs = await _dbContext.BellumGensPushSubscriptions.Where(s => s.UserId == entity.InvitingUser.Id).ToListAsync();
			await _notificationService.SendNotificationAsync(subs, entity, NotificationState.Accepted);
			return Ok(entity);
		}

		[Route("RejectTeamInvite")]
		[HttpPut]
		public async Task<IActionResult> RejectTeamInvite(TeamInvite invite)
		{
			TeamInvite entity = await _dbContext.TeamInvites.FindAsync(invite.InvitingUserId, invite.InvitedUserId, invite.TeamId);
			if (entity == null)
			{
				return NotFound();
			}

			entity.State = NotificationState.Rejected;
			try
			{
				await _dbContext.SaveChangesAsync();
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
