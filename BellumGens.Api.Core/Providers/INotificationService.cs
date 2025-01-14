using BellumGens.Api.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
    public interface INotificationService
    {
        public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamInvite notification);
        public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamInvite notification, NotificationState state);
        public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamApplication notification);
        public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TeamApplication notification, NotificationState state);
        public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, StrategyComment comment);
        public Task SendNotificationAsync(List<BellumGensPushSubscription> subs, TournamentApplication application);

    }
}
