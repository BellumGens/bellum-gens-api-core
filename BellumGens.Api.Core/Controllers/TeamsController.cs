using System.Collections.Generic;
using System.Linq;
using SteamModels;
using System;
using System.Threading.Tasks;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using BellumGens.Api.Core.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BellumGens.Api.Controllers
{
	[Authorize]
	public class TeamsController : BaseController
	{
		private readonly ISteamService _steamService;
		private readonly INotificationService _notificationService;
		public TeamsController(ISteamService steamService,
							   INotificationService notificationService,
							   UserManager<ApplicationUser> userManager,
							   RoleManager<IdentityRole> roleManager,
							   SignInManager<ApplicationUser> signInManager,
							   IEmailSender sender,
							   BellumGensDbContext context,
							   ILogger<TeamsController> logger) : base(userManager, roleManager, signInManager, sender, context, logger)
		{
			_steamService = steamService;
			_notificationService = notificationService;
		}

		[HttpGet]
		[AllowAnonymous]
		public async Task<CSGOTeam> Get(string teamId)
		{
			return await ResolveTeam(teamId);
		}

		[Route("Members")]
		[AllowAnonymous]
		public async Task<List<TeamMember>> GetTeamMembers(Guid teamId)
		{
			return await _dbContext.TeamMembers.Include(m => m.Member).ThenInclude(u => u.CSGODetails).Where(m => m.TeamId == teamId).ToListAsync();
		}

		[Route("Tournaments")]
		[AllowAnonymous]
		public async Task<IActionResult> GetTournaments(string teamid)
		{
			CSGOTeam team = await ResolveTeam(teamid);
			List<TeamTournamentViewModel> model = new();
			await _dbContext.Tournaments
							.Include(t => t.CSGOMatches)
								.ThenInclude(m => m.Team1)
								.Where(t => t.CSGOMatches.Any(m => m.Team1Id == team.TeamId))
							.Include(t => t.CSGOMatches)
								.ThenInclude(m => m.Team2)
								.Where(t => t.CSGOMatches.Any(m => m.Team2Id == team.TeamId))
							.ForEachAsync(tournament => model.Add(new TeamTournamentViewModel(tournament, team.TeamId)));
			return Ok(model);
		}

		[Route("teamadmin")]
		[HttpGet]
		public async Task<IActionResult> GetIsTeamAdmin(string teamid)
		{
			if (Guid.TryParse(teamid, out Guid id))
            {
				return Ok(await UserIsTeamAdmin(id));
			}
			ApplicationUser user = await GetAuthUser();
			return Ok(await _dbContext.CSGOTeams.AnyAsync(t => t.CustomUrl == teamid && t.Members.Any(m => m.UserId == user.Id && m.IsAdmin)));
		}

		[Route("teammember")]
		[HttpGet]
		public async Task<IActionResult> GetIsTeamMember(Guid teamid)
		{
			return Ok(await UserIsTeamMember(teamid));
		}

		[Route("teameditor")]
		[HttpGet]
		public async Task<IActionResult> GetIsTeamEditor(Guid teamid)
		{
			return Ok(await UserIsTeamEditor(teamid));
		}

		[Route("MapPool")]
		public async Task<IActionResult> GetTeamMapPool(Guid teamId)
		{
			if (!await UserIsTeamMember(teamId))
			{
				return BadRequest("You're not a member of this team.");
			}
			return Ok(await _dbContext.TeamMapPools.Where(t => t.TeamId == teamId).ToListAsync());
		}

		[Route("SteamMembers")]
		public async Task<IActionResult> GetSteamGroupMembers(string members)
		{
			List<SteamUserSummary> groupMembers = await _steamService.GetSteamUsersSummary(members);
			return Ok(groupMembers);
		}

		[Route("Team")]
		[HttpPost]
		public async Task<IActionResult> TeamFromSteamGroup(SteamUserGroup group)
		{
            ApplicationUser user = await GetAuthUser();
            if (!await _steamService.VerifyUserIsGroupAdmin(user.Id, group.groupID64))
			{
				return BadRequest("User is not a steam group owner for " + group.groupName);
			}

			CSGOTeam team = new()
			{
				SteamGroupId = group.groupID64,
				TeamName = group.groupName,
				TeamAvatar = group.avatarFull
			};
			_dbContext.CSGOTeams.Add(team);
			team.InitializeDefaults();
			team.UniqueCustomUrl(_dbContext);

			team.Members.Add(new TeamMember()
			{
				UserId = user.Id,
				IsActive = true,
				IsAdmin = true,
				IsEditor = true
			});

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceWarning($"Attempting to register steam group twice: ${group.groupID64} msg: ${e.Message}");
				return BadRequest(group.groupName + " Steam group has already been registered.");
			}
			return Ok(team);
		}

		[Route("Team")]
		[HttpPut]
		public async Task<IActionResult> UpdateTeam(CSGOTeam team)
		{
			if (!await UserIsTeamAdmin(team.TeamId))
			{
				return BadRequest("User is not a team admin for " + team.TeamName);
			}

			if (ModelState.IsValid)
			{
				_dbContext.Attach(team).State = EntityState.Modified;

				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError($"Team update error: ${e.Message}");
					return BadRequest("Something went wrong!");
				}
				return Ok(team);
			}
			return BadRequest("Invalid state of the team " + team.TeamName);
		}

		[Route("NewTeam")]
		[HttpPost]
		public async Task<IActionResult> NewTeam(CSGOTeam team)
		{
            ApplicationUser user = await GetAuthUser();

			_dbContext.CSGOTeams.Add(team);

			team.Members.Add(new TeamMember()
			{
				UserId = user.Id,
				IsActive = true,
				IsAdmin = true,
				IsEditor = true
			});
			team.InitializeDefaults();
			team.UniqueCustomUrl(_dbContext);

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team create error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}
			return Ok(team);
		}

		[Route("Member")]
		[HttpPut]
		public async Task<IActionResult> UpdateTeamMember(TeamMember member)
		{
			if (!await UserIsTeamAdmin(member.TeamId))
			{
				return BadRequest("User is not team admin...");
			}
			TeamMember entity = await _dbContext.TeamMembers.FindAsync(member.TeamId, member.UserId);
			_dbContext.Entry(entity).CurrentValues.SetValues(member);
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team member update error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}
			return Ok();
		}

		[Route("RemoveMember")]
		[HttpDelete]
		public async Task<IActionResult> RemoveTeamMember(Guid teamId, string userId)
		{
			if (!await UserIsTeamAdmin(teamId))
			{
				return BadRequest("User is not team admin...");
			}
			TeamMember entity = await _dbContext.TeamMembers.FindAsync(teamId, userId);
			_dbContext.TeamMembers.Remove(entity);
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team member delete error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}
			return Ok();
		}

		[Route("Abandon")]
		[HttpDelete]
		public async Task<IActionResult> AbandonTeam(Guid teamId)
		{
            ApplicationUser user = await GetAuthUser();
			CSGOTeam team = await _dbContext.CSGOTeams.FindAsync(teamId);
			await _dbContext.Entry(team).Collection(t => t.Members).LoadAsync();
			object response = new { removed = false };
			if (team.Members.Count == 1)
			{
				_dbContext.CSGOTeams.Remove(team);
				response = new { removed = true };
			}
			else
            {
				TeamMember entity = team.Members.FirstOrDefault(m => m.UserId == user.Id);
				if (entity != null)
				{
					team.Members.Remove(entity);
					if (!team.Members.Any(m => m.IsAdmin))
					{
						team.Members.First().IsAdmin = true;
					}
				}
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team abandon error: ${e.Message}");
				return BadRequest("Could not remove team because there's an active tournament registration associated with it!");
			}
			return Ok(response);
		}

		[Route("Invite")]
		[HttpPost]
		public async Task<IActionResult> InviteToTeam(InviteModel model)
		{
			if (!await UserIsTeamAdmin(model.TeamId))
			{
				return BadRequest("User is not team admin...");
			}

			ApplicationUser invitingUserEntity = await GetAuthUser();
			TeamInvite invite = await _dbContext.TeamInvites.FindAsync(invitingUserEntity.Id, model.UserId, model.TeamId);
			
			if (invite != null)
			{
				if (invite.State != NotificationState.NotSeen)
				{
					invite.State = NotificationState.NotSeen;
				}
			}
			else
			{
				invite = new TeamInvite()
				{
					InvitedUserId = model.UserId,
					InvitingUserId = invitingUserEntity.Id,
					TeamId = model.TeamId
				};
				_dbContext.TeamInvites.Add(invite);
			}
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team invite error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}
			List<BellumGensPushSubscription> subs = await _dbContext.BellumGensPushSubscriptions.Where(sub => sub.UserId == model.UserId).ToListAsync();
			await _notificationService.SendNotificationAsync(subs, invite);
			return Ok(model.UserId);
		}

		[Route("Apply")]
		[HttpPost]
		public async Task<IActionResult> ApplyToTeam(TeamApplication application)
		{
			if (ModelState.IsValid)
			{
				TeamApplication entity = await _dbContext.TeamApplications.FindAsync(application.ApplicantId, application.TeamId);
				if (entity != null)
				{
					entity.Message = application.Message;
					entity.Sent = DateTimeOffset.Now;
					entity.State = NotificationState.NotSeen;
				}
				else
				{
					_dbContext.TeamApplications.Add(application);
				}

				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError($"Team application error: ${e.Message}");
					return BadRequest("Something went wrong...");
				}
				TeamMember admin = await _dbContext.TeamMembers.Where(m => m.TeamId == application.TeamId && m.IsAdmin).FirstOrDefaultAsync();
				try
				{
					List<BellumGensPushSubscription> subs = await _dbContext.BellumGensPushSubscriptions.Where(s => s.UserId == admin.UserId).ToListAsync();
					await _notificationService.SendNotificationAsync(subs, application);
				}
				catch (Exception e)
				{
					System.Diagnostics.Trace.TraceError($"Push sub error: ${e.Message}");
				}
				return Ok(application);
			}
			return BadRequest("Something went wrong with your application validation");
		}

		[Route("Applications")]
		public async Task<IActionResult> GetTeamApplications(Guid teamId)
		{
			if (!await UserIsTeamAdmin(teamId))
			{
				return BadRequest("You need to be team admin.");
			}

			return Ok(await _dbContext.TeamApplications.Where(a => a.TeamId == teamId).OrderByDescending(n => n.Sent).ToListAsync());
		}

		[Route("ApproveApplication")]
		[HttpPut]
		public async Task<IActionResult> ApproveApplication(TeamApplication application)
		{
			if (!await UserIsTeamAdmin(application.TeamId))
			{
				return BadRequest("You need to be team admin.");
			}

			TeamApplication entity = await _dbContext.TeamApplications.FindAsync(application.ApplicantId, application.TeamId);
			entity.State = NotificationState.Accepted;

			_dbContext.TeamMembers.Add(new TeamMember()
			{
				UserId = application.ApplicantId,
				TeamId = application.TeamId,
				IsActive = true,
				IsAdmin = false,
				IsEditor = false
			});
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team application approve error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}

			try
			{
				List<BellumGensPushSubscription> subs = await _dbContext.BellumGensPushSubscriptions.Where(s => s.UserId == entity.ApplicantId).ToListAsync();
				await _notificationService.SendNotificationAsync(subs, application, NotificationState.Accepted);
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.TraceWarning($"Team application approve push notification fail: ${e.Message}");
			}

			return Ok(entity);
		}

		[Route("RejectApplication")]
		[HttpPut]
		public async Task<IActionResult> RejectApplication(TeamApplication application)
		{
			if (!await UserIsTeamAdmin(application.TeamId))
			{
				return BadRequest("You need to be team admin.");
			}

			TeamApplication entity = await _dbContext.TeamApplications.FindAsync(application.ApplicantId, application.TeamId);
			entity.State = NotificationState.Rejected;
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team application reject error: ${e.Message}");
				return BadRequest("Something went wrong... ");
			}
			return Ok("ok");
		}

		[Route("MapPool")]
		[HttpPut]
		public async Task<IActionResult> SetTeamMapPool(List<TeamMapPool> maps)
		{
			if (!await UserIsTeamAdmin(maps[0].TeamId))
			{
				return BadRequest("You need to be team admin.");
			}

			foreach (TeamMapPool mapPool in maps)
			{
				TeamMapPool entity = await _dbContext.TeamMapPools.FindAsync(mapPool.TeamId, mapPool.Map);
				_dbContext.Entry(entity).CurrentValues.SetValues(mapPool);
			}
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team map pool error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}
			return Ok("ok");
		}

		[Route("availability")]
		[AllowAnonymous]
		[HttpGet]
		public async Task<IActionResult> GetTeamAvailability(Guid teamid)
		{
			return Ok(await _dbContext.TeamAvailabilities.Where(t => t.TeamId == teamid).ToListAsync());
		}

		[Route("availability")]
		[HttpPut]
		public async Task<IActionResult> SetTeamAvailability(TeamAvailability day)
		{
			if (!await UserIsTeamAdmin(day.TeamId))
			{
				return BadRequest("You need to be team admin.");
			}

			TeamAvailability entity = await _dbContext.TeamAvailabilities.FindAsync(day.TeamId, day.Day);
			_dbContext.Entry(entity).CurrentValues.SetValues(day);
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError($"Team availability error: ${e.Message}");
				return BadRequest("Something went wrong...");
			}
			return Ok(entity);
		}
	}

	public class InviteModel
	{
		public string UserId { get; set; }
		public Guid TeamId { get; set; }
	}
}
