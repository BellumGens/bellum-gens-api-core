using BellumGens.Api.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using WebPush;

namespace BellumGens.Api.Core.Providers
{
	public class NotificationsService : INotificationService
	{
		private const string Subject = "https://bellumgens.com";

		private readonly IWebPushClient _webPushClient;
		private readonly VapidDetails _vapidDetails;
		private readonly ILogger<NotificationsService> _logger;

		public NotificationsService(IWebPushClient webPushClient, AppConfiguration appInfo, ILogger<NotificationsService> logger)
        {
			_webPushClient = webPushClient;
			_logger = logger;
			// VAPID details are passed per send (rather than configured on the client at registration)
			// so a missing key in local dev only fails the send, not service construction.
			_vapidDetails = new VapidDetails(Subject, appInfo.Config.PublicVapidKey, appInfo.Config.PrivateVapidKey);
		}

		public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamInvite notification)
		{
			return SendToAllAsync(subs, () => new BellumGensNotificationWrapper(notification));
		}

		public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamInvite notification, NotificationState state)
		{
			return SendToAllAsync(subs, () => new BellumGensNotificationWrapper(notification, state));
		}

		public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamApplication notification)
		{
			return SendToAllAsync(subs, () => new BellumGensNotificationWrapper(notification));
		}

		public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamApplication notification, NotificationState state)
		{
			return SendToAllAsync(subs, () => new BellumGensNotificationWrapper(notification, state));
		}

		public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, StrategyComment comment)
		{
			return SendToAllAsync(subs, () => new BellumGensNotificationWrapper(comment));
        }

		public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TournamentApplication application, string callbackUrl)
		{
			return SendToAllAsync(subs, () => new BellumGensNotificationWrapper(application, callbackUrl));
		}

		// Delivers the payload to every subscription. A failure for one subscription (expired endpoint,
		// invalid keys, network error) is logged and never stops delivery to the remaining ones.
		private async Task SendToAllAsync(List<BellumGensPushSubscription> subs, Func<BellumGensNotificationWrapper> createPayload)
		{
			if (subs == null || subs.Count == 0)
			{
				return;
			}

			string payload = createPayload().ToString();
			foreach (BellumGensPushSubscription sub in subs)
			{
				if (sub == null)
				{
					continue;
				}

				try
				{
					var subscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
					await _webPushClient.SendNotificationAsync(subscription, payload, _vapidDetails);
				}
				catch (WebPushException exception) when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
				{
					_logger.LogInformation("Push subscription {Endpoint} for user {UserId} has expired ({StatusCode}).", sub.Endpoint, sub.UserId, (int)exception.StatusCode);
				}
				catch (WebPushException exception)
				{
					_logger.LogWarning(exception, "Push service rejected notification for subscription {Endpoint} of user {UserId} ({StatusCode}).", sub.Endpoint, sub.UserId, (int)exception.StatusCode);
				}
				catch (Exception exception)
				{
					_logger.LogWarning(exception, "Failed to send push notification to subscription {Endpoint} of user {UserId}.", sub.Endpoint, sub.UserId);
				}
			}
		}
	}
}
