using BellumGens.Api.Core;
using BellumGens.Api.Core.Models;
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
    public class TournamentController : BaseController
    {
        private readonly AppConfiguration _appInfo;
        public TournamentController(AppConfiguration appInfo, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, IEmailSender sender, BellumGensDbContext context, ILogger<AccountController> logger)
            : base(userManager, roleManager, signInManager, sender, context, logger)
        {
            _appInfo = appInfo;
        }

        #region TOURNAMENTS AND REGISTRATIONS
        [AllowAnonymous]
        [Route("ActiveTournament")]
        public async Task<IActionResult> GetActiveTournament()
        {
            return Ok(await _dbContext.Tournaments.FirstOrDefaultAsync(t => t.Active));
        }

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register(TournamentApplication application)
        {
            if (ModelState.IsValid)
            {
                Company c = await _dbContext.Companies.FindAsync(application.CompanyId);
                ApplicationUser user = await GetAuthUser();
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
                    if (user.BattleNetId == null)
                    {
                        user.BattleNetId = application.BattleNetId;
                    }
                }
                else
                {
                    if (application.TeamId == Guid.Empty)
                    {
                        return BadRequest("Моля попълнете отбор във формата за регистрация!");
                    }
                    if (await _dbContext.TournamentApplications.Where(a => a.TeamId == application.TeamId && a.TournamentId == application.TournamentId).SingleOrDefaultAsync() != null)
                    {
                        return BadRequest("Вече има направена регистрация за този отбор!");
                    }
                }
                if (c == null)
                {
                    _dbContext.Companies.Add(new Company()
                    {
                        Name = application.CompanyId
                    });
                }
                await application.UniqueHash(_dbContext);
                application.UserId = user.Id;
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

        [Route("Registrations")]
        public async Task<IActionResult> GetUserRegistrations()
        {
            ApplicationUser user = await GetAuthUser();
            return Ok(await _dbContext.TournamentApplications.Where(a => a.UserId == user.Id && a.Tournament.Active).ToListAsync());
        }

        [AllowAnonymous]
        [Route("RegCount")]
        public async Task<IActionResult> GetTotalRegistrationsCount(Guid tournamentId)
        {
            List<TournamentApplication> registrations = await _dbContext.TournamentApplications.Where(a => a.Tournament.ID == tournamentId).ToListAsync();
            List<RegistrationCountViewModel> model = new List<RegistrationCountViewModel>()
            {
                new RegistrationCountViewModel(registrations.Where(r => r.Game == Game.CSGO).Count(), Game.CSGO),
                new RegistrationCountViewModel(registrations.Where(r => r.Game == Game.StarCraft2).Count(), Game.StarCraft2)
            };
            return Ok(model);
        }

        [Route("AllRegistrations")]
        public async Task<IActionResult> GetAllApplications()
        {
            return await UserIsInRole("admin") ? Ok(await _dbContext.TournamentApplications.Include(a => a.Tournament).ToListAsync()) : Unauthorized();
        }

        [HttpPut]
        [Route("Confirm")]
        public async Task<IActionResult> ConfirmRegistration(Guid id, TournamentApplication application)
        {
            if (await UserIsInRole("admin"))
            {
                TournamentApplication entity = await _dbContext.TournamentApplications.FindAsync(id);
                if (entity != null)
                {
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
            return Unauthorized();
        }

        [AllowAnonymous]
        [Route("CSGORegs")]
        public async Task<IActionResult> GetCSGORegistrations(Guid? tournamentId = null)
        {
            List<TournamentApplication> entities = tournamentId != null ?
                await _dbContext.TournamentApplications.Include(a => a.Team).Where(r => r.Game == Game.CSGO && r.TournamentId == tournamentId).ToListAsync() :
                await _dbContext.TournamentApplications.Include(a => a.Team).Where(r => r.Game == Game.CSGO && r.Tournament.Active).ToListAsync();

            List<TournamentCSGOMatch> matches = tournamentId != null ?
                await _dbContext.TournamentCSGOMatches.Where(m => m.TournamentId == tournamentId).ToListAsync() :
                await _dbContext.TournamentCSGOMatches.Where(m => m.Tournament.Active).ToListAsync();

            List<TournamentCSGOParticipant> registrations = new List<TournamentCSGOParticipant>();
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
                await _dbContext.TournamentApplications.Include(a => a.User).Where(r => r.Game == Game.StarCraft2 && r.TournamentId == tournamentId).ToListAsync() :
                await _dbContext.TournamentApplications.Include(a => a.User).Where(r => r.Game == Game.StarCraft2 && r.Tournament.Active).ToListAsync();

            List<TournamentSC2Match> matches = tournamentId != null ?
                await _dbContext.TournamentSC2Matches.Where(m => m.TournamentId == tournamentId).ToListAsync() :
                await _dbContext.TournamentSC2Matches.Where(m => m.Tournament.Active).ToListAsync();

            List<TournamentSC2Participant> registrations = new List<TournamentSC2Participant>();
            
            foreach (TournamentApplication app in entities)
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
            TournamentApplication application = await _dbContext.TournamentApplications.FindAsync(id);
            if (application?.UserId == user.Id || await UserIsInRole("admin"))
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
            return Ok(await _dbContext.Tournaments.ToListAsync());
        }


        [HttpPut]
        [Route("Create")]
        public async Task<IActionResult> CreateTournament(Tournament tournament)
        {
            if (await UserIsInRole("admin"))
            {
                if (ModelState.IsValid)
                {
                    var entity = await _dbContext.Tournaments.FindAsync(tournament.ID);
                    if (entity != null)
                    {
                        _dbContext.Entry(entity).CurrentValues.SetValues(tournament);
                    }
                    else
                    {
                        _dbContext.Tournaments.Add(tournament);
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
                    return Ok(tournament);
                }
                return BadRequest("Invalid tournament");
            }
            return Unauthorized();
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
                                    .ThenInclude(p => p.Team)
                                .Include(g => g.Matches).ToListAsync() :
                await _dbContext.TournamentCSGOGroups.Where(g => g.Tournament.Active)
                                .Include(g => g.Participants)
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
                                    .ThenInclude(p => p.User)
                                .Include(g => g.Matches).ToListAsync() :
                await _dbContext.TournamentSC2Groups.Where(g => g.Tournament.Active)
                                .Include(g => g.Participants)
                                    .ThenInclude(p => p.User)
                                .Include(g => g.Matches).ToListAsync();
            return Ok(groups);
        }

        [HttpPut]
        [Route("csgogroup")]
        public async Task<IActionResult> SubmitCSGOGroup(Guid? id, TournamentCSGOGroup group)
        {
            if (await UserIsInRole("event-admin"))
            {
                TournamentCSGOGroup entity = await _dbContext.TournamentCSGOGroups.FindAsync(id);
                if (entity != null)
                {
                    _dbContext.Entry(entity).CurrentValues.SetValues(group);
                }
                else
                {
                    group.TournamentId = (await _dbContext.Tournaments.FirstOrDefaultAsync(t => t.Active)).ID;
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
            return Unauthorized();
        }

        [HttpPut]
        [Route("sc2group")]
        public async Task<IActionResult> SubmitSC2Group(Guid? id, TournamentSC2Group group)
        {
            if (await UserIsInRole("event-admin"))
            {
                TournamentSC2Group entity = await _dbContext.TournamentSC2Groups.FindAsync(id);
                if (entity != null)
                {
                    _dbContext.Entry(entity).CurrentValues.SetValues(group);
                }
                else
                {
                    group.TournamentId = (await _dbContext.Tournaments.FirstOrDefaultAsync(t => t.Active)).ID;
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
            return Unauthorized();
        }

        [HttpDelete]
        [Route("group")]
        public async Task<IActionResult> DeleteGroup(Guid id)
        {
            TournamentGroup entity;
            if (await UserIsInRole("event-admin"))
            {
                entity = await _dbContext.TournamentCSGOGroups.FindAsync(id);
                if (entity != null)
                {
                    foreach (var par in entity.Participants)
                    {
                        par.TournamentCSGOGroupId = null;
                    }
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
                    foreach (var par in entity.Participants)
                    {
                        par.TournamentSC2GroupId = null;
                    }
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
            return Unauthorized();
        }

        [HttpPut]
        [Route("participanttogroup")]
        public async Task<IActionResult> AddToGroup(Guid id, TournamentApplication participant)
        {
            TournamentGroup entity;
            if (await UserIsInRole("event-admin"))
            {
                entity = await _dbContext.TournamentCSGOGroups.FindAsync(id);
                if (entity == null)
                {
                    entity = await _dbContext.TournamentSC2Groups.FindAsync(id);
                    if (entity != null)
                    {
                        TournamentApplication app = await _dbContext.TournamentApplications.FindAsync(participant.Id);
                        if (app == null)
                        {
                            return NotFound();
                        }

                        app.TournamentSC2GroupId = id;
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
                else
                {
                    TournamentApplication app = await _dbContext.TournamentApplications.FindAsync(participant.Id);
                    if (app == null)
                    {
                        return NotFound(); 
                    }

                    app.TournamentCSGOGroupId = id;
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
            }
            return Unauthorized();
        }

        [HttpDelete]
        [Route("participanttogroup")]
        public async Task<IActionResult> RemoveFromGroup(Guid id)
        {
            if (await UserIsInRole("event-admin"))
            {
                TournamentApplication entity = await _dbContext.TournamentApplications.FindAsync(id);
                if (entity != null)
                {
                    entity.TournamentCSGOGroupId = null;
                    entity.TournamentSC2GroupId = null;
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
            return Unauthorized();
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
                        await _dbContext.TournamentSC2Matches.Where(m => m.TournamentId == tournamentId).Include(m => m.Player1).Include(m => m.Player2).Include(m => m.Maps).OrderBy(m => m.StartTime).ToListAsync() :
                        await _dbContext.TournamentSC2Matches.Include(m => m.Player1).Include(m => m.Player2).Include(m => m.Maps).OrderBy(m => m.StartTime).ToListAsync();
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
        public async Task<IActionResult> SubmitCSGOMatch(Guid? id, TournamentCSGOMatch match)
        {
            if (await UserIsInRole("event-admin"))
            {
                foreach (CSGOMatchMap map in match.Maps)
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
                    _dbContext.Entry(entity).CurrentValues.SetValues(match);
                    match = entity;
                }
                else
                {
                    if (match.TournamentId == null)
                    {
                        match.TournamentId = (await _dbContext.Tournaments.FirstOrDefaultAsync(t => t.Active)).ID;
                    }
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
            return Unauthorized();
        }

        [HttpDelete]
        [Route("csgomatch")]
        public async Task<IActionResult> DeleteCSGOMatch(Guid? id)
        {
            if (await UserIsInRole("event-admin"))
            {
                TournamentCSGOMatch entity = await _dbContext.TournamentCSGOMatches.FindAsync(id);
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
            return Unauthorized();
        }

        [HttpPut]
        [Route("csgomatchmap")]
        public async Task<IActionResult> SubmitCSGOMatchMap(Guid? id, CSGOMatchMap map)
        {
            if (await UserIsInRole("event-admin"))
            {
                CSGOMatchMap entity = await _dbContext.CSGOMatchMaps.FindAsync(id);
                if (entity != null)
                {
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
            return Unauthorized();
        }

        [HttpDelete]
        [Route("csgomatchmap")]
        public async Task<IActionResult> DeleteCSGOMatchMap(Guid? id)
        {
            if (await UserIsInRole("event-admin"))
            {
                CSGOMatchMap entity = await _dbContext.CSGOMatchMaps.FindAsync(id);
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
            return Unauthorized();
        }

        [HttpPut]
        [Route("sc2match")]
        public async Task<IActionResult> SubmitSC2Match(Guid? id, TournamentSC2Match match)
        {
            if (await UserIsInRole("event-admin"))
            {
                foreach (SC2MatchMap map in match.Maps)
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
                    _dbContext.Entry(entity).CurrentValues.SetValues(match);
                    match = entity;
                }
                else
                {
                    if (match.TournamentId == null)
                    {
                        match.TournamentId = _dbContext.Tournaments.Where(t => t.Active).First().ID;
                    }
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
            return Unauthorized();
        }



        [HttpDelete]
        [Route("sc2match")]
        public async Task<IActionResult> DeleteSC2Match(Guid? id)
        {
            if (await UserIsInRole("event-admin"))
            {
                TournamentSC2Match entity = await _dbContext.TournamentSC2Matches.FindAsync(id);
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
            return Unauthorized();
        }

        [HttpPut]
        [Route("sc2matchmap")]
        public async Task<IActionResult> SubmitSC2MatchMap(Guid? id, SC2MatchMap map)
        {
            if (await UserIsInRole("event-admin"))
            {
                SC2MatchMap entity = await _dbContext.SC2MatchMaps.FindAsync(id);
                if (entity != null)
                {
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
            return Unauthorized();
        }

        [HttpDelete]
        [Route("sc2matchmap")]
        public async Task<IActionResult> DeleteSC2MatchMap(Guid? id)
        {
            if (await UserIsInRole("event-admin"))
            {
                SC2MatchMap entity = await _dbContext.SC2MatchMaps.FindAsync(id);
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
            return Unauthorized();
        }
        #endregion
    }
}
