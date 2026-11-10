using System.Net;
using System.Text.Json;
using Azure.Core;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BellumGens.Api.Core.Tests
{
	public class EmailServiceProviderTests
	{
		private sealed class StubHandler : HttpMessageHandler
		{
			private readonly Queue<Func<HttpResponseMessage>> _responses;
			public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

			public StubHandler(params Func<HttpResponseMessage>[] responses)
			{
				_responses = new Queue<Func<HttpResponseMessage>>(responses);
			}

			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				string body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
				Requests.Add((request, body));
				return _responses.Dequeue()();
			}
		}

		private static EmailOptions DefaultOptions() => new()
		{
			Sender = "info@bellumgens.com",
			DomainSenders = new Dictionary<string, string> { { "eb-league.com", "info@eb-league.com" } }
		};

		private static EmailServiceProvider CreateProvider(StubHandler handler, EmailOptions? options = null, bool configured = true,
			HttpContext? httpContext = null)
		{
			var credential = new Mock<TokenCredential>();
			credential.Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new AccessToken("test-token", DateTimeOffset.UtcNow.AddHours(1)));
			var http = new HttpClient(handler) { BaseAddress = new Uri(EmailServiceProvider.GraphBaseUrl) };
			return new EmailServiceProvider(http,
				Options.Create(options ?? DefaultOptions()),
				new GraphMailCredential(configured ? credential.Object : null),
				new HttpContextAccessor { HttpContext = httpContext },
				NullLogger<EmailServiceProvider>.Instance);
		}

		private static HttpContext RequestWith(string? origin = null, string? referer = null)
		{
			var context = new DefaultHttpContext();
			if (origin != null)
			{
				context.Request.Headers.Origin = origin;
			}
			if (referer != null)
			{
				context.Request.Headers.Referer = referer;
			}
			return context;
		}

		private static string ResolvedSender(HttpContext? httpContext) =>
			CreateProvider(new StubHandler(), httpContext: httpContext).ResolveSender();

		private static HttpResponseMessage Throttled()
		{
			var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
			response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
			return response;
		}

		[Fact]
		public async Task SendEmailAsync_PostsHtmlMessageAsSenderWithBearerToken()
		{
			var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.Accepted));

			await CreateProvider(handler).SendEmailAsync("player@example.com", "Hello", "<p>Hi</p>");

			var (request, body) = Assert.Single(handler.Requests);
			Assert.Equal(HttpMethod.Post, request.Method);
			Assert.Equal("https://graph.microsoft.com/v1.0/users/info%40bellumgens.com/sendMail", request.RequestUri!.AbsoluteUri);
			Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
			Assert.Equal("test-token", request.Headers.Authorization.Parameter);

			using var json = JsonDocument.Parse(body);
			var message = json.RootElement.GetProperty("message");
			Assert.Equal("Hello", message.GetProperty("subject").GetString());
			Assert.Equal("HTML", message.GetProperty("body").GetProperty("contentType").GetString());
			Assert.Equal("<p>Hi</p>", message.GetProperty("body").GetProperty("content").GetString());
			Assert.Equal("player@example.com", message.GetProperty("toRecipients")[0].GetProperty("emailAddress").GetProperty("address").GetString());
			Assert.False(message.TryGetProperty("bccRecipients", out _));
			Assert.True(json.RootElement.GetProperty("saveToSentItems").GetBoolean());
		}

		[Fact]
		public async Task SendEmailAsync_SendsAsEBLeagueMailboxForEBLeagueRequests()
		{
			var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.Accepted));

			await CreateProvider(handler, httpContext: RequestWith(origin: "https://eb-league.com"))
				.SendEmailAsync("player@example.com", "Hello", "Hi");

			Assert.Equal("https://graph.microsoft.com/v1.0/users/info%40eb-league.com/sendMail", handler.Requests[0].Request.RequestUri!.AbsoluteUri);
		}

		[Theory]
		[InlineData("https://eb-league.com")]
		[InlineData("https://www.eb-league.com")]
		[InlineData("http://staging.eb-league.com")]
		[InlineData("https://WWW.EB-LEAGUE.COM")]
		public void ResolveSender_MapsEBLeagueOriginAndSubdomains(string origin)
		{
			Assert.Equal("info@eb-league.com", ResolvedSender(RequestWith(origin: origin)));
		}

		[Theory]
		[InlineData("https://bellumgens.com")]
		[InlineData("https://www.bellumgens.com")]
		[InlineData("https://not-eb-league.com")]
		[InlineData("https://eb-league.com.evil.example")]
		[InlineData("null")]
		public void ResolveSender_UsesDefaultSenderForOtherOrigins(string origin)
		{
			Assert.Equal("info@bellumgens.com", ResolvedSender(RequestWith(origin: origin)));
		}

		[Fact]
		public void ResolveSender_FallsBackToRefererWithoutOrigin()
		{
			Assert.Equal("info@eb-league.com", ResolvedSender(RequestWith(referer: "https://eb-league.com/register")));
		}

		[Fact]
		public void ResolveSender_PrefersOriginOverReferer()
		{
			Assert.Equal("info@bellumgens.com", ResolvedSender(RequestWith(origin: "https://bellumgens.com", referer: "https://eb-league.com/")));
		}

		[Fact]
		public void ResolveSender_UsesDefaultSenderOutsideARequest()
		{
			Assert.Equal("info@bellumgens.com", ResolvedSender(null));
			Assert.Equal("info@bellumgens.com", ResolvedSender(RequestWith()));
		}

		[Fact]
		public async Task SendEmailAsync_RetriesWhenThrottled()
		{
			var handler = new StubHandler(Throttled, Throttled, () => new HttpResponseMessage(HttpStatusCode.Accepted));

			await CreateProvider(handler).SendEmailAsync("player@example.com", "Hello", "Hi");

			Assert.Equal(3, handler.Requests.Count);
		}

		[Fact]
		public async Task SendEmailAsync_GivesUpAfterMaxAttempts()
		{
			var handler = new StubHandler(Enumerable.Repeat<Func<HttpResponseMessage>>(Throttled, EmailServiceProvider.MaxAttempts).ToArray());

			var ex = await Assert.ThrowsAsync<HttpRequestException>(() => CreateProvider(handler).SendEmailAsync("player@example.com", "Hello", "Hi"));

			Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
			Assert.Equal(EmailServiceProvider.MaxAttempts, handler.Requests.Count);
		}

		[Fact]
		public async Task SendEmailAsync_ThrowsWithGraphErrorWithoutRetryingOnForbidden()
		{
			var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.Forbidden)
			{
				Content = new StringContent("{\"error\":{\"code\":\"ErrorAccessDenied\"}}")
			});

			var ex = await Assert.ThrowsAsync<HttpRequestException>(() => CreateProvider(handler).SendEmailAsync("player@example.com", "Hello", "Hi"));

			Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
			Assert.Contains("ErrorAccessDenied", ex.Message);
			Assert.Single(handler.Requests);
		}

		[Fact]
		public async Task SendEmailAsync_ThrowsWhenNotConfigured()
		{
			var handler = new StubHandler();

			await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProvider(handler, configured: false).SendEmailAsync("player@example.com", "Hello", "Hi"));

			Assert.Empty(handler.Requests);
		}

		[Fact]
		public void FromOptions_ReturnsNoCredentialWithoutAppRegistration()
		{
			Assert.Null(GraphMailCredential.FromOptions(new EmailOptions { Sender = "info@bellumgens.com" }).Credential);
			Assert.Null(GraphMailCredential.FromOptions(new EmailOptions { TenantId = "t" }).Credential);
		}

		[Theory]
		[InlineData(null)]
		[InlineData("00000000-0000-0000-0000-000000000003")]
		public void FromOptions_UsesFederatedManagedIdentityWithoutSecretOrCertificate(string? managedIdentityClientId)
		{
			var credential = GraphMailCredential.FromOptions(new EmailOptions
			{
				TenantId = "00000000-0000-0000-0000-000000000001",
				ClientId = "00000000-0000-0000-0000-000000000002",
				ManagedIdentityClientId = managedIdentityClientId
			}).Credential;

			Assert.IsType<Azure.Identity.ClientAssertionCredential>(credential);
		}

		[Fact]
		public void FromOptions_UsesClientSecretWhenPresent()
		{
			var credential = GraphMailCredential.FromOptions(new EmailOptions
			{
				TenantId = "00000000-0000-0000-0000-000000000001",
				ClientId = "00000000-0000-0000-0000-000000000002",
				ClientSecret = "secret"
			}).Credential;

			Assert.IsType<Azure.Identity.ClientSecretCredential>(credential);
		}
	}
}
