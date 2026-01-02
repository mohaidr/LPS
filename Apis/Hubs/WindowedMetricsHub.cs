using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Apis.Hubs
{
    /// <summary>
    /// SignalR hub for pushing windowed metrics to connected dashboard clients.
    /// Clients join a group based on their iterationId to receive targeted updates.
    /// </summary>
    public class WindowedMetricsHub : Hub
    {
        /// <summary>
        /// Client calls this to subscribe to a specific iteration's windowed metrics.
        /// </summary>
        /// <param name="iterationId">The iteration GUID to subscribe to</param>
        public async Task SubscribeToIteration(string iterationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, iterationId);
            await Clients.Caller.SendAsync("Subscribed", iterationId);
        }

        /// <summary>
        /// Client calls this to unsubscribe from a specific iteration.
        /// </summary>
        /// <param name="iterationId">The iteration GUID to unsubscribe from</param>
        public async Task UnsubscribeFromIteration(string iterationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, iterationId);
            await Clients.Caller.SendAsync("Unsubscribed", iterationId);
        }

        /// <summary>
        /// Client calls this to subscribe to all windowed metrics (broadcast).
        /// </summary>
        public async Task SubscribeToAll()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "all");
            await Clients.Caller.SendAsync("SubscribedToAll");
        }

        public override async Task OnConnectedAsync()
        {
            // Automatically add to "all" group for broadcast
            await Groups.AddToGroupAsync(Context.ConnectionId, "all");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
