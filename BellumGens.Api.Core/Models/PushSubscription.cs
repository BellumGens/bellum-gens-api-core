using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace BellumGens.Api.Core.Models
{
	public class BellumGensPushSubscription
	{
        public string UserId { get; set; }

		public string Endpoint { get; set; }

		public TimeSpan? ExpirationTime { get; set; }

        public string P256dh { get; set; }

		public string Auth { get; set; }

		[ForeignKey("UserId")]
		public virtual ApplicationUser User { get; set; }
	}

	public class BellumGensPushSubscriptionViewModel
	{
		public string Endpoint { get; set; }

		public TimeSpan? ExpirationTime { get; set; }

		public SubKeys Keys { get; set; }

		public class SubKeys
		{
			public string P256dh { get; set; }

			public string Auth { get; set; }
		}
	}

	public class BellumGensNotificationWrapper
	{
		public BellumGensNotificationWrapper(TeamInvite invite)
		{
			Notification = new BellumGensNotification()
			{
				Title = $"You have been invited to join team {invite.TeamInfo.TeamName}",
				Icon = invite.TeamInfo.TeamAvatar,
				Data = invite.TeamId,
				Renotify = true,
				Actions = new List<BellumGensNotificationAction>()
				{
					new BellumGensNotificationAction()
					{
						Action = "viewteam",
						Title = "View team"
					}
				}
			};
		}

		public BellumGensNotificationWrapper(TeamInvite invite, NotificationState state)
		{
			if (state == NotificationState.Accepted)
			{
				Notification = new BellumGensNotification()
				{
					Title = $"{invite.InvitedUser.UserName} has accepted your invitation to join {invite.TeamInfo.TeamName}!",
					Icon = invite.InvitedUser.CSGODetails.AvatarFull,
					Data = invite.InvitedUserId,
					Renotify = true,
					Actions = new List<BellumGensNotificationAction>()
					{
						new BellumGensNotificationAction()
						{
							Action = "viewuser",
							Title = "View player"
						}
					}
				};
			}
		}

		public BellumGensNotificationWrapper(TeamApplication application)
		{
			Notification = new BellumGensNotification()
			{
				Title = $"{application.User.UserName} has applied to join {application.Team.TeamName}",
				Icon = application.User.CSGODetails.AvatarFull,
				Data = application.ApplicantId,
				Renotify = true,
				Actions = new List<BellumGensNotificationAction>()
				{
					new BellumGensNotificationAction()
					{
						Action = "viewuser",
						Title = "View player"
					}
				}
			};
		}

		public BellumGensNotificationWrapper(TeamApplication application, NotificationState state)
		{
			if (state == NotificationState.Accepted)
			{
				Notification = new BellumGensNotification()
				{
					Title = $"You have been accepted to join team {application.Team.TeamName}",
					Icon = application.Team.TeamAvatar,
					Data = application.TeamId,
					Renotify = true,
					Actions = new List<BellumGensNotificationAction>()
					{
						new BellumGensNotificationAction()
						{
							Action = "viewteam",
							Title = "View team"
						}
					}
				};
			}
		}

		public BellumGensNotificationWrapper(StrategyComment comment)
		{
			Notification = new BellumGensNotification()
			{
				Title = $"New comments on your strategy",
				Icon = comment.UserAvatar,
				Data = comment.StratId,
				Renotify = true,
				Actions = new List<BellumGensNotificationAction>()
				{
					new BellumGensNotificationAction()
					{
						Action = "viewstrategy",
						Title = "View comments"
					}
				}
			};
		}

		public BellumGensNotification Notification { get; set; }

		public override string ToString()
		{
			return JsonSerializer.Serialize(this);
		}
	}

	public class BellumGensNotification
	{
		public string Title { get; set; }

		public string Dir { get; set; } = "ltr";

		public string Lang { get; set; } = "en";

		public string Badge { get; set; } = "https://bellumgens.com/assets/icons/icon-72x72.png";

		public string Icon { get; set; }

		public string Tag { get; set; }

		public string Image { get; set; } = "https://bellumgens.com/assets/icons/icon-192x192.png";

		public object Data { get; set; }

		public int[] Vibrate { get; set; } = { 200, 100, 200 };

		public bool Renotify { get; set; }

		public bool RequireInteraction { get; set; }

		public List<BellumGensNotificationAction> Actions { get; set; }
	}

	public class BellumGensNotificationAction
	{
		public string Action { get; set; }

		public string Title { get; set; }

		public string Icon { get; set; }
	}
}