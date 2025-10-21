using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperPanel.WebAPI.Hubs;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Data;
using Microsoft.Extensions.DependencyInjection;
using SuperPanel.WebAPI.Services;

namespace SuperPanel.WebAPI.Services
{
    public class ServerMonitoringService : BackgroundService
    {
        private readonly ILogger<ServerMonitoringService> _logger;
        private readonly IHubContext<MonitoringHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _monitoringInterval = TimeSpan.FromSeconds(5);

        public ServerMonitoringService(
            ILogger<ServerMonitoringService> logger,
            IHubContext<MonitoringHub> hubContext,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Server Monitoring Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectAndBroadcastMetrics();
                    await Task.Delay(_monitoringInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in server monitoring service");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait longer on error
                }
            }
        }

        private async Task CollectAndBroadcastMetrics()
        {
            // For demo purposes, we'll simulate metrics for the seeded servers
            // In a real implementation, this would connect to actual servers via SSH, WMI, or APIs

            var servers = new[]
            {
                new { Id = 1, Name = "WebServer-01" },
                new { Id = 2, Name = "Database-01" },
                new { Id = 3, Name = "MailServer-01" }
            };

            foreach (var server in servers)
            {
                var metrics = GenerateMockMetrics(server.Id);
                await MonitoringHub.BroadcastServerMetrics(_hubContext, server.Id, metrics);

                // Check for alerts
                await CheckForAlerts(server.Id, server.Name, metrics);
            }
        }

        private ServerMetrics GenerateMockMetrics(int serverId)
        {
            var random = new Random(serverId + (int)DateTime.UtcNow.Ticks);

            // Simulate realistic but varying metrics
            var baseCpu = serverId == 1 ? 45.0 : serverId == 2 ? 23.0 : 12.0;
            var baseMemory = serverId == 1 ? 68.0 : serverId == 2 ? 54.0 : 34.0;
            var baseDisk = serverId == 1 ? 72.0 : serverId == 2 ? 45.0 : 28.0;

            return new ServerMetrics
            {
                CpuUsage = Math.Max(0, Math.Min(100, baseCpu + random.NextDouble() * 20 - 10)),
                MemoryUsage = Math.Max(0, Math.Min(100, baseMemory + random.NextDouble() * 15 - 7.5)),
                DiskUsage = Math.Max(0, Math.Min(100, baseDisk + random.NextDouble() * 10 - 5)),
                NetworkIn = random.Next(1000000, 10000000), // bytes per second
                NetworkOut = random.Next(500000, 5000000),  // bytes per second
                ActiveConnections = random.Next(10, 200),
                Timestamp = DateTime.UtcNow,
                Status = ServerStatus.Online
            };
        }

        private async Task CheckForAlerts(int serverId, string serverName, ServerMetrics metrics)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Get the server from database
                var server = await dbContext.Servers.FindAsync(serverId);
                if (server == null) return;

                // Evaluate alert rules for this server and metrics
                await alertService.EvaluateAlertRulesAsync(server, metrics);
            }
        }
    }
}