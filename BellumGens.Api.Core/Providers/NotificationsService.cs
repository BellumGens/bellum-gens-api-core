using BellumGens.Api.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebPush;

namespace BellumGens.Api.Core.Providers
{
	public class NotificationsService : INotificationService
	{
		private string _publicVapidKey;
		private string _privateVapidKey;

		public NotificationsService(AppConfiguration appInfo)
        {
			_publicVapidKey = appInfo.Config.PublicVapidKey;
			_privateVapidKey = appInfo.Config.PrivateVapidKey;
		}

		public async Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamInvite notification)
		{
			var subject = @"https://bellumgens.com";

			foreach (BellumGensPushSubscription sub in subs)
			{
				var subscription = new PushSubscription(sub.endpoint, sub.p256dh, sub.auth);
				var vapidDetails = new VapidDetails(subject, _publicVapidKey, _privateVapidKey);

				var webPushClient = new WebPushClient();
				var payload = new BellumGensNotificationWrapper(notification);
				try
				{
					await webPushClient.SendNotificationAsync(subscription, payload.ToString(), vapidDetails);
				}
				catch (WebPushException exception)
				{
					Console.WriteLine(exception);
				}
			}
		}

		public async Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamInvite notification, NotificationState state)
		{
			var subject = @"https://bellumgens.com";

			foreach (BellumGensPushSubscription sub in subs)
			{
				var subscription = new PushSubscription(sub.endpoint, sub.p256dh, sub.auth);
				var vapidDetails = new VapidDetails(subject, _publicVapidKey, _privateVapidKey);

				var webPushClient = new WebPushClient();
				var payload = new BellumGensNotificationWrapper(notification, state);
				try
				{
					await webPushClient.SendNotificationAsync(subscription, payload.ToString(), vapidDetails);
				}
				catch
				{
				}
			}
		}

		public async Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamApplication notification)
		{
			var subject = @"https://bellumgens.com";

			foreach (BellumGensPushSubscription sub in subs)
			{
				var subscription = new PushSubscription(sub.endpoint, sub.p256dh, sub.auth);
				var vapidDetails = new VapidDetails(subject, _publicVapidKey, _privateVapidKey);

				var webPushClient = new WebPushClient();
				var payload = new BellumGensNotificationWrapper(notification);
				try
				{
					await webPushClient.SendNotificationAsync(subscription, payload.ToString(), vapidDetails);
				}
				catch
				{
				}
			}
		}

		public async Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamApplication notification, NotificationState state)
		{
			var subject = @"https://bellumgens.com";

			foreach (BellumGensPushSubscription sub in subs)
			{
				var subscription = new PushSubscription(sub.endpoint, sub.p256dh, sub.auth);
				var vapidDetails = new VapidDetails(subject, _publicVapidKey, _privateVapidKey);

				var webPushClient = new WebPushClient();
				var payload = new BellumGensNotificationWrapper(notification, state);
				try
				{
					await webPushClient.SendNotificationAsync(subscription, payload.ToString(), vapidDetails);
				}
				catch
				{
				}
			}
		}

		public async Task SendNotificationAsync(List<BellumGensPushSubscription> subs, StrategyComment comment)
		{
			var subject = @"https://bellumgens.com";

			foreach (BellumGensPushSubscription sub in subs)
			{
				var subscription = new PushSubscription(sub.endpoint, sub.p256dh, sub.auth);
				var vapidDetails = new VapidDetails(subject, _publicVapidKey, _privateVapidKey);

				var webPushClient = new WebPushClient();
				var payload = new BellumGensNotificationWrapper(comment);
				try
				{
					await webPushClient.SendNotificationAsync(subscription, payload.ToString(), vapidDetails);
				}
				catch
				{
				}
			}
		}
	}
}