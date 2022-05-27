using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Models.Extensions;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
	public class SearchController : BaseController
	{
		private readonly ISteamService _steamService;

		public SearchController(ISteamService steamService, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, IEmailSender sender, BellumGensDbContext context, ILogger<SearchController> logger)
			: base(userManager, roleManager, signInManager, sender, context, logger)
		{
			_steamService = steamService;
		}

		[HttpGet]
		public async Task<IActionResult> Get(string name)
		{
			SearchResultViewModel results = new();
			if (!string.IsNullOrEmpty(name))
			{
				results.Teams = await _dbContext.CSGOTeams.Where(t => t.Visible && t.TeamName.Contains(name)).ToListAsync();
				results.Strategies = await _dbContext.CSGOStrategies.Where(s => s.Visible && s.Title.Contains(name) || s.Description.Contains(name)).ToListAsync();
				List<ApplicationUser> activeUsers = await _dbContext.Users.Include(u => u.CSGODetails).Where(u => u.SearchVisible && u.UserName.Contains(name)).ToListAsync();

				foreach (ApplicationUser user in activeUsers)
				{
					results.Players.Add(new UserStatsViewModel(user));
				}
				if (!activeUsers.Any(u => u.UserName == name))
				{
					results.SteamUser = await _steamService.GetSteamUserDetails(name);
				}

				return Ok(results);
			}
			return Ok(results);
		}

		[Route("Teams")]
		[HttpGet]
		public async Task<IActionResult> SearchTeams(PlaystyleRole? role, double overlap)
		{
			if (overlap <= 0 && role == null)
			{
				return Ok(await _dbContext.CSGOTeams.Where(t => t.Visible).ToListAsync());
			}

			List<CSGOTeam> teams;
			if (role != null)
			{
				teams = await _dbContext.CSGOTeams.Include(t => t.PracticeSchedule).Where(t => t.Visible && !t.Members.Any(m => m.Role == role) && t.PracticeSchedule.Any(d => d.Available)).ToListAsync();
			}
			else
			{
				teams = await _dbContext.CSGOTeams.Include(t => t.PracticeSchedule).Where(t => t.Visible && t.PracticeSchedule.Any(d => d.Available)).ToListAsync();
			}
			if (overlap > 0)
			{
				if (!User.Identity.IsAuthenticated)
				{
					return BadRequest("You must sign in to perform search by availability...");
				}
                ApplicationUser user = await GetAuthUser();
				List<UserAvailability> availabilities = await _dbContext.UserAvailabilities.Where(a => a.UserId == user.Id).ToListAsync();
				if (!availabilities.Any(a => a.Available))
				{
					return BadRequest("You must provide your availability in your user profile...");
				}
				overlap = Math.Min(overlap, availabilities.GetTotalAvailability());
				return Ok(teams.Where(t => t.GetTotalAvailability() >= overlap && t.GetTotalOverlap(user) >= overlap));
			}
			return Ok(teams);
		}

		[Route("Players")]
		[HttpGet]
		public async Task<IActionResult> SearchPlayers(PlaystyleRole? role, double overlap, Guid? teamid)
        {
            List<ApplicationUser> users = new();
            List<UserStatsViewModel> players = new();
            if (overlap <= 0 && role == null)
			{
				users = await _dbContext.Users.Where(u => u.SearchVisible).ToListAsync();

				foreach (ApplicationUser user in users)
				{
					players.Add(new UserStatsViewModel(user));
				}

				return Ok(players);
			}

			if (role != null)
			{
				users = await _dbContext.Users
										.Include(u => u.Availability)
										.Include(u => u.CSGODetails)
										.Where(u => u.SearchVisible && (u.PreferredPrimaryRole == role || u.PreferredSecondaryRole == role)).ToListAsync();
			}
			else
			{
				users = await _dbContext.Users
										.Include(u => u.Availability)
										.Include(u => u.CSGODetails)
										.Where(u => u.SearchVisible && u.Availability.Any(d => d.Available)).ToListAsync();
			}
			if (overlap > 0)
			{
				if (!User.Identity.IsAuthenticated)
				{
					return BadRequest("You must sign in to perform search by availability...");
				}
				
				if (teamid != null)
				{
					List<TeamAvailability> availabilities = await _dbContext.TeamAvailabilities.Where(t => t.TeamId == teamid).ToListAsync();
					overlap = Math.Min(overlap, availabilities.GetTotalAvailability());
					users = users.Where(u => u.GetTotalAvailability() >= overlap && availabilities.GetTotalOverlap(u) >= overlap).ToList();
				}
				else
				{
					ApplicationUser user = await GetAuthUser();
					List<UserAvailability> availability = await _dbContext.UserAvailabilities.Where(a => a.UserId == user.Id).ToListAsync();
					overlap = Math.Min(overlap, availability.GetTotalAvailability());
					if (!availability.Any(a => a.Available))
					{
						return BadRequest("You must provide your availability in your user profile...");
					}
					users = users.Where(u => u.GetTotalAvailability() >= overlap && u.GetTotalOverlap(user) >= overlap).ToList();
				}

				foreach (ApplicationUser user in users)
				{
					players.Add(new UserStatsViewModel(user));
				}
				return Ok(players);
			}

			foreach (ApplicationUser user in users)
			{
				players.Add(new UserStatsViewModel(user));
			}
			return Ok(players);
		}
    }
}
