using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
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
	[Authorize]
	public class StrategyController : BaseController
	{
		private readonly IFileService _fileService;
		private readonly INotificationService _notificationService;
		public StrategyController(IFileService fileService,
								  INotificationService notificationService,
								  UserManager<ApplicationUser> userManager,
								  RoleManager<IdentityRole> roleManager,
								  SignInManager<ApplicationUser> signInManager,
								  IEmailSender sender,
								  BellumGensDbContext context,
								  ILogger<StrategyController> logger) : base(userManager, roleManager, signInManager, sender, context, logger)
		{
			_fileService = fileService;
			_notificationService = notificationService;
		}

		[Route("Strategies")]
		[AllowAnonymous]
		public IActionResult GetStrategies(int page = 0)
		{
			List<CSGOStrategy> strategies = _dbContext.CSGOStrategies.Include(s => s.Comments)
																	 .Include(s => s.Votes)
																	 .Where(s => s.Visible == true && (!string.IsNullOrEmpty(s.Url) || !string.IsNullOrEmpty(s.StratImage)))
																	 .OrderByDescending(s => s.LastUpdated).Skip(page * 25).Take(25).ToList();
			return Ok(strategies.OrderByDescending(s => s.Rating));
		}

		[Route("teamstrats")]
		public async Task<IActionResult> GetTeamStrats(Guid teamId)
		{
			if (!await UserIsTeamMember(teamId))
			{
				return BadRequest("You're not a member of this team.");
			}
			return Ok(await _dbContext.CSGOStrategies.Include(s => s.Comments).Include(s => s.Votes).Where(s => s.TeamId == teamId).ToListAsync());
		}

		[Route("userstrats")]
		public async Task<IActionResult> GetUserStrats(string userId)
		{
			ApplicationUser user = await GetAuthUser();
			if (user.Id == userId)
			{
				var strategies = _dbContext.CSGOStrategies.Where(s => s.UserId == userId).OrderByDescending(s => s.LastUpdated).ToList();
				return Ok(strategies);
			}
			return BadRequest("You need to authenticate first.");
		}

		[Route("Strat")]
		[AllowAnonymous]
		public async Task<IActionResult> GetStrat(string stratId)
		{
			CSGOStrategy strat = await ResolveStrategy(stratId);
			if (strat != null && !strat.Visible && strat.TeamId != null && strat.TeamId != Guid.Empty)
			{
				if (!await UserIsTeamMember(strat.TeamId.Value))
				{
					return BadRequest("You need to be team editor.");
				}
			}

			if (strat != null)
			{
				return Ok(strat);
			}
			return BadRequest("Strat not found or user is not team member.");
		}

		[Route("Strategy")]
		[HttpPost]
		public async Task<IActionResult> SubmitStrategy(CSGOStrategy strategy)
		{
			if (strategy.TeamId != null && strategy.TeamId != Guid.Empty)
			{
				if (!await UserIsTeamEditor(strategy.TeamId.Value))
				{
					return BadRequest("You need to be team editor.");
				}
			}

			CSGOStrategy entity = await UserCanEdit(strategy.Id);
			if (entity == null)
			{
                ApplicationUser user = await GetAuthUser();
                strategy.UserId = user.Id;
				strategy.UniqueCustomUrl(_dbContext);
				strategy.StratImage = _fileService.SaveImageFile(strategy.StratImage, strategy.CustomUrl);
				_dbContext.CSGOStrategies.Add(strategy);
			}
			else
			{
                if (string.IsNullOrEmpty(entity.UserId))
                {
                    ApplicationUser user = await GetAuthUser();
                    strategy.UserId = user.Id;
                }
				strategy.LastUpdated = DateTimeOffset.Now;
				strategy.StratImage = _fileService.SaveImageFile(strategy.StratImage, strategy.CustomUrl);
				_dbContext.Entry(entity).CurrentValues.SetValues(strategy);
			}

			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("User Strategy submit error: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(entity);
		}

		[Route("Strat")]
		[HttpDelete]
		public async Task<IActionResult> DeleteStrategy(Guid id)
		{
			CSGOStrategy entity = await UserCanEdit(id);
			if (entity == null)
			{
				return BadRequest("You need to be team editor.");
			}

			if (entity.TeamId != null && entity.TeamId != Guid.Empty)
			{
				if (!await UserIsTeamEditor(entity.TeamId.Value))
				{
					return BadRequest("You need to be team editor to delete this strategy.");
				}
			}

			_dbContext.CSGOStrategies.Remove(entity);

			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Delete user strategy error: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok("Ok");
		}

		[Route("Vote")]
		[HttpPost]
		public async Task<IActionResult> SubmitStrategyVote(VoteModel model)
		{
			ApplicationUser user = await GetAuthUser();

			StrategyVote vote = _dbContext.StrategyVotes.Find(model.id, user.Id);
			if (vote == null)
			{
				vote = new StrategyVote()
				{
					StratId = model.id,
					UserId = user.Id,
					Vote = model.direction
				};
				_dbContext.StrategyVotes.Add(vote);
			}
			else
			{
				if (vote.Vote == model.direction)
				{
					_dbContext.StrategyVotes.Remove(vote);
					vote = null;
				}
				else
				{
					vote.Vote = model.direction;
				}
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Vote user strategy error: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(vote);
		}

		[Route("Comment")]
		[HttpPost]
		public async Task<IActionResult> SubmitStrategyComment(StrategyComment comment)
		{
			ApplicationUser user = await GetAuthUser();
			CSGOStrategy strat = null;

			comment.User = user;

			var entity = _dbContext.StrategyComments.Find(comment.Id);
			if (entity != null)
			{
				_dbContext.Entry(entity).CurrentValues.SetValues(comment);
			}
			else
			{
				_dbContext.StrategyComments.Add(comment);
				strat = _dbContext.CSGOStrategies.Find(comment.StratId);
			}

			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Comment user strategy error: " + e.Message);
				return BadRequest("Something went wrong...");
			}

			if (strat != null && strat.UserId != user.Id)
			{
				List<BellumGensPushSubscription> subs = _dbContext.BellumGensPushSubscriptions.Where(s => s.userId == comment.Strategy.UserId).ToList();
				await _notificationService.SendNotificationAsync(subs, comment);
			}
			return Ok(comment);
		}

		[Route("Comment")]
		[HttpDelete]
		public async Task<IActionResult> DeleteStrategyComment(Guid id)
		{
			ApplicationUser user = await GetAuthUser();

			var comment = _dbContext.StrategyComments.Find(id);
			if (comment == null || comment.UserId != user.Id)
			{
				return BadRequest("Could not delete this user comment...");
			}

			_dbContext.StrategyComments.Remove(comment);
			try
			{
				_dbContext.SaveChanges();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Delete user strategy comment error: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(comment);
		}


		private async Task<CSGOStrategy> UserCanEdit(Guid id)
		{
			ApplicationUser user = await GetAuthUser();
            CSGOStrategy strat = _dbContext.CSGOStrategies.Find(id);
            if (strat?.TeamId != null)
            {
                if (strat.Team.Members.Any(m => m.IsEditor || m.IsAdmin && m.UserId == user.Id))
                {
                    return strat;
                }
            }
            else if (strat?.UserId == user.Id)
            {
                return strat;
            }
            return null;
		}

		private async Task<CSGOStrategy> ResolveStrategy(string stratId)
		{
			Guid.TryParse(stratId, out Guid id);
			return await _dbContext.CSGOStrategies.Include(s => s.Comments).Include(s => s.Votes).FirstOrDefaultAsync(s => s.CustomUrl == stratId || s.Id == id);
		}
	}
}
