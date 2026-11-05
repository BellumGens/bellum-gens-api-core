using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
	public interface IEmailService
	{
		Task SendEmailAsync(string destination, string subject, string body);
	}
}
