using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Models.Extensions;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
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
			SearchResultViewModel results = new SearchResultViewModel();
			if (!string.IsNullOrEmpty(name))
			{
				results.Teams = _dbContext.CSGOTeams.Where(t => t.Visible && t.TeamName.Contains(name)).ToList();
				results.Strategies = _dbContext.CSGOStrategies.Where(s => s.Visible && s.Title.Contains(name)).ToList();
				List<ApplicationUser> activeUsers = _dbContext.Users.Where(u => u.SearchVisible && u.UserName.Contains(name)).ToList();

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
				return Ok(_dbContext.CSGOTeams.Where(t => t.Visible).OrderBy(t => t.TeamId).Take(50).ToList());
			}

			List<CSGOTeam> teams;
			if (role != null)
			{
				teams = _dbContext.CSGOTeams.Where(t => t.Visible && !t.Members.Any(m => m.Role == role) && t.PracticeSchedule.Any(d => d.Available)).ToList();
			}
			else
			{
				teams = _dbContext.CSGOTeams.Where(t => t.Visible && t.PracticeSchedule.Any(d => d.Available)).ToList();
			}
			if (overlap > 0)
			{
				if (!User.Identity.IsAuthenticated)
				{
					return BadRequest("You must sign in to perform search by availability...");
				}
                ApplicationUser user = await GetAuthUser();
				if (!user.Availability.Any(a => a.Available))
				{
					return BadRequest("You must provide your availability in your user profile...");
				}
				overlap = Math.Min(overlap, user.GetTotalAvailability());
				return Ok(teams.Where(t => t.GetTotalAvailability() >= overlap && t.GetTotalOverlap(user) >= overlap));
			}
			return Ok(teams);
		}

		[Route("Players")]
		[HttpGet]
		public async Task<IActionResult> SearchPlayers(PlaystyleRole? role, double overlap, Guid? teamid)
        {
            List<ApplicationUser> users = new List<ApplicationUser>();
            List<UserStatsViewModel> players = new List<UserStatsViewModel>();
            if (overlap <= 0 && role == null)
			{
				users = _dbContext.Users.Where(u => u.SearchVisible).OrderBy(u => u.Id).Take(50).ToList();

				foreach (ApplicationUser user in users)
				{
					players.Add(new UserStatsViewModel(user));
				}

				return Ok(players);
			}

			if (role != null)
			{
				users = _dbContext.Users.Where(u => u.SearchVisible && (u.PreferredPrimaryRole == role || u.PreferredSecondaryRole == role)).ToList();
			}
			else
			{
				users = _dbContext.Users.Where(u => u.SearchVisible && u.Availability.Any(d => d.Available)).ToList();
			}
			if (overlap > 0)
			{
				if (!User.Identity.IsAuthenticated)
				{
					return BadRequest("You must sign in to perform search by availability...");
				}
				
				if (teamid != null)
				{
					CSGOTeam team = _dbContext.CSGOTeams.Find(teamid);
					overlap = Math.Min(overlap, team.GetTotalAvailability());
					users = users.Where(u => u.GetTotalAvailability() >= overlap && team.GetTotalOverlap(u) >= overlap).ToList();
				}
				else
				{
					ApplicationUser user = await GetAuthUser();
					overlap = Math.Min(overlap, user.GetTotalAvailability());
					if (!user.Availability.Any(a => a.Available))
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
