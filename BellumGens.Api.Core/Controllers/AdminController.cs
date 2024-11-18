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
    public class AdminController : BaseController
    {
        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, EmailServiceProvider sender, BellumGensDbContext context, ILogger<AdminController> logger)
            : base(userManager, roleManager, signInManager, sender, context, logger)
        {
        }

        [HttpGet]
        public async Task<bool> Get()
        {
            return await UserIsInRole("admin");
        }

        [Route("TournamentAdmin")]
        public async Task<bool> GetUserIsTournamentAdmin()
        {
            return await UserIsInRole("event-admin");
        }

        [HttpPut]
        [Route("CreateRole")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateRole(string rolename)
        {
            var result = await _roleManager.CreateAsync(new IdentityRole() { Name = rolename });
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest();
        }

        [Route("Roles")]
        [Authorize(Roles = "admin")]
        public async Task<List<string>> GetRoles()
        {
            return await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        }

        [Route("Users")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _dbContext.Users.Select(s => new { s.Id, s.UserName }).ToListAsync();
            return Ok(users);
        }

        [Route("Promos")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetPromoCodes()
        {
            var promos = await _dbContext.PromoCodes.ToListAsync();
            return Ok(promos);
        }

        [HttpPut]
        [Route("AddUserRole")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AddUserToRole(string userid, string role)
        {
            ApplicationUser user = await _userManager.FindByIdAsync(userid);
            IdentityResult result = await _userManager.AddToRoleAsync(user, role);
            if (result.Succeeded)
            {
                return Ok("Ok");
            }
            return BadRequest();
        }
    }
}
