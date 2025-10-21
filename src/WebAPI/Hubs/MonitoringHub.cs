using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Hubs
{
    [Authorize]
    public class MonitoringHub : Hub
    {
        private static readonly Dictionary<string, ServerMetrics> _serverMetrics = new();
        private static readonly object _lock = new();

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.Identity?.Name ?? "Anonymous";
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.Identity?.Name ?? "Anonymous";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SubscribeToServerMetrics(int serverId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"server_{serverId}");
            // Send current metrics if available
            ServerMetrics? currentMetrics = null;
            lock (_lock)
            {
                if (_serverMetrics.TryGetValue(serverId.ToString(), out var metrics))
                {
                    currentMetrics = metrics;
                }
            }
            
            if (currentMetrics != null)
            {
                await Clients.Caller.SendAsync("ReceiveServerMetrics", serverId, currentMetrics);
            }
        }

        public async Task UnsubscribeFromServerMetrics(int serverId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"server_{serverId}");
        }

        // Method to broadcast metrics updates (called by background service)
        public static async Task BroadcastServerMetrics(IHubContext<MonitoringHub> hubContext, int serverId, ServerMetrics metrics)
        {
            lock (_lock)
            {
                _serverMetrics[serverId.ToString()] = metrics;
            }

            await hubContext.Clients.Group($"server_{serverId}").SendAsync("ReceiveServerMetrics", serverId, metrics);
        }

        // Method to broadcast alerts
        public static async Task BroadcastAlert(IHubContext<MonitoringHub> hubContext, ServerAlert alert)
        {
            await hubContext.Clients.All.SendAsync("ReceiveAlert", alert);
        }
    }

    public class ServerMetrics
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public long NetworkIn { get; set; }
        public long NetworkOut { get; set; }
        public int ActiveConnections { get; set; }
        public DateTime Timestamp { get; set; }
        public ServerStatus Status { get; set; }
    }

    public class ServerAlert
    {
        public int ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public AlertType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum AlertType
    {
        CpuHigh,
        MemoryHigh,
        DiskFull,
        ServerDown,
        NetworkIssue
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
}