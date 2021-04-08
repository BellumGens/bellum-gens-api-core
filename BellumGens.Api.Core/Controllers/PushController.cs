using BellumGens.Api.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
	public class PushController : BaseController
    {
		public PushController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, IEmailSender sender, BellumGensDbContext context, ILogger<PushController> logger)
			: base(userManager, roleManager, signInManager, sender, context, logger)
		{
		}

		[HttpPost]
		[Route("Subscribe")]
		public async Task<IActionResult> Subscribe(BellumGensPushSubscriptionViewModel sub)
        {
            BellumGensPushSubscription push = new()
            {
				Endpoint = sub.Endpoint,
				ExpirationTime = sub.ExpirationTime,
				UserId = (await GetAuthUser())?.Id,
				P256dh = sub.Keys.P256dh,
				Auth = sub.Keys.Auth
			};
			_dbContext.BellumGensPushSubscriptions.Add(push);

			try
			{
				await _dbContext.SaveChangesAsync();
			}
			catch (DbUpdateException e)
			{
				System.Diagnostics.Trace.TraceError("Push notification sub error: " + e.Message);
				return Ok("Sub already exists...");
			}
			return Ok(push);
		}
    }
}
