using BellumGens.Api.Core;
using Microsoft.AspNetCore.Mvc;

namespace BellumGens.Api.Controllers
{
	[Route("[controller]")]
	public class HomeController : ControllerBase
	{
		public IActionResult Index()
		{
			return Redirect(CORSConfig.returnOrigin);
		}
	}
}
