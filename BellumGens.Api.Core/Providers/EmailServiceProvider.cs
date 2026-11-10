using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BellumGens.Api.Core.Providers
{
	/// <summary>
	/// Sends mail through the Microsoft Graph sendMail endpoint as the configured sender mailbox, authenticating as an
	/// Entra ID application. Registered as a typed HttpClient whose BaseAddress is <see cref="GraphBaseUrl"/>.
	/// </summary>
	public class EmailServiceProvider : IEmailService
	{
		public const string GraphBaseUrl = "https://graph.microsoft.com/v1.0/";
		private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];
		internal const int MaxAttempts = 4;
		private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

		private readonly HttpClient _http;
		private readonly EmailOptions _options;
		private readonly TokenCredential _credential;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ILogger<EmailServiceProvider> _logger;

		public EmailServiceProvider(HttpClient http, IOptions<EmailOptions> options, GraphMailCredential credential, IHttpContextAccessor httpContextAccessor, ILogger<EmailServiceProvider> logger)
		{
			_http = http;
			_options = options.Value;
			_credential = credential.Credential;
			_httpContextAccessor = httpContextAccessor;
			_logger = logger;
		}

		public async Task SendEmailAsync(string destination, string subject, string body)
		{
			if (_credential == null || string.IsNullOrWhiteSpace(_options.Sender))
			{
				throw new InvalidOperationException("Email is not configured. Set email:sender, email:tenantId, email:clientId and either email:clientSecret or email:certificateThumbprint.");
			}

			var payload = new
			{
				message = new
				{
					subject,
					body = new { contentType = "HTML", content = body },
					toRecipients = new[] { Recipient(destination) }
				},
				saveToSentItems = _options.SaveToSentItems
			};
			string path = $"users/{Uri.EscapeDataString(ResolveSender())}/sendMail";

			for (int attempt = 1; ; attempt++)
			{
				AccessToken token = await _credential.GetTokenAsync(new TokenRequestContext(GraphScopes), CancellationToken.None).ConfigureAwait(false);
				using HttpRequestMessage request = new(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

				using HttpResponseMessage response = await _http.SendAsync(request).ConfigureAwait(false);
				if (response.IsSuccessStatusCode)
				{
					return;
				}

				// 429 (throttled) and 503 mean Graph did not accept the message, so a retry cannot send it twice.
				if (attempt < MaxAttempts && response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
				{
					TimeSpan delay = RetryDelay(response, attempt);
					_logger.LogWarning("Graph sendMail returned {StatusCode}; retrying in {Delay} (attempt {Attempt} of {MaxAttempts}).",
						(int)response.StatusCode, delay, attempt, MaxAttempts);
					await Task.Delay(delay).ConfigureAwait(false);
					continue;
				}

				string error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				throw new HttpRequestException($"Graph sendMail failed with {(int)response.StatusCode} {response.StatusCode}: {error}", null, response.StatusCode);
			}
		}

		/// <summary>Picks the mailbox for the site the current request came from, or the default sender outside a request.</summary>
		internal string ResolveSender()
		{
			HttpRequest request = _httpContextAccessor.HttpContext?.Request;
			if (request != null && _options.DomainSenders.Count > 0)
			{
				string source = request.Headers.Origin.ToString();
				if (string.IsNullOrEmpty(source))
				{
					source = request.Headers.Referer.ToString();
				}
				// A sandboxed or privacy-redirected request sends "Origin: null", which is not an absolute URI.
				if (Uri.TryCreate(source, UriKind.Absolute, out Uri uri))
				{
					foreach ((string domain, string mailbox) in _options.DomainSenders)
					{
						if (uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase)
							|| uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
						{
							return mailbox;
						}
					}
				}
			}
			return _options.Sender;
		}

		private static object Recipient(string address) => new { emailAddress = new { address } };

		private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
		{
			TimeSpan delay = response.Headers.RetryAfter switch
			{
				{ Delta: TimeSpan delta } => delta,
				{ Date: DateTimeOffset date } => date - DateTimeOffset.UtcNow,
				_ => TimeSpan.FromSeconds(Math.Pow(2, attempt))
			};
			return delay < TimeSpan.Zero ? TimeSpan.Zero : delay > MaxRetryDelay ? MaxRetryDelay : delay;
		}
	}
}
