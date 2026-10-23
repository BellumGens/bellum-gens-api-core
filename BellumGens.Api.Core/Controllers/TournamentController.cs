using BellumGens.Api.Core;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
	public class TournamentController : BaseController
	{
		private readonly AppConfiguration _appInfo;
		private readonly INotificationService _notificationService;

		public TournamentController(AppConfiguration appInfo, INotificationService notificationsService, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, IEmailService sender, BellumGensDbContext context, ILogger<AccountController> logger)
			: base(userManager, roleManager, signInManager, sender, context, logger)
		{
			_appInfo = appInfo;
			_notificationService = notificationsService;
		}

		#region TOURNAMENTS AND REGISTRATIONS
		[AllowAnonymous]
		[Route("ActiveTournament")]
		public async Task<IActionResult> GetActiveTournament()
		{
			Tournament tournament = await _dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(t => t.Active);
			if (tournament != null)
			{
				tournament.InviteCode = null;
			}
			return Ok(tournament);
		}

		[HttpGet]
		[AllowAnonymous]
		public async Task<IActionResult> Get(Guid id)
		{
			// Private tournaments are unlisted, not hidden: anyone with the id can see them, but only the owner and admins get the invite code
			Tournament tournament = await _dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(t => t.ID == id);
			if (tournament != null && !await CanManageTournament(tournament, await GetAuthUser()))
			{
				tournament.InviteCode = null;
			}
			return Ok(tournament);
		}

		[HttpPost]
		[Route("Register")]
		public async Task<IActionResult> Register(TournamentApplication application)
		{
			if (ModelState.IsValid)
			{
				ApplicationUser user = await GetAuthUser();
				if (user == null)
				{
					return Unauthorized();
				}
				Company c = await _dbContext.Companies.FindAsync(application.CompanyId);
				if (application.Game == Game.StarCraft2)
				{
					if (string.IsNullOrEmpty(application.BattleNetId))
					{
						return BadRequest("Моля попълнете Battle.net battle tag!");
					}
					if (await _dbContext.TournamentApplications.Where(a => a.BattleNetId == application.BattleNetId && a.TournamentId == application.TournamentId).SingleOrDefaultAsync() != null)
					{
						return BadRequest($"Вече има направена регистрация с battle tag {application.BattleNetId}!");
					}
					user.BattleNetId ??= application.BattleNetId;
				}
				else
				{
					if (application.TeamId == null || application.TeamId == Guid.Empty)
					{
						return BadRequest("Моля попълнете отбор във формата за регистрация!");
					}
					if (await _dbContext.TournamentApplications.Where(a => a.TeamId == application.TeamId && a.TournamentId == application.TournamentId).SingleOrDefaultAsync() != null)
					{
						return BadRequest("Вече има направена регистрация за този отбор!");
					}
				}
				if (c == null && application.CompanyId != null)
				{
					_dbContext.Companies.Add(new Company()
					{
						Name = application.CompanyId
					});
				}
				await application.UniqueHash(_dbContext);
				application.UserId = user.Id;
				if (!await UserIsTournamentAdmin())
				{
					application.State = TournamentApplicationState.Pending;
				}
				_dbContext.TournamentApplications.Add(application);
                
				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament registration error: " + e.Message);
					return BadRequest("Нещо се обърка...");
				}

				try
				{
					string gameMsg = application.Game == Game.CSGO ? "Вашата регистрация е за участие в лигата по CS:GO" :
																	$"Вашата регистрация е за участие в лигата по StarCraft II, с battle tag {application.BattleNetId}";
					string message = $@"Здравей, { user.UserName },
									<p>Успешно получихме вашата регистрация за Esports Бизнес Лигата. В регистрацията сте посочили, че текущо работите в <b>{ application.CompanyId }</b>. {gameMsg}. Регистрация ще бъде потвърдена след като преведете таксата за участие (60лв. с ДДС за лигата по StarCraft II, или 300лв. с ДДС за лигата по CS:GO).</p>
									<p>Банковата ни сметка е</p>
									<ul>
										<li>Име на Банката: <b>{ _appInfo.Config.Bank }</b></li>
										<li>Титуляр: <b>{ _appInfo.Config.BankAccountOwner }</b></li>
										<li>Сметка: <b>{ _appInfo.Config.BankAccount }</b></span></li>
										<li>BIC: <b>{ _appInfo.Config.BIC }</b></li>
									</ul>
									<p>Моля при превода да сложите в основанието уникалния код, който сме генерирали за вашата регистрация: <b>{ application.Hash }</b>. Можете да намерите кода и през вашият профил на сайта ни.</p>
									<p>Ако ви е нужна фактура, моля да се свържете с нас на <a href='mailto:info@eb-league.com'>info@eb-league.com</a>!</p>
									<p>Заповядайте и в нашият <a href='https://discord.gg/bnTcpa9'>дискорд сървър</a>!</p>
									<p>Поздрави от екипа на Bellum Gens!</p>
									<a href='https://eb-league.com' target='_blank'>https://eb-league.com</a>";
					await _sender.SendEmailAsync(application.Email, "Регистрацията ви е получена", message).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					System.Diagnostics.Trace.TraceError("Tournament registration error: " + e.Message);
				}
				return Ok(application);
			}
			return BadRequest("Не успяхме да вилидираме информацията...");
		}

		[HttpPut]
		[Route("RegisterBGE")]
		public async Task<IActionResult> RegisterForBGE(Guid? tournamentId, TournamentApplication application)
		{
			if (ModelState.IsValid)
			{
				ApplicationUser user = await GetAuthUser();
				if (user == null)
				{
					return Unauthorized();
				}
				if (tournamentId == null)
				{
					Tournament t = await _dbContext.Tournaments.FindAsync(application.TournamentId);
					if (t == null)
					{
						return BadRequest("Tournament not found.");
					}
					Company c = await _dbContext.Companies.FindAsync(application.CompanyId);
					if (c == null && application.CompanyId != null)
					{
						_dbContext.Companies.Add(new Company()
						{
							Name = application.CompanyId
						});
					}
					await application.UniqueHash(_dbContext);
					application.UserId = user.Id;
					if (!await UserIsTournamentAdmin())
					{
						application.State = TournamentApplicationState.Pending;
					}
					_dbContext.TournamentApplications.Add(application);

					try
					{
						await _dbContext.SaveChangesAsync();
					}
					catch (DbUpdateException e)
					{
						System.Diagnostics.Trace.TraceError("Tournament registration error: " + e.Message);
						return BadRequest("Something went wrong...");
					}

					try
					{
						string message = $@"Greetings, {user.UserName},
									<p>We've received your registration for the {t.Name}, with battle tag {application.BattleNetId}.</p>
									<p>There will be a check-in 2 hours before the matches start. Make sure you check-in on time!</p>
									<p>Thank you from the Bellum Gens team and GLHF!</p>
									<a href='https://bellumgens.com' target='_blank'>https://bellumgens.com</a>";
						await _sender.SendEmailAsync(application.Email, "BGE: Registration Received", message).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						System.Diagnostics.Trace.TraceError("Tournament registration error: " + e.Message);
					}
					return Ok(application);
				}
				else
				{
					TournamentApplication tournamentApplication = await _dbContext.TournamentApplications.FindAsync(application.Id);
					if (tournamentApplication != null)
					{
						bool isAdmin = await UserIsTournamentAdmin();
						if (tournamentApplication.UserId != user.Id && !isAdmin)
						{
							return BadRequest("You can only update your own registration.");
						}
						application.UserId = tournamentApplication.UserId;
						application.Hash = tournamentApplication.Hash;
						application.DateSubmitted = tournamentApplication.DateSubmitted;
						if (!isAdmin)
						{
							application.State = tournamentApplication.State;
							application.TournamentId = tournamentApplication.TournamentId;
						}
						_dbContext.Entry(tournamentApplication).CurrentValues.SetValues(application);
						try
						{
							await _dbContext.SaveChangesAsync();
						}
						catch (DbUpdateException e)
						{
							System.Diagnostics.Trace.TraceError("Tournament registration update error: " + e.Message);
							return BadRequest("Something went wrong!");
						}
						return Ok(application);
					}
				}
			}
			return BadRequest("We couldn't validate your submission...");
		}

		[Route("UserRegistration")]
		public async Task<TournamentApplication> GetRegistrationForTournament(Guid tournamentId)
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return null;
			}
			return await _dbContext.TournamentApplications.Where(a => a.TournamentId == tournamentId && a.UserId == user.Id).FirstOrDefaultAsync();
		}

		[Route("Registrations")]
		public async Task<IActionResult> GetUserRegistrations()
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			return Ok(await _dbContext.TournamentApplications.Include(a => a.Tournament).Where(a => a.UserId == user.Id).ToListAsync());
		}

		[AllowAnonymous]
		[Route("RegCount")]
		public async Task<IActionResult> GetTotalRegistrationsCount(Guid tournamentId)
		{
			List<TournamentApplication> registrations = await _dbContext.TournamentApplications.Where(a => a.Tournament.ID == tournamentId).ToListAsync();
			List<RegistrationCountViewModel> model = new()
			{
				new RegistrationCountViewModel(registrations.Where(r => r.Game == Game.CSGO).Count(), Game.CSGO),
				new RegistrationCountViewModel(registrations.Where(r => r.Game == Game.StarCraft2).Count(), Game.StarCraft2)
			};
			return Ok(model);
		}

		[Route("AllRegistrations")]
		[Authorize(Roles = "admin")]
		public async Task<IActionResult> GetAllApplications()
		{
			return Ok(await _dbContext.TournamentApplications.Include(a => a.Tournament).ToListAsync());
		}

		[Route("TournamentRegistrations")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> GetApplications(Guid tournamentId)
		{
			return Ok(await _dbContext.TournamentApplications.Where(a => a.TournamentId == tournamentId).Include(a => a.Tournament).ToListAsync());
		}

		[Route("ResetState")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> ResetRegistrationsState(Guid tournamentId)
		{
			List<TournamentApplication> applications = await _dbContext.TournamentApplications.Where(a => a.TournamentId == tournamentId && a.State != TournamentApplicationState.Banned).ToListAsync();
			applications.ForEach(applications => applications.State = TournamentApplicationState.Pending);
			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament registration update error: " + e.Message);
				return BadRequest("Something went wrong!");
			}
			return Ok(new { message = $"{applications.Count} registrations have been set to pending state." });
		}

		[Route("SendCheckinEmails")]
		[Authorize(Roles = "admin")]
		public async Task<IActionResult> SendCheckinEmails(Guid tournamentId)
		{
			List<TournamentApplication> applications = await _dbContext.TournamentApplications.Include(a => a.Tournament).Where(a => a.TournamentId == tournamentId && a.State != TournamentApplicationState.Banned).ToListAsync();

			// DbContext is not thread-safe: load every push subscription we need up front, so the
			// parallel loop below only does network I/O (SMTP and web push) and never touches _dbContext.
			List<string> userIds = applications.Where(a => a.UserId != null).Select(a => a.UserId).Distinct().ToList();
			Dictionary<string, List<BellumGensPushSubscription>> subsByUser =
				(await _dbContext.BellumGensPushSubscriptions.Where(s => userIds.Contains(s.UserId)).ToListAsync())
					.GroupBy(s => s.UserId)
					.ToDictionary(g => g.Key, g => g.ToList());

			// The URL helper is request-scoped as well, so build the check-in links sequentially too.
			var checkins = applications
				.Select(app => (app, callbackUrl: Url.ActionLink("WeeklyCheckin", "Tournament", new { id = app.Id, hash = app.Hash })))
				.ToList();

			var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
			await Parallel.ForEachAsync(checkins, options, async (checkin, token) =>
			{
				var (app, callbackUrl) = checkin;
				try
				{
					string message = $@"Greetings, {app.FirstName},
						<p>The checkin for {app.Tournament?.Name} is live. <a href='{callbackUrl}' target='_blank'>Use this link</a> to check in within the next 1 hour.</p>
						<p>All live communications during the matches are performed on our discord server. <a href='https://discord.gg/bnTcpa9' target='_blank'>Join us there</a>!</p>
						<p>Thank you from the Bellum Gens team and GL HF in your matches!</p>
						<a href='https://bellumgens.com' target='_blank'>https://bellumgens.com</a>";
					await _sender.SendEmailAsync(app.Email, "BGE: Time to check in", message).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					System.Diagnostics.Trace.TraceError($"Tournament checkin email error for registration {app.Id}: " + e.Message);
				}

				// A failed email must not prevent the push notification (and vice versa).
				if (app.UserId != null && subsByUser.TryGetValue(app.UserId, out List<BellumGensPushSubscription> subs))
				{
					try
					{
						await _notificationService.SendNotificationAsync(subs, app, callbackUrl).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						System.Diagnostics.Trace.TraceError($"Tournament checkin push notification error for registration {app.Id}: " + e.Message);
					}
				}
			});
			return Ok(new { message = $"{applications.Count} emails sent" });
		}

		[HttpPut]
		[Route("Checkin")]
		public async Task<IActionResult> WeeklyCheckin(Guid id)
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			TournamentApplication entity = await _dbContext.TournamentApplications.FindAsync(id);
			if (entity != null && (entity.UserId == user.Id || await UserIsInRole("admin")))
			{
				if (entity.State != TournamentApplicationState.Banned)
				{
					entity.State = TournamentApplicationState.Confirmed;

					try
					{
						await _dbContext.SaveChangesAsync();
					}
					catch (DbUpdateException e)
					{
						System.Diagnostics.Trace.TraceError("Tournament registration update error: " + e.Message);
						return BadRequest("Something went wrong!");
					}
					return Ok(entity);
				}
			}
			return NotFound();
		}

		[Route("Checkin")]
		[AllowAnonymous]
		public async Task<IActionResult> WeeklyCheckin(Guid id, string hash)
		{
			TournamentApplication entity = await _dbContext.TournamentApplications.FindAsync(id);
			if (entity != null && entity.Hash == hash && entity.State != TournamentApplicationState.Banned)
			{
				entity.State = TournamentApplicationState.Confirmed;

				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament registration update error: " + e.Message);
					return BadRequest("Something went wrong!");
				}
				return Redirect(CORSConfig.returnOrigin + "?message=Checkin successful!");
			}
			return NotFound();
		}

		[HttpPut]
		[Route("Confirm")]
		[Authorize(Roles = "admin")]
		public async Task<IActionResult> ConfirmRegistration(Guid id, TournamentApplication application)
		{
			TournamentApplication entity = await _dbContext.TournamentApplications.FindAsync(id);
			if (entity != null)
			{
				application.Id = entity.Id;
				application.UserId = entity.UserId;
				application.Hash = entity.Hash;
				application.TournamentId = entity.TournamentId;
				application.DateSubmitted = entity.DateSubmitted;
				_dbContext.Entry(entity).CurrentValues.SetValues(application);

				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament registration update error: " + e.Message);
					return BadRequest("Something went wrong!");
				}
				return Ok(application);
			}
			return NotFound();
		}

		[AllowAnonymous]
		[Route("CSGORegs")]
		public async Task<IActionResult> GetCSGORegistrations(Guid? tournamentId = null)
		{
			List<TournamentApplication> entities = tournamentId != null ?
				await _dbContext.TournamentApplications.Include(a => a.Team).Where(r => r.Game == Game.CSGO && r.TournamentId == tournamentId).ToListAsync() :
				await _dbContext.TournamentApplications.Include(a => a.Team).Where(r => r.Game == Game.CSGO && r.Tournament.Active).ToListAsync();

			List<TournamentCSGOMatch> matches = tournamentId != null ?
				await _dbContext.TournamentCSGOMatches.Where(m => m.TournamentId == tournamentId).Include(m => m.Maps).ToListAsync() :
				await _dbContext.TournamentCSGOMatches.Where(m => m.Tournament.Active).Include(m => m.Maps).ToListAsync();

			List<TournamentCSGOParticipant> registrations = new();
			foreach (TournamentApplication app in entities)
			{
				registrations.Add(new TournamentCSGOParticipant(app, matches.FindAll(m => m.Team1Id == app.TeamId || m.Team2Id == app.TeamId)));
			}
			return Ok(registrations);
		}

		[AllowAnonymous]
		[Route("SC2Regs")]
		public async Task<IActionResult> GetSC2sRegistrations(Guid? tournamentId = null)
		{
			List<TournamentApplication> entities = tournamentId != null ?
				await _dbContext.TournamentApplications
								.Include(a => a.User)
									.ThenInclude(u => u.StarCraft2Details)
								.Where(r => r.Game == Game.StarCraft2 && r.TournamentId == tournamentId && r.State != TournamentApplicationState.Banned).ToListAsync() :
				await _dbContext.TournamentApplications
								.Include(a => a.User)
									.ThenInclude(u => u.StarCraft2Details)
								.Where(r => r.Game == Game.StarCraft2 && r.Tournament.Active && r.State != TournamentApplicationState.Banned).ToListAsync();

			List<TournamentSC2Match> matches = tournamentId != null ?
				await _dbContext.TournamentSC2Matches.Where(m => m.TournamentId == tournamentId).ToListAsync() :
				await _dbContext.TournamentSC2Matches.Where(m => m.Tournament.Active).ToListAsync();

			List<TournamentSC2Participant> registrations = new();

			// TournamentSC2Participant dereferences the registration's user; a registration whose user
			// is missing (UserId cleared or the account removed) would otherwise fail the whole list.
			foreach (TournamentApplication app in entities.Where(a => a.User != null))
			{
				registrations.Add(new TournamentSC2Participant(app, matches.FindAll(m => m.Player1Id == app.UserId || m.Player2Id == app.UserId)));
			}
			return Ok(registrations);
		}

		[HttpDelete]
		[Route("Delete")]
		public async Task<IActionResult> DeleteRegistraion(Guid id)
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			TournamentApplication application = await _dbContext.TournamentApplications.FindAsync(id);
			if (application == null)
			{
				return NotFound();
			}
			if (application.UserId == user.Id || await UserIsInRole("admin"))
			{
				_dbContext.TournamentApplications.Remove(application);
				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament registration delete error: " + e.Message);
					return BadRequest("Something went wrong!");
				}
				return Ok(id);
			}
			return NotFound();
		}

		[HttpGet]
		[AllowAnonymous]
		[Route("Tournaments")]
		public async Task<IActionResult> GetTournaments()
		{
			List<Tournament> tournaments = await _dbContext.Tournaments.AsNoTracking()
				.Where(t => t.Visibility != TournamentVisibility.Private)
				.ToListAsync();
			tournaments.ForEach(t => t.InviteCode = null);
			return Ok(tournaments);
		}

		/// <summary>The tournaments the current user created, with their invite codes.</summary>
		[HttpGet]
		[Route("MyTournaments")]
		public async Task<IActionResult> GetMyTournaments()
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			return Ok(await _dbContext.Tournaments.AsNoTracking()
				.Where(t => t.CreatorId == user.Id)
				.OrderByDescending(t => t.StartDate)
				.ToListAsync());
		}

		/// <summary>
		/// Creates a tournament owned by the current user (when the id is empty) or updates an existing one,
		/// which only its owner or an admin can do.
		/// </summary>
		[HttpPut]
		[Route("Create")]
		public async Task<IActionResult> CreateTournament(TournamentViewModel tournament)
		{
			if (!ModelState.IsValid || tournament == null)
			{
				return BadRequest("Invalid tournament");
			}
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			string error = ValidateTournament(tournament);
			if (error != null)
			{
				return BadRequest(error);
			}

			bool isAdmin = await _userManager.IsInRoleAsync(user, "admin");
			Tournament entity;
			if (tournament.ID == Guid.Empty)
			{
				entity = new Tournament()
				{
					CreatorId = user.Id
				};
				ApplyTournamentChanges(entity, tournament, isAdmin, true);
				_dbContext.Tournaments.Add(entity);
			}
			else
			{
				entity = await _dbContext.Tournaments.FindAsync(tournament.ID);
				if (entity == null)
				{
					return NotFound("Tournament not found.");
				}
				if (entity.CreatorId != user.Id && !isAdmin)
				{
					return StatusCode(StatusCodes.Status403Forbidden, "You can only edit tournaments you created.");
				}
				if (tournament.MaxParticipants != null)
				{
					int participants = await CountParticipants(entity.ID);
					if (participants > tournament.MaxParticipants)
					{
						return BadRequest($"The tournament already has {participants} participants, more than the new limit of {tournament.MaxParticipants}.");
					}
				}
				ApplyTournamentChanges(entity, tournament, isAdmin, false);
			}

			if (entity.Visibility != TournamentVisibility.Public)
			{
				await entity.UniqueInviteCode(_dbContext);
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament update exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(entity);
		}

		[HttpPost]
		[Route("Join")]
		public async Task<IActionResult> JoinTournament(TournamentJoinViewModel model)
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			string code = Tournament.NormalizeInviteCode(model?.InviteCode);
			if (string.IsNullOrEmpty(code))
			{
				return BadRequest("Please enter an invite code.");
			}
			Tournament tournament = await _dbContext.Tournaments.FirstOrDefaultAsync(t => t.InviteCode == code);
			if (tournament == null)
			{
				return NotFound("There is no tournament with this invite code.");
			}
			if (!tournament.AcceptsParticipants)
			{
				return BadRequest("This tournament is no longer accepting participants.");
			}
			if (await _dbContext.TournamentApplications.AnyAsync(a => a.TournamentId == tournament.ID && a.UserId == user.Id))
			{
				return BadRequest("You have already joined this tournament.");
			}
			if (tournament.MaxParticipants != null && await CountParticipants(tournament.ID) >= tournament.MaxParticipants)
			{
				return BadRequest("This tournament is full.");
			}

			// The invite code is the organizer's approval, so the participant is confirmed right away
			TournamentApplication application = new()
			{
				TournamentId = tournament.ID,
				UserId = user.Id,
				Game = tournament.Game ?? Game.CSGO,
				Email = user.Email,
				BattleNetId = user.BattleNetId,
				State = TournamentApplicationState.Confirmed
			};
			await application.UniqueHash(_dbContext);
			_dbContext.TournamentApplications.Add(application);

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament join exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}

			_dbContext.Entry(tournament).State = EntityState.Detached;
			if (tournament.CreatorId != user.Id)
			{
				tournament.InviteCode = null;
			}
			return Ok(tournament);
		}

		[HttpDelete]
		[Route("delete-tournament")]
		public async Task<IActionResult> DeleteTournament(Guid id)
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			Tournament tournament = await _dbContext.Tournaments.FindAsync(id);
			if (tournament == null)
			{
				return NotFound("Tournament not found.");
			}
			if (tournament.CreatorId != user.Id && !await _userManager.IsInRoleAsync(user, "admin"))
			{
				return StatusCode(StatusCodes.Status403Forbidden, "You can only delete tournaments you created.");
			}

			// Matches reference the tournament and its groups without cascading deletes, so remove them first;
			// registrations, groups and their participants are removed by the database cascades
			List<Guid?> groupIds = (await _dbContext.TournamentCSGOGroups.Where(g => g.TournamentId == id).Select(g => g.Id).ToListAsync())
				.Concat(await _dbContext.TournamentSC2Groups.Where(g => g.TournamentId == id).Select(g => g.Id).ToListAsync())
				.Select(g => (Guid?)g)
				.ToList();
			_dbContext.TournamentCSGOMatches.RemoveRange(await _dbContext.TournamentCSGOMatches
				.Where(m => m.TournamentId == id || groupIds.Contains(m.GroupId)).ToListAsync());
			_dbContext.TournamentSC2Matches.RemoveRange(await _dbContext.TournamentSC2Matches
				.Where(m => m.TournamentId == id || groupIds.Contains(m.GroupId)).ToListAsync());
			_dbContext.Tournaments.Remove(tournament);

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament delete exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(id);
		}
		#endregion

		#region GROUPS
		[AllowAnonymous]
		[Route("CSGOGroups")]
		public async Task<IActionResult> GetCSGOGroups(Guid? tournamentId = null)
		{
			List<TournamentCSGOGroup> groups = tournamentId != null ?
				await _dbContext.TournamentCSGOGroups.Where(g => g.TournamentId == tournamentId)
								.Include(g => g.Participants)
									.ThenInclude(p => p.TournamentApplication)
										.ThenInclude(p => p.Team)
								.Include(g => g.Matches).ToListAsync() :
				await _dbContext.TournamentCSGOGroups.Where(g => g.Tournament.Active)
								.Include(g => g.Participants)
									.ThenInclude(p => p.TournamentApplication)
										.ThenInclude(p => p.Team)
								.Include(g => g.Matches).ToListAsync();
			return Ok(groups);
		}

		[AllowAnonymous]
		[Route("SC2Groups")]
		public async Task<IActionResult> GetSC2Groups(Guid? tournamentId = null)
		{
			List<TournamentSC2Group> groups = tournamentId != null ?
				await _dbContext.TournamentSC2Groups.Where(g => g.TournamentId == tournamentId)
								.Include(g => g.Participants)
									.ThenInclude(p => p.TournamentApplication)
										.ThenInclude(p => p.User)
											.ThenInclude(p => p.StarCraft2Details)
								.Include(g => g.Matches).ToListAsync() :
				await _dbContext.TournamentSC2Groups.Where(g => g.Tournament.Active)
								.Include(g => g.Participants)
									.ThenInclude(p => p.TournamentApplication)
										.ThenInclude(p => p.User)
											.ThenInclude(p => p.StarCraft2Details)
								.Include(g => g.Matches).ToListAsync();
			return Ok(groups);
		}

		[HttpPut]
		[Route("csgogroup")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> SubmitCSGOGroup(Guid? id, TournamentCSGOGroup group)
		{
			TournamentCSGOGroup entity = await _dbContext.TournamentCSGOGroups.FindAsync(id);
			if (entity != null)
			{
				group.Id = entity.Id;
				_dbContext.Entry(entity).CurrentValues.SetValues(group);
			}
			else
			{
				Tournament active = await _dbContext.Tournaments.FirstOrDefaultAsync(t => t.Active);
				if (active == null)
				{
					return BadRequest("There is no active tournament.");
				}
				group.TournamentId = active.ID;
				_dbContext.TournamentCSGOGroups.Add(group);
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament group update exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(group);
		}

		[HttpPut]
		[Route("sc2group")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> SubmitSC2Group(Guid? id, TournamentSC2Group group)
		{
			TournamentSC2Group entity = await _dbContext.TournamentSC2Groups.FindAsync(id);
			if (entity != null)
			{
				group.Id = entity.Id;
				_dbContext.Entry(entity).CurrentValues.SetValues(group);
			}
			else
			{
				Tournament active = await _dbContext.Tournaments.FirstOrDefaultAsync(t => t.Active);
				if (active == null)
				{
					return BadRequest("There is no active tournament.");
				}
				group.TournamentId = active.ID;
				_dbContext.TournamentSC2Groups.Add(group);
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament group update exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(group);
		}

		[HttpDelete]
		[Route("group")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> DeleteGroup(Guid id)
		{
			TournamentGroup entity = await _dbContext.TournamentCSGOGroups.FindAsync(id);
			if (entity != null)
			{
				_dbContext.TournamentCSGOGroups.Remove(entity as TournamentCSGOGroup);
				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament group delete exception: " + e.Message);
					return BadRequest("Something went wrong...");
				}
				return Ok();
			}
			entity = await _dbContext.TournamentSC2Groups.FindAsync(id);
			if (entity != null)
			{
				_dbContext.TournamentSC2Groups.Remove(entity as TournamentSC2Group);
				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament group delete exception: " + e.Message);
					return BadRequest("Something went wrong...");
				}
				return Ok();
			}
			return NotFound();
		}

		[HttpPut]
		[Route("participanttogroup")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> AddToGroup(Guid id, TournamentApplication participant)
		{
			TournamentGroup entity = await _dbContext.TournamentCSGOGroups.FindAsync(id);
			if (entity == null)
			{
				entity = await _dbContext.TournamentSC2Groups.FindAsync(id);
			}
			if (entity != null)
			{
				TournamentGroupParticipant groupParticipant = new TournamentGroupParticipant()
				{
					TournamentApplicationId = participant.Id,
					TournamentGroupId = entity.Id
				};
				entity.Participants.Add(groupParticipant);

				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament group participant add exception: " + e.Message);
					return BadRequest("Something went wrong...");
				}
				return Ok();
			}
			return NotFound();
		}

		[HttpPut]
		[Route("participantpoints")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> SubmitParticipantPoints(Guid participantId, Guid groupId, TournamentGroupParticipant points)
		{
			TournamentGroupParticipant entity = await _dbContext.TournamentGroupParticipants.FindAsync(groupId, participantId);
			if (entity != null)
			{
				entity.Points = points.Points;

				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament group participant add exception: " + e.Message);
					return BadRequest("Something went wrong...");
				}
				return Ok();
			}
			return NotFound();
		}

		[HttpDelete]
		[Route("participanttogroup")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> RemoveFromGroup(Guid id, Guid groupid)
		{
			TournamentGroupParticipant participant = await _dbContext.TournamentGroupParticipants.FindAsync(groupid, id);
			if (participant != null) {
				_dbContext.TournamentGroupParticipants.Remove(participant);
				try
				{
					await _dbContext.SaveChangesAsync();
				}
				catch (DbUpdateException e)
				{
					System.Diagnostics.Trace.TraceError("Tournament group participant delete exception: " + e.Message);
					return BadRequest("Something went wrong...");
				}
				return Ok();
			}
			return NotFound();
		}
		#endregion

		#region MATCHES
		[AllowAnonymous]
		[Route("csgomatches")]
		public async Task<IActionResult> GetCSGOMatches(Guid? tournamentId = null)
		{
			List<TournamentCSGOMatch> matches;
			matches = tournamentId != null ?
						await _dbContext.TournamentCSGOMatches.Where(m => m.TournamentId == tournamentId).Include(m => m.Team1).Include(m => m.Team2).Include(m => m.Maps).OrderBy(m => m.StartTime).ToListAsync() :
						await _dbContext.TournamentCSGOMatches.Include(m => m.Team1).Include(m => m.Team2).Include(m => m.Maps).OrderBy(m => m.StartTime).ToListAsync();
			return Ok(matches);
		}

		[AllowAnonymous]
		[Route("csgomatch")]
		public async Task<IActionResult> GetCSGOMatch(Guid id)
		{
			TournamentCSGOMatch match = await _dbContext.TournamentCSGOMatches.FindAsync(id);
			if (match != null)
				return Ok(match);
			return NotFound();
		}

		[AllowAnonymous]
		[Route("sc2matches")]
		public async Task<IActionResult> GetSC2Matches(Guid? tournamentId = null)
		{
			List<TournamentSC2Match> matches;
			matches = tournamentId != null ?
						await _dbContext.TournamentSC2Matches.Where(m => m.TournamentId == tournamentId)
							.Include(m => m.Player1)
								.ThenInclude(p1 => p1.StarCraft2Details)
							.Include(m => m.Player2)
								.ThenInclude(p2 => p2.StarCraft2Details)
							.Include(m => m.Maps)
							.OrderBy(m => m.StartTime).ToListAsync() :
						await _dbContext.TournamentSC2Matches
							.Include(m => m.Player1)
								.ThenInclude(p1 => p1.StarCraft2Details)
							.Include(m => m.Player2)
								.ThenInclude(p2 => p2.StarCraft2Details)
							.Include(m => m.Maps)
							.OrderBy(m => m.StartTime).ToListAsync();
			return Ok(matches);
		}

		[AllowAnonymous]
		[Route("sc2match")]
		public async Task<IActionResult> GetSC2Match(Guid id)
		{
			TournamentSC2Match match = await _dbContext.TournamentSC2Matches.FindAsync(id);
			if (match != null)
				return Ok(match);
			return NotFound();
		}

		[HttpPut]
		[Route("csgomatch")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> SubmitCSGOMatch(Guid? id, TournamentCSGOMatch match)
		{
			foreach (CSGOMatchMap map in match.Maps ?? Enumerable.Empty<CSGOMatchMap>())
			{
				CSGOMatchMap mapEntity = await _dbContext.CSGOMatchMaps.FindAsync(map.Id);
				if (mapEntity != null)
				{
					_dbContext.Entry(mapEntity).CurrentValues.SetValues(map);
				}
				else
				{
					_dbContext.CSGOMatchMaps.Add(map);
				}
			}

			TournamentCSGOMatch entity = await _dbContext.TournamentCSGOMatches.FindAsync(id);

			if (entity != null)
			{
				match.Id = entity.Id;
				_dbContext.Entry(entity).CurrentValues.SetValues(match);
				match = entity;
			}
			else
			{
				_dbContext.TournamentCSGOMatches.Add(match);
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament CS:GO match update exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			if (entity == null)
			{
				await _dbContext.Entry(match).Reference(m => m.Team1).LoadAsync();
				await _dbContext.Entry(match).Reference(m => m.Team2).LoadAsync();
			}

			return Ok(match);
		}

		[HttpDelete]
		[Route("csgomatch")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> DeleteCSGOMatch(Guid? id)
		{
			TournamentCSGOMatch entity = await _dbContext.TournamentCSGOMatches.FindAsync(id);
			if (entity == null)
			{
				return NotFound();
			}
			_dbContext.TournamentCSGOMatches.Remove(entity);

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament CS:GO match delete exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(entity);
		}

		[HttpPut]
		[Route("csgomatchmap")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> SubmitCSGOMatchMap(Guid? id, CSGOMatchMap map)
		{
			CSGOMatchMap entity = await _dbContext.CSGOMatchMaps.FindAsync(id);
			if (entity != null)
			{
				map.Id = entity.Id;
				_dbContext.Entry(entity).CurrentValues.SetValues(map);
			}
			else
			{
				_dbContext.CSGOMatchMaps.Add(map);
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament CS:GO match map update exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(map);
		}

		[HttpDelete]
		[Route("csgomatchmap")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> DeleteCSGOMatchMap(Guid? id)
		{
			CSGOMatchMap entity = await _dbContext.CSGOMatchMaps.FindAsync(id);
			if (entity == null)
			{
				return NotFound();
			}
			_dbContext.CSGOMatchMaps.Remove(entity);

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament CS:GO match map delete exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(entity);
		}

		[HttpPut]
		[Route("sc2match")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> SubmitSC2Match(Guid? id, TournamentSC2Match match)
		{
			foreach (SC2MatchMap map in match.Maps ?? Enumerable.Empty<SC2MatchMap>())
			{
				SC2MatchMap mapEntity = await _dbContext.SC2MatchMaps.FindAsync(map.Id);
				if (mapEntity != null)
				{
					_dbContext.Entry(mapEntity).CurrentValues.SetValues(map);
				}
				else
				{
					_dbContext.SC2MatchMaps.Add(map);
				}
			}

			TournamentSC2Match entity = await _dbContext.TournamentSC2Matches.FindAsync(id);
			if (entity != null)
			{
				match.Id = entity.Id;
				_dbContext.Entry(entity).CurrentValues.SetValues(match);
				match = entity;
			}
			else
			{
				_dbContext.TournamentSC2Matches.Add(match);
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament StarCraft II match update exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			if (entity == null)
			{
				await _dbContext.Entry(match).Reference(m => m.Player1).LoadAsync();
				await _dbContext.Entry(match).Reference(m => m.Player2).LoadAsync();
			}
			return Ok(match);
		}



		[HttpDelete]
		[Route("sc2match")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> DeleteSC2Match(Guid? id)
		{
			TournamentSC2Match entity = await _dbContext.TournamentSC2Matches.FindAsync(id);
			if (entity == null)
			{
				return NotFound();
			}
			_dbContext.TournamentSC2Matches.Remove(entity);

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament StarCraft II match delete exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(entity);
		}

		[HttpPut]
		[Route("sc2matchmap")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> SubmitSC2MatchMap(Guid? id, SC2MatchMap map)
		{
			SC2MatchMap entity = await _dbContext.SC2MatchMaps.FindAsync(id);
			if (entity != null)
			{
				map.Id = entity.Id;
				_dbContext.Entry(entity).CurrentValues.SetValues(map);
			}
			else
			{
				_dbContext.SC2MatchMaps.Add(map);
			}

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament CS:GO match map update exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(map);
		}

		[HttpDelete]
		[Route("sc2matchmap")]
		[Authorize(Roles = "admin, event-admin")]
		public async Task<IActionResult> DeleteSC2MatchMap(Guid? id)
		{
			SC2MatchMap entity = await _dbContext.SC2MatchMaps.FindAsync(id);
			if (entity == null)
			{
				return NotFound();
			}
			_dbContext.SC2MatchMaps.Remove(entity);

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Tournament CS:GO match map delete exception: " + e.Message);
				return BadRequest("Something went wrong...");
			}
			return Ok(entity);
		}
		#endregion

		private async Task<bool> UserIsTournamentAdmin()
		{
			return await UserIsInRole("admin") || await UserIsInRole("event-admin");
		}

		/// <summary>Whether the user owns the tournament or is an admin, i.e. can edit or delete it and see its invite code.</summary>
		private async Task<bool> CanManageTournament(Tournament tournament, ApplicationUser user)
		{
			if (user == null)
			{
				return false;
			}
			return (tournament.CreatorId != null && tournament.CreatorId == user.Id) || await _userManager.IsInRoleAsync(user, "admin");
		}

		private async Task<int> CountParticipants(Guid tournamentId)
		{
			return await _dbContext.TournamentApplications.CountAsync(a => a.TournamentId == tournamentId && a.State != TournamentApplicationState.Banned);
		}

		private static string ValidateTournament(TournamentViewModel tournament)
		{
			if (string.IsNullOrWhiteSpace(tournament.Name))
			{
				return "Tournament name is required.";
			}
			if (tournament.MaxParticipants != null && tournament.MaxParticipants < 2)
			{
				return "A tournament needs room for at least 2 participants.";
			}
			if (tournament.StartDate != null && tournament.EndDate != null && tournament.EndDate < tournament.StartDate)
			{
				return "The tournament can't end before it starts.";
			}
			if (tournament.Game != null && !Enum.IsDefined(tournament.Game.Value))
			{
				return "Unknown game.";
			}
			if (tournament.Visibility != null && !Enum.IsDefined(tournament.Visibility.Value))
			{
				return "Unknown tournament visibility.";
			}
			if (tournament.Status != null && !Enum.IsDefined(tournament.Status.Value))
			{
				return "Unknown tournament status.";
			}
			return null;
		}

		private static void ApplyTournamentChanges(Tournament entity, TournamentViewModel model, bool isAdmin, bool isNew)
		{
			entity.Name = model.Name.Trim();
			entity.Description = model.Description;
			entity.StartDate = model.StartDate;
			entity.EndDate = model.EndDate;
			entity.Game = model.Game;
			entity.MaxParticipants = model.MaxParticipants;
			entity.PrizePool = model.PrizePool;
			// The tournament form has no logo field, so a missing logo keeps the current one
			if (model.Logo != null)
			{
				entity.Logo = model.Logo;
			}
			if (model.Visibility != null)
			{
				entity.Visibility = model.Visibility.Value;
			}
			// Clients send Draft by default, so an update never moves a tournament back to Draft
			if (model.Status != null && (isNew || model.Status != TournamentStatus.Draft))
			{
				entity.Status = model.Status.Value;
			}

			// Active marks the current league and only admins change it; the status follows it
			bool wasActive = entity.Active;
			if (isAdmin && model.Active != null)
			{
				entity.Active = model.Active.Value;
			}
			if (entity.Active && (isNew || !wasActive) && (entity.Status == TournamentStatus.Draft || entity.Status == TournamentStatus.Open))
			{
				entity.Status = TournamentStatus.InProgress;
			}
			else if (!entity.Active && wasActive && entity.Status == TournamentStatus.InProgress)
			{
				entity.Status = TournamentStatus.Completed;
			}
		}
	}
}
