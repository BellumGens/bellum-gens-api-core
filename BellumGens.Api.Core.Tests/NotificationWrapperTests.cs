using System;
using BellumGens.Api.Core.Models;

namespace BellumGens.Api.Core.Tests
{
	public class NotificationWrapperTests
	{
		[Fact]
		public void TeamInvite_WithoutTeamLoaded_DoesNotThrow()
		{
			var invite = new TeamInvite { TeamId = Guid.NewGuid(), InvitedUserId = "user1" };

			var wrapper = new BellumGensNotificationWrapper(invite);

			Assert.NotNull(wrapper.Notification);
			Assert.Equal(invite.TeamId, wrapper.Notification.Data);
		}

		[Fact]
		public void AcceptedTeamInvite_ForUserWithoutCSGODetails_DoesNotThrow()
		{
			var invite = new TeamInvite
			{
				InvitedUserId = "sc2user",
				InvitedUser = new ApplicationUser { Id = "sc2user", UserName = "sc2player" },
				Team = new CSGOTeam { TeamName = "Team1" }
			};

			var wrapper = new BellumGensNotificationWrapper(invite, NotificationState.Accepted);

			Assert.Equal("sc2player has accepted your invitation to join Team1!", wrapper.Notification.Title);
			Assert.Null(wrapper.Notification.Icon);
		}

		[Fact]
		public void TeamApplication_ForUserWithoutCSGODetails_DoesNotThrow()
		{
			var application = new TeamApplication
			{
				ApplicantId = "sc2user",
				User = new ApplicationUser { Id = "sc2user", UserName = "sc2player" },
				Team = new CSGOTeam { TeamName = "Team1" }
			};

			var wrapper = new BellumGensNotificationWrapper(application);

			Assert.Equal("sc2player has applied to join Team1", wrapper.Notification.Title);
			Assert.Null(wrapper.Notification.Icon);
		}

		[Fact]
		public void AcceptedTeamApplication_WithoutTeamLoaded_DoesNotThrow()
		{
			var application = new TeamApplication { ApplicantId = "user1", TeamId = Guid.NewGuid() };

			var wrapper = new BellumGensNotificationWrapper(application, NotificationState.Accepted);

			Assert.Equal(application.TeamId, wrapper.Notification.Data);
		}
	}
}
