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
            BellumGensPushSubscription push = new BellumGensPushSubscription()
            {
				endpoint = sub.endpoint,
				expirationTime = sub.expirationTime,
				userId = (await GetAuthUser())?.Id,
				p256dh = sub.keys.p256dh,
				auth = sub.keys.auth
			};
			_dbContext.BellumGensPushSubscriptions.Add(push);

			try
			{
				_dbContext.SaveChanges();
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
