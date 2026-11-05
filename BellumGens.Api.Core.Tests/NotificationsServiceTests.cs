using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using WebPush;
using Xunit;

namespace BellumGens.Api.Core.Tests
{
	public class NotificationsServiceTests
	{
		private readonly Mock<IWebPushClient> _webPushClient;
		private readonly Mock<ILogger<NotificationsService>> _logger;
		private readonly NotificationsService _service;
		private readonly List<string> _attemptedEndpoints = new();

		public NotificationsServiceTests()
		{
			_webPushClient = new Mock<IWebPushClient>();
			_webPushClient
				.Setup(c => c.SendNotificationAsync(It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
				.Callback<PushSubscription, string, VapidDetails, CancellationToken>((s, _, _, _) => _attemptedEndpoints.Add(s.Endpoint))
				.Returns(Task.CompletedTask);
			_logger = new Mock<ILogger<NotificationsService>>();
			_service = new NotificationsService(_webPushClient.Object, TestUtils.CreateAppConfiguration(), _logger.Object);
		}

		public static TheoryData<string> Overloads => new()
		{
			"TeamInvite", "TeamInviteAccepted", "TeamApplication", "TeamApplicationAccepted", "StrategyComment", "TournamentApplication"
		};

		private static ApplicationUser CreateUser() => new()
		{
			Id = "user-1",
			UserName = "player1",
			CSGODetails = new CSGODetails { AvatarFull = "https://avatars.example.com/full.jpg", AvatarIcon = "https://avatars.example.com/icon.jpg" }
		};

		private static CSGOTeam CreateTeam() => new() { TeamId = Guid.NewGuid(), TeamName = "Bellum Gens", TeamAvatar = "https://avatars.example.com/team.jpg" };

		private Task Send(string overload, List<BellumGensPushSubscription> subs)
		{
			var user = CreateUser();
			var team = CreateTeam();
			var invite = new TeamInvite { TeamId = team.TeamId, Team = team, InvitedUser = user, InvitedUserId = user.Id };
			var application = new TeamApplication { TeamId = team.TeamId, Team = team, User = user, ApplicantId = user.Id };
			return overload switch
			{
				"TeamInvite" => _service.SendNotificationAsync(subs, invite),
				"TeamInviteAccepted" => _service.SendNotificationAsync(subs, invite, NotificationState.Accepted),
				"TeamApplication" => _service.SendNotificationAsync(subs, application),
				"TeamApplicationAccepted" => _service.SendNotificationAsync(subs, application, NotificationState.Accepted),
				"StrategyComment" => _service.SendNotificationAsync(subs, new StrategyComment { StratId = Guid.NewGuid(), User = user }),
				"TournamentApplication" => _service.SendNotificationAsync(subs, new TournamentApplication(), "https://bellumgens.com/checkin"),
				_ => throw new ArgumentOutOfRangeException(nameof(overload))
			};
		}

		private static List<BellumGensPushSubscription> Subscriptions(params string[] endpoints)
		{
			var subs = new List<BellumGensPushSubscription>();
			foreach (var endpoint in endpoints)
			{
				subs.Add(new BellumGensPushSubscription { UserId = "user-1", Endpoint = endpoint, P256dh = "p256dh-" + endpoint, Auth = "auth-" + endpoint });
			}
			return subs;
		}

		private void FailFor(string endpoint, Exception exception)
		{
			_webPushClient
				.Setup(c => c.SendNotificationAsync(It.Is<PushSubscription>(s => s.Endpoint == endpoint), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
				.Callback<PushSubscription, string, VapidDetails, CancellationToken>((s, _, _, _) => _attemptedEndpoints.Add(s.Endpoint))
				.ThrowsAsync(exception);
		}

		private void VerifyLogged(LogLevel level, Times times)
		{
			_logger.Verify(l => l.Log(
				level,
				It.IsAny<EventId>(),
				It.IsAny<It.IsAnyType>(),
				It.IsAny<Exception?>(),
				(Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), times);
		}

		private static WebPushException PushServiceError(HttpStatusCode status) =>
			new("Received unexpected response code", new PushSubscription(), new HttpResponseMessage(status));

		[Theory]
		[MemberData(nameof(Overloads))]
		public async Task SendNotificationAsync_SendsToEverySubscriptionWithVapidDetails(string overload)
		{
			await Send(overload, Subscriptions("https://push.example.com/a", "https://push.example.com/b"));

			Assert.Equal(new[] { "https://push.example.com/a", "https://push.example.com/b" }, _attemptedEndpoints);
			_webPushClient.Verify(c => c.SendNotificationAsync(
				It.Is<PushSubscription>(s => s.Endpoint == "https://push.example.com/a" && s.P256DH == "p256dh-https://push.example.com/a" && s.Auth == "auth-https://push.example.com/a"),
				It.Is<string>(p => p.Contains("\"Notification\"")),
				It.Is<VapidDetails>(v => v.Subject == "https://bellumgens.com" && v.PublicKey == "test" && v.PrivateKey == "test"),
				It.IsAny<CancellationToken>()), Times.Once);
			VerifyLogged(LogLevel.Warning, Times.Never());
		}

		// Regression: the TeamInvite overload only caught WebPushException, so an ArgumentException from
		// WebPush's subscription validation aborted delivery to every remaining subscriber.
		[Theory]
		[MemberData(nameof(Overloads))]
		public async Task SendNotificationAsync_WhenOneSubscriptionIsInvalid_StillDeliversToTheRest(string overload)
		{
			FailFor("bad", new ArgumentException("Invalid p256dh"));

			await Send(overload, Subscriptions("https://push.example.com/a", "bad", "https://push.example.com/c"));

			Assert.Equal(new[] { "https://push.example.com/a", "bad", "https://push.example.com/c" }, _attemptedEndpoints);
			VerifyLogged(LogLevel.Warning, Times.Once());
		}

		[Theory]
		[MemberData(nameof(Overloads))]
		public async Task SendNotificationAsync_WhenPushServiceRejects_StillDeliversToTheRest(string overload)
		{
			FailFor("https://push.example.com/a", PushServiceError(HttpStatusCode.BadRequest));
			FailFor("https://push.example.com/b", new HttpRequestException("connection reset"));

			await Send(overload, Subscriptions("https://push.example.com/a", "https://push.example.com/b", "https://push.example.com/c"));

			Assert.Equal(3, _attemptedEndpoints.Count);
			VerifyLogged(LogLevel.Warning, Times.Exactly(2));
		}

		[Theory]
		[InlineData(HttpStatusCode.NotFound)]
		[InlineData(HttpStatusCode.Gone)]
		public async Task SendNotificationAsync_WhenSubscriptionExpired_LogsInformationAndContinues(HttpStatusCode status)
		{
			FailFor("https://push.example.com/a", PushServiceError(status));

			await Send("TeamInvite", Subscriptions("https://push.example.com/a", "https://push.example.com/b"));

			Assert.Equal(2, _attemptedEndpoints.Count);
			VerifyLogged(LogLevel.Information, Times.Once());
			VerifyLogged(LogLevel.Warning, Times.Never());
		}

		[Fact]
		public async Task SendNotificationAsync_SkipsNullSubscriptionEntries()
		{
			var subs = Subscriptions("https://push.example.com/a");
			subs.Insert(0, null!);

			await Send("StrategyComment", subs);

			Assert.Equal(new[] { "https://push.example.com/a" }, _attemptedEndpoints);
		}

		[Fact]
		public async Task SendNotificationAsync_WithNoSubscriptions_DoesNothing()
		{
			// The invite has no team, so building its payload would throw; with no subscribers it is never built.
			await _service.SendNotificationAsync(new List<BellumGensPushSubscription>(), new TeamInvite());
			await _service.SendNotificationAsync(null!, new TeamInvite());

			_webPushClient.Verify(c => c.SendNotificationAsync(It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task SendNotificationAsync_SerializesTheNotificationPayload()
		{
			string? payload = null;
			_webPushClient
				.Setup(c => c.SendNotificationAsync(It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
				.Callback<PushSubscription, string, VapidDetails, CancellationToken>((_, p, _, _) => payload = p)
				.Returns(Task.CompletedTask);

			await Send("TeamInvite", Subscriptions("https://push.example.com/a"));

			Assert.NotNull(payload);
			Assert.Contains("You have been invited to join team Bellum Gens", payload);
			Assert.Contains("viewteam", payload);
		}
	}
}
