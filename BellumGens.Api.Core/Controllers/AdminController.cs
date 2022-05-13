using BellumGens.Api.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
    [Authorize]
    public class AdminController : BaseController
    {
        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, IEmailSender sender, BellumGensDbContext context, ILogger<AdminController> logger)
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
        public async Task<IActionResult> CreateRole(string rolename)
        {
            if (await UserIsInRole("admin"))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole() { Name = rolename });
                if (result.Succeeded)
                {
                    return Ok();
                }
            }
            return Unauthorized();
        }

        [Route("Roles")]
        public async Task<IActionResult> GetRoles()
        {
            if (await UserIsInRole("admin"))
            {
                return Ok(_roleManager.Roles.Select(r => r.Name).ToList());
            }
            return Unauthorized();
        }

        [Route("Users")]
        public async Task<IActionResult> GetUsers()
        {
            if (await UserIsInRole("admin"))
            {
                var users = _dbContext.Users.Select(s => new { s.Id, s.UserName }).ToList();
                return Ok(users);
            }
            return Unauthorized();
        }

        [Route("Promos")]
        public async Task<IActionResult> GetPromoCodes()
        {
            if (await UserIsInRole("admin"))
            {
                var promos = _dbContext.PromoCodes.ToList();
                return Ok(promos);
            }
            return Unauthorized();
        }

        [HttpPut]
        [Route("AddUserRole")]
        public async Task<IActionResult> AddUserToRole(string userid, string role)
        {
            if (await UserIsInRole("admin"))
            {
                ApplicationUser user = await _userManager.FindByIdAsync(userid);
                IdentityResult result = await _userManager.AddToRoleAsync(user, role);
                if (result.Succeeded)
                {
                    return Ok("Ok");
                }
            }
            return Unauthorized();
        }
    }
}
