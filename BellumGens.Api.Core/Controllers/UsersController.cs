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
		private readonly IBattleNetService _battleNetService;
		private readonly INotificationService _notificationService;
		public UsersController(ISteamService steamService,
								IBattleNetService battleNetService,
								INotificationService notificationService,
								UserManager<ApplicationUser> userManager,
								RoleManager<IdentityRole> roleManager,
								SignInManager<ApplicationUser> signInManager,
								IEmailService sender,
								BellumGensDbContext context,
								ILogger<UsersController> logger) : base(userManager, roleManager, signInManager, sender, context, logger)
		{
			_steamService = steamService;
			_battleNetService = battleNetService;
			_notificationService = notificationService;
		}

		[HttpGet]
		[AllowAnonymous]
		public async Task<IActionResult> Get(string userid)
		{
			ApplicationUser registered = await _dbContext.Users.Include(u => u.CSGODetails).Include(u => u.StarCraft2Details).Include(u => u.MemberOf).ThenInclude(m => m.Team).FirstOrDefaultAsync(u => u.Id == userid);
			UserStatsViewModel user = new UserStatsViewModel();

			if (registered != null)
			{
				if (registered.SteamID != null)
				{
					user = await _steamService.GetSteamUserDetails(registered.SteamID) ?? new UserStatsViewModel();
				}

				if (registered.BattleNetId != null)
				{
					user.SC2Player = await _battleNetService.GetStarCraft2Player(registered.BattleNetId);
				}
				user.SetUser(registered, _dbContext);
			}
			else
			{
				user = await _steamService.GetSteamUserDetails(userid);
				if (user?.SteamUser == null)
				{
					return NotFound();
				}
				registered = await _dbContext.Users.Include(u => u.CSGODetails).Include(u => u.StarCraft2Details).Include(u => u.MemberOf).ThenInclude(m => m.Team).FirstOrDefaultAsync(u => u.SteamID == user.SteamUser.steamID64);
				if (registered != null)
				{
					user.SetUser(registered, _dbContext);
				}
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
			ApplicationUser user = await GetAuthUser();
			newAvailability.UserId = user.Id;
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
				System.Diagnostics.Trace.TraceError($"User availability error: {e.Message}");
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
			ApplicationUser user = await GetAuthUser();
			mapPool.UserId = user.Id;
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
				System.Diagnostics.Trace.TraceError($"User map pool error: {e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(mapPool);
		}
		
		[Route("PrimaryRole")]
		[HttpPut]
		public async Task<IActionResult> SetPrimaryRole(PlaystyleRole id, Role role)
		{
			CSGODetails details = await GetAuthUserCSGODetails();
			if (details == null)
			{
				return BadRequest(SteamAccountRequiredMessage);
			}
			details.PreferredPrimaryRole = id;
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User primary role error: {e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(role);
		}
		
		[Route("SecondaryRole")]
		[HttpPut]
		public async Task<IActionResult> SetSecondaryRole(PlaystyleRole id, Role role)
		{
			CSGODetails details = await GetAuthUserCSGODetails();
			if (details == null)
			{
				return BadRequest(SteamAccountRequiredMessage);
			}
			details.PreferredSecondaryRole = id;
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User secondary role error: {e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(role);
		}

		[Route("AcceptTeamInvite")]
		[HttpPut]
		public async Task<IActionResult> AcceptTeamInvite(TeamInvite invite)
		{
			TeamInvite entity = await _dbContext.TeamInvites.FirstOrDefaultAsync(i => i.InvitingUserId == invite.InvitingUserId && i.InvitedUserId == invite.InvitedUserId && i.TeamId == invite.TeamId);
			if (entity == null)
			{
				return NotFound();
			}

			ApplicationUser user = await GetAuthUser();
			if (entity.InvitedUserId != user.Id)
			{
				return BadRequest("This invite was not sent to you...");
			}
			if (!await _dbContext.CSGOTeams.AnyAsync(t => t.TeamId == entity.TeamId))
			{
				return NotFound();
			}
			if (!await _dbContext.TeamMembers.AnyAsync(m => m.TeamId == entity.TeamId && m.UserId == user.Id))
			{
				_dbContext.TeamMembers.Add(new TeamMember()
				{
					TeamId = entity.TeamId,
					UserId = user.Id,
					IsActive = true,
					IsAdmin = false,
					IsEditor = false
				});
			}
			entity.State = NotificationState.Accepted;
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User team invite accept error: {e.Message}");
				return BadRequest("Something went wrong...");
			}
			await _dbContext.Entry(entity).Reference(i => i.Team).LoadAsync();
			await _dbContext.Entry(entity).Reference(i => i.InvitedUser).LoadAsync();
			await _dbContext.Entry(entity.InvitedUser).Reference(u => u.CSGODetails).LoadAsync();
			List<BellumGensPushSubscription> subs = await _dbContext.BellumGensPushSubscriptions.Where(s => s.UserId == entity.InvitingUserId).ToListAsync();
			await _notificationService.SendNotificationAsync(subs, entity, NotificationState.Accepted);
			return Ok(entity);
		}

		[Route("RejectTeamInvite")]
		[HttpPut]
		public async Task<IActionResult> RejectTeamInvite(TeamInvite invite)
		{
			TeamInvite entity = await _dbContext.TeamInvites.FirstOrDefaultAsync(i => i.InvitingUserId == invite.InvitingUserId && i.InvitedUserId == invite.InvitedUserId && i.TeamId == invite.TeamId);
			if (entity == null)
			{
				return NotFound();
			}

			ApplicationUser user = await GetAuthUser();
			if (entity.InvitedUserId != user.Id)
			{
				return BadRequest("This invite was not sent to you...");
			}

			entity.State = NotificationState.Rejected;
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"User team invite reject error: {e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok(entity);
		}

		private const string SteamAccountRequiredMessage = "You need to link a Steam account to set your CS:GO roles...";

		/// <summary>
		/// Lazy loading is disabled, so the CSGODetails navigation on the user returned by the
		/// UserManager has to be loaded explicitly.
		/// </summary>
		private async Task<CSGODetails> GetAuthUserCSGODetails()
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return null;
			}
			await _dbContext.Entry(user).Reference(u => u.CSGODetails).LoadAsync();
			return user.CSGODetails;
		}

		[Route("Tournaments")]
		[AllowAnonymous]
		public async Task<IActionResult> GetTournaments(string userid)
		{
			List<PlayerTournamentViewModel> model = new();

			await _dbContext.Tournaments
							.Include(t => t.SC2Matches)
								.ThenInclude(m => m.Player1)
									.ThenInclude(p1 => p1.StarCraft2Details)
							.Include(t => t.SC2Matches)
								.ThenInclude(m => m.Player2)
									.ThenInclude(p2 => p2.StarCraft2Details)
							.Where(t => t.SC2Matches.Any(m => m.Player1Id == userid || m.Player2Id == userid))
							.OrderByDescending(t => t.StartDate)
							.ForEachAsync(tournament => model.Add(new PlayerTournamentViewModel(tournament, userid)));

			return Ok(model);
		}
	}
}
