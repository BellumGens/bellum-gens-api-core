using BellumGens.Api.Core;
using Microsoft.AspNetCore.Mvc;

namespace BellumGens.Api.Controllers
{
	public class HomeController : ControllerBase
	{
		public IActionResult Index()
		{
			return Redirect(CORSConfig.returnOrigin);
		}
	}
}
