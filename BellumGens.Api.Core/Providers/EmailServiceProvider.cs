using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
	public class EmailServiceProvider : IEmailSender
	{
        private readonly AppConfiguration _appInfo;
        public EmailServiceProvider(AppConfiguration appInfo)
        {
            _appInfo = appInfo;
        }

        public Task SendEmailAsync(string destination, string subject, string body)
        {
            MailMessage msg = new();
            msg.To.Add(new MailAddress(destination));
            msg.Bcc.Add(new MailAddress("info@eb-league.com"));
            msg.From = new MailAddress(_appInfo.Config.Email, "Bellum Gens");
            msg.Subject = subject;
            msg.Body = body;
            msg.IsBodyHtml = true;

            SmtpClient client = new()
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_appInfo.Config.EmailUsername, _appInfo.Config.EmailPassword),
                Port = 587,
                Host = "smtp.office365.com",
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = true
            };
            return client.SendMailAsync(msg);
        }
    }
}