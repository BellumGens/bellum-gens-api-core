using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Models.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class BaseController : ControllerBase
    {
		protected readonly IEmailSender _sender;
		protected readonly BellumGensDbContext _dbContext;
		protected readonly UserManager<ApplicationUser> _userManager;
		protected readonly RoleManager<IdentityRole> _roleManager;
		protected readonly SignInManager<ApplicationUser> _signInManager;
		protected readonly ILogger<BaseController> _logger;

		public BaseController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, IEmailSender sender, BellumGensDbContext context, ILogger<BaseController> logger)
        {
			_userManager = userManager;
			_roleManager = roleManager;
			_signInManager = signInManager;
			_sender = sender;
			_dbContext = context;
			_logger = logger;
        }

		protected async Task<ApplicationUser> GetAuthUser()
		{
			return User.Identity.IsAuthenticated ? await _userManager.FindByIdAsync(User.GetResolvedUserId()) : null;
		}

		protected async Task<bool> UserIsInRole(string role)
		{
			return await _userManager.IsInRoleAsync(await GetAuthUser(), role);
		}
	}
}
