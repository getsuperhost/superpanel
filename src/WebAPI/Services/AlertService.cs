using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace SuperPanel.WebAPI.Services
{
    public class AlertService : IAlertService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AlertService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ISystemMonitoringService _systemMonitoringService;
        private readonly IEmailService _emailService;
        private readonly IHubContext<MonitoringHub> _hubContext;

        public AlertService(
            ApplicationDbContext context,
            ILogger<AlertService> logger,
            HttpClient httpClient,
            ISystemMonitoringService systemMonitoringService,
            IEmailService emailService,
            IHubContext<MonitoringHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClient;
            _systemMonitoringService = systemMonitoringService;
            _emailService = emailService;
            _hubContext = hubContext;
        }

        // Alert Rules Management
        public async Task<IEnumerable<AlertRule>> GetAllAlertRulesAsync()
        {
            return await _context.AlertRules
                .Include(r => r.Server)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<AlertRule> GetAlertRuleByIdAsync(int id)
        {
            var rule = await _context.AlertRules
                .Include(r => r.Server)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rule == null)
                throw new KeyNotFoundException($"Alert rule with ID {id} not found");

            return rule;
        }

        public async Task<AlertRule> CreateAlertRuleAsync(AlertRule rule)
        {
            rule.CreatedAt = DateTime.UtcNow;
            _context.AlertRules.Add(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created alert rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);
            return rule;
        }

        public async Task<AlertRule> UpdateAlertRuleAsync(int id, AlertRule updatedRule)
        {
            var existingRule = await GetAlertRuleByIdAsync(id);

            existingRule.Name = updatedRule.Name;
            existingRule.Type = updatedRule.Type;
            existingRule.Description = updatedRule.Description;
            existingRule.ServerId = updatedRule.ServerId;
            existingRule.MetricName = updatedRule.MetricName;
            existingRule.Condition = updatedRule.Condition;
            existingRule.Threshold = updatedRule.Threshold;
            existingRule.Severity = updatedRule.Severity;
            existingRule.Enabled = updatedRule.Enabled;
            existingRule.CooldownMinutes = updatedRule.CooldownMinutes;
            existingRule.WebhookUrl = updatedRule.WebhookUrl;
            existingRule.EmailRecipients = updatedRule.EmailRecipients;
            existingRule.SlackWebhookUrl = updatedRule.SlackWebhookUrl;
            existingRule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated alert rule: {RuleName} (ID: {RuleId})", existingRule.Name, id);
            return existingRule;
        }

        public async Task DeleteAlertRuleAsync(int id)
        {
            var rule = await GetAlertRuleByIdAsync(id);
            _context.AlertRules.Remove(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted alert rule: {RuleName} (ID: {RuleId})", rule.Name, id);
        }

        // Alert Management
        public async Task<IEnumerable<Alert>> GetAllAlertsAsync(int? serverId = null, AlertStatus? status = null)
        {
            var query = _context.Alerts
                .Include(a => a.AlertRule)
                .Include(a => a.Server)
                .OrderByDescending(a => a.TriggeredAt)
                .AsQueryable();

            if (serverId.HasValue)
                query = query.Where(a => a.ServerId == serverId.Value);

            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);

            return await query.ToListAsync();
        }

        public async Task<Alert> GetAlertByIdAsync(int id)
        {
            var alert = await _context.Alerts
                .Include(a => a.AlertRule)
                .Include(a => a.Server)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (alert == null)
                throw new KeyNotFoundException($"Alert with ID {id} not found");

            return alert;
        }

        public async Task<Alert> CreateAlertAsync(Alert alert)
        {
            alert.TriggeredAt = DateTime.UtcNow;
            _context.Alerts.Add(alert);
            await _context.SaveChangesAsync();

            _logger.LogWarning("Alert triggered: {Title} (ID: {AlertId})", alert.Title, alert.Id);

            // Send notifications
            await SendAlertNotificationsAsync(alert);

            // Track history
            await AddAlertHistoryAsync(alert.Id, "Created", AlertStatus.Active, AlertStatus.Active,
                "Alert was automatically created by monitoring system");

            return alert;
        }

        public async Task<Alert> AcknowledgeAlertAsync(int id)
        {
            var alert = await GetAlertByIdAsync(id);
            var oldStatus = alert.Status;
            alert.Status = AlertStatus.Acknowledged;
            alert.AcknowledgedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Track history
            await AddAlertHistoryAsync(id, "Acknowledged", oldStatus, AlertStatus.Acknowledged,
                "Alert was acknowledged by user");

            _logger.LogInformation("Alert acknowledged: {Title} (ID: {AlertId})", alert.Title, id);
            return alert;
        }

        public async Task<Alert> ResolveAlertAsync(int id)
        {
            var alert = await GetAlertByIdAsync(id);
            var oldStatus = alert.Status;
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Track history
            await AddAlertHistoryAsync(id, "Resolved", oldStatus, AlertStatus.Resolved,
                "Alert was resolved by user");

            _logger.LogInformation("Alert resolved: {Title} (ID: {AlertId})", alert.Title, id);
            return alert;
        }

        // Alert Evaluation
        public async Task EvaluateAlertRulesAsync()
        {
            var enabledRules = await _context.AlertRules
                .Where(r => r.Enabled)
                .Include(r => r.Server)
                .ToListAsync();

            // Get current system metrics for all rules
            ServerMetrics metrics = null;
            try
            {
                var systemInfo = await _systemMonitoringService.GetSystemInfoAsync();
                metrics = new ServerMetrics
                {
                    Status = ServerStatus.Online,
                    CpuUsage = systemInfo.CpuUsagePercent,
                    MemoryUsage = systemInfo.TotalMemoryMB > 0 ?
                        ((double)(systemInfo.TotalMemoryMB - systemInfo.AvailableMemoryMB) / systemInfo.TotalMemoryMB) * 100 : 0,
                    DiskUsage = systemInfo.Drives.Any() ? systemInfo.Drives.Max(d => d.UsagePercent) : 0,
                    Timestamp = systemInfo.LastUpdated
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system metrics for alert evaluation");
                // Continue with null metrics - some rules might still work
            }

            foreach (var rule in enabledRules)
            {
                try
                {
                    if (metrics != null)
                    {
                        await EvaluateAlertRuleWithMetricsAsync(rule, metrics);
                    }
                    else
                    {
                        await EvaluateAlertRuleAsync(rule);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating alert rule {RuleId}: {RuleName}", rule.Id, rule.Name);
                }
            }
        }

        public async Task EvaluateAlertRulesAsync(Server server, ServerMetrics metrics)
        {
            var enabledRules = await _context.AlertRules
                .Where(r => r.Enabled && r.ServerId == server.Id)
                .Include(r => r.Server)
                .ToListAsync();

            foreach (var rule in enabledRules)
            {
                try
                {
                    await EvaluateAlertRuleWithMetricsAsync(rule, metrics);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating alert rule {RuleId}: {RuleName} for server {ServerId}",
                        rule.Id, rule.Name, server.Id);
                }
            }
        }

        private async Task EvaluateAlertRuleAsync(AlertRule rule)
        {
            // Check if rule is in cooldown period
            var lastAlert = await _context.Alerts
                .Where(a => a.AlertRuleId == rule.Id)
                .OrderByDescending(a => a.TriggeredAt)
                .FirstOrDefaultAsync();

            if (lastAlert != null)
            {
                var cooldownEnd = lastAlert.TriggeredAt.AddMinutes(rule.CooldownMinutes);
                if (DateTime.UtcNow < cooldownEnd)
                    return; // Still in cooldown
            }

            // Evaluate based on rule type
            bool shouldTrigger = false;
            string title = "";
            string message = "";
            double? metricValue = null;

            switch (rule.Type)
            {
                case AlertRuleType.ServerDown:
                    (shouldTrigger, title, message) = await EvaluateServerDownRuleAsync(rule);
                    break;
                case AlertRuleType.HighCpuUsage:
                    (shouldTrigger, title, message, metricValue) = await EvaluateCpuUsageRuleAsync(rule);
                    break;
                case AlertRuleType.HighMemoryUsage:
                    (shouldTrigger, title, message, metricValue) = await EvaluateMemoryUsageRuleAsync(rule);
                    break;
                case AlertRuleType.LowDiskSpace:
                    (shouldTrigger, title, message, metricValue) = await EvaluateDiskSpaceRuleAsync(rule);
                    break;
                // Add more rule types as needed
            }

            if (shouldTrigger)
            {
                var alert = new Alert
                {
                    AlertRuleId = rule.Id,
                    ServerId = rule.ServerId ?? 0, // Assuming server-specific rules
                    Title = title,
                    Message = message,
                    Severity = rule.Severity,
                    MetricValue = metricValue,
                    MetricName = rule.MetricName,
                    ContextData = JsonSerializer.Serialize(new
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Threshold = rule.Threshold,
                        Condition = rule.Condition
                    })
                };

                await CreateAlertAsync(alert);
            }
        }

        private async Task EvaluateAlertRuleWithMetricsAsync(AlertRule rule, ServerMetrics metrics)
        {
            // Check if rule is in cooldown period
            var lastAlert = await _context.Alerts
                .Where(a => a.AlertRuleId == rule.Id)
                .OrderByDescending(a => a.TriggeredAt)
                .FirstOrDefaultAsync();

            if (lastAlert != null)
            {
                var cooldownEnd = lastAlert.TriggeredAt.AddMinutes(rule.CooldownMinutes);
                if (DateTime.UtcNow < cooldownEnd)
                    return; // Still in cooldown
            }

            // Evaluate based on rule type using real metrics
            bool shouldTrigger = false;
            string title = "";
            string message = "";
            double? metricValue = null;

            switch (rule.Type)
            {
                case AlertRuleType.ServerDown:
                    (shouldTrigger, title, message) = await EvaluateServerDownRuleAsync(rule, metrics);
                    break;
                case AlertRuleType.HighCpuUsage:
                    (shouldTrigger, title, message, metricValue) = await EvaluateCpuUsageRuleAsync(rule, metrics);
                    break;
                case AlertRuleType.HighMemoryUsage:
                    (shouldTrigger, title, message, metricValue) = await EvaluateMemoryUsageRuleAsync(rule, metrics);
                    break;
                case AlertRuleType.LowDiskSpace:
                    (shouldTrigger, title, message, metricValue) = await EvaluateDiskSpaceRuleAsync(rule, metrics);
                    break;
                // Add more rule types as needed
            }

            if (shouldTrigger)
            {
                var alert = new Alert
                {
                    AlertRuleId = rule.Id,
                    ServerId = rule.ServerId ?? 0,
                    Title = title,
                    Message = message,
                    Severity = rule.Severity,
                    MetricValue = metricValue,
                    MetricName = rule.MetricName,
                    ContextData = JsonSerializer.Serialize(new
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Threshold = rule.Threshold,
                        Condition = rule.Condition
                    })
                };

                await CreateAlertAsync(alert);
            }
        }

        private async Task<(bool shouldTrigger, string title, string message)> EvaluateServerDownRuleAsync(AlertRule rule)
        {
            if (!rule.ServerId.HasValue) return (false, "", "");

            try
            {
                // Try to get system info as a basic health check
                var systemInfo = await _systemMonitoringService.GetSystemInfoAsync();

                // Server is considered "up" if we can get system info
                // In a real distributed system, this would check server connectivity
                var isDown = systemInfo == null || string.IsNullOrEmpty(systemInfo.ServerName);

                var title = $"Server {rule.Server?.Name ?? "Unknown"} is down";
                var message = isDown ?
                    $"Server monitoring indicates the server is unreachable." :
                    $"Server is responding normally.";
                return (isDown, title, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check server status for alert rule {RuleId}", rule.Id);

                // If we can't get system info, consider the server down
                var title = $"Server {rule.Server?.Name ?? "Unknown"} is down";
                var message = $"Server monitoring failed: {ex.Message}";
                return (true, title, message);
            }
        }

        private async Task<(bool shouldTrigger, string title, string message, double? metricValue)> EvaluateCpuUsageRuleAsync(AlertRule rule)
        {
            if (!rule.ServerId.HasValue) return (false, "", "", null);

            try
            {
                var cpuUsage = await _systemMonitoringService.GetCpuUsageAsync();
                var shouldTrigger = EvaluateThreshold(cpuUsage, rule.Threshold, rule.Condition);

                var title = $"High CPU usage on {rule.Server?.Name ?? "Unknown"}";
                var message = $"CPU usage is {cpuUsage:F1}%, threshold: {rule.Threshold}%";
                return (shouldTrigger, title, message, cpuUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get CPU usage for alert rule {RuleId}", rule.Id);
                return (false, "", "", null);
            }
        }

        private async Task<(bool shouldTrigger, string title, string message, double? metricValue)> EvaluateMemoryUsageRuleAsync(AlertRule rule)
        {
            if (!rule.ServerId.HasValue) return (false, "", "", null);

            try
            {
                var availableMemoryMB = await _systemMonitoringService.GetAvailableMemoryAsync();
                var totalMemoryMB = await _systemMonitoringService.GetTotalMemoryAsync();

                if (totalMemoryMB == 0) return (false, "", "", null);

                var memoryUsagePercent = ((double)(totalMemoryMB - availableMemoryMB) / totalMemoryMB) * 100;
                var shouldTrigger = EvaluateThreshold(memoryUsagePercent, rule.Threshold, rule.Condition);

                var title = $"High memory usage on {rule.Server?.Name ?? "Unknown"}";
                var message = $"Memory usage is {memoryUsagePercent:F1}%, threshold: {rule.Threshold}%";
                return (shouldTrigger, title, message, memoryUsagePercent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory usage for alert rule {RuleId}", rule.Id);
                return (false, "", "", null);
            }
        }

        private async Task<(bool shouldTrigger, string title, string message, double? metricValue)> EvaluateDiskSpaceRuleAsync(AlertRule rule)
        {
            if (!rule.ServerId.HasValue) return (false, "", "", null);

            try
            {
                var drives = await _systemMonitoringService.GetDriveInfoAsync();

                // Check if any drive exceeds the threshold
                foreach (var drive in drives)
                {
                    var shouldTrigger = EvaluateThreshold(drive.UsagePercent, rule.Threshold, rule.Condition);
                    if (shouldTrigger)
                    {
                        var title = $"Low disk space on {rule.Server?.Name ?? "Unknown"}";
                        var message = $"Drive {drive.Name} usage is {drive.UsagePercent:F1}%, threshold: {rule.Threshold}%";
                        return (true, title, message, drive.UsagePercent);
                    }
                }

                // If no drive triggered the alert, return the highest usage for monitoring
                var highestUsage = drives.Max(d => d.UsagePercent);
                var checkTitle = $"Disk space check on {rule.Server?.Name ?? "Unknown"}";
                var checkMessage = $"Highest drive usage is {highestUsage:F1}%, threshold: {rule.Threshold}%";
                return (false, checkTitle, checkMessage, highestUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get disk space info for alert rule {RuleId}", rule.Id);
                return (false, "", "", null);
            }
        }

        private async Task<(bool shouldTrigger, string title, string message, double? metricValue)> EvaluateDiskSpaceRuleAsync(AlertRule rule, ServerMetrics metrics)
        {
            if (!rule.ServerId.HasValue) return (false, "", "", null);

            var shouldTrigger = EvaluateThreshold(metrics.DiskUsage, rule.Threshold, rule.Condition);
            var title = $"Low disk space on {rule.Server?.Name ?? "Unknown"}";
            var message = $"Disk usage is {metrics.DiskUsage:F1}%, threshold: {rule.Threshold}%";
            return (shouldTrigger, title, message, metrics.DiskUsage);
        }

        private async Task<(bool shouldTrigger, string title, string message)> EvaluateServerDownRuleAsync(AlertRule rule, ServerMetrics metrics)
        {
            if (!rule.ServerId.HasValue) return (false, "", "");

            // Check if server is offline based on metrics status
            var isDown = metrics.Status != ServerStatus.Online;
            var title = $"Server {rule.Server?.Name ?? "Unknown"} is down";
            var message = $"Server monitoring indicates the server is unreachable.";
            return (isDown, title, message);
        }

        private async Task<(bool shouldTrigger, string title, string message, double? metricValue)> EvaluateCpuUsageRuleAsync(AlertRule rule, ServerMetrics metrics)
        {
            if (!rule.ServerId.HasValue) return (false, "", "", null);

            var shouldTrigger = EvaluateThreshold(metrics.CpuUsage, rule.Threshold, rule.Condition);
            var title = $"High CPU usage on {rule.Server?.Name ?? "Unknown"}";
            var message = $"CPU usage is {metrics.CpuUsage:F1}%, threshold: {rule.Threshold}%";
            return (shouldTrigger, title, message, metrics.CpuUsage);
        }

        private async Task<(bool shouldTrigger, string title, string message, double? metricValue)> EvaluateMemoryUsageRuleAsync(AlertRule rule, ServerMetrics metrics)
        {
            if (!rule.ServerId.HasValue) return (false, "", "", null);

            var shouldTrigger = EvaluateThreshold(metrics.MemoryUsage, rule.Threshold, rule.Condition);
            var title = $"High memory usage on {rule.Server?.Name ?? "Unknown"}";
            var message = $"Memory usage is {metrics.MemoryUsage:F1}%, threshold: {rule.Threshold}%";
            return (shouldTrigger, title, message, metrics.MemoryUsage);
        }

        private bool EvaluateThreshold(double currentValue, double threshold, string condition)
        {
            return condition.ToLower() switch
            {
                "gt" => currentValue > threshold,
                "gte" => currentValue >= threshold,
                "lt" => currentValue < threshold,
                "lte" => currentValue <= threshold,
                "eq" => Math.Abs(currentValue - threshold) < 0.001, // floating point comparison
                "ne" => Math.Abs(currentValue - threshold) >= 0.001,
                _ => false
            };
        }

        // Notification Methods
        private async Task SendAlertNotificationsAsync(Alert alert)
        {
            var rule = alert.AlertRule;
            if (rule == null)
            {
                // Load the alert rule if not already loaded
                rule = await _context.AlertRules.FindAsync(alert.AlertRuleId);
                if (rule == null)
                {
                    _logger.LogWarning("Alert rule {AlertRuleId} not found for alert {AlertId}", alert.AlertRuleId, alert.Id);
                    return;
                }
            }

            // Webhook notification
            if (!string.IsNullOrEmpty(rule.WebhookUrl))
            {
                await SendWebhookNotificationAsync(rule.WebhookUrl, alert);
            }

            // Email notification
            if (!string.IsNullOrEmpty(rule.EmailRecipients))
            {
                await SendEmailNotificationAsync(rule.EmailRecipients, alert);
            }

            // Slack notification
            if (!string.IsNullOrEmpty(rule.SlackWebhookUrl))
            {
                await SendSlackNotificationAsync(rule.SlackWebhookUrl, alert);
            }
        }

        private async Task SendWebhookNotificationAsync(string webhookUrl, Alert alert)
        {
            try
            {
                var payload = new
                {
                    alertId = alert.Id,
                    title = alert.Title,
                    message = alert.Message,
                    severity = alert.Severity.ToString(),
                    serverId = alert.ServerId,
                    serverName = alert.Server?.Name,
                    triggeredAt = alert.TriggeredAt,
                    metricValue = alert.MetricValue,
                    metricName = alert.MetricName
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Webhook notification sent for alert {AlertId}", alert.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send webhook notification for alert {AlertId}", alert.Id);
            }
        }

        private async Task SendEmailNotificationAsync(string emailRecipients, Alert alert)
        {
            try
            {
                var emails = JsonSerializer.Deserialize<string[]>(emailRecipients) ?? new string[0];

                if (emails.Length == 0)
                {
                    _logger.LogWarning("No email recipients specified for alert {AlertId}", alert.Id);
                    return;
                }

                var subject = $"SuperPanel Alert: {alert.Title}";
                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>SuperPanel Alert</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px 8px 0 0; margin: -30px -30px 20px -30px; }}
        .alert-title {{ font-size: 24px; font-weight: bold; margin: 0; }}
        .alert-badge {{ display: inline-block; padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: bold; text-transform: uppercase; }}
        .alert-critical {{ background-color: #dc3545; color: white; }}
        .alert-warning {{ background-color: #ffc107; color: #212529; }}
        .alert-info {{ background-color: #17a2b8; color: white; }}
        .alert-error {{ background-color: #fd7e14; color: white; }}
        .content {{ margin: 20px 0; }}
        .metric {{ background-color: #f8f9fa; padding: 15px; border-radius: 6px; margin: 10px 0; }}
        .metric strong {{ color: #495057; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; color: #6c757d; font-size: 14px; }}
        .server-info {{ background-color: #e9ecef; padding: 10px; border-radius: 4px; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 class='alert-title'>🚨 {alert.Title}</h1>
        </div>

        <div class='content'>
            <p><strong>Alert Details:</strong></p>
            <p>{alert.Message}</p>

            <div class='server-info'>
                <strong>Server:</strong> {alert.Server?.Name ?? "Unknown"}<br>
                <strong>Triggered:</strong> {alert.TriggeredAt:yyyy-MM-dd HH:mm:ss UTC}<br>
                <strong>Severity:</strong> <span class='alert-badge alert-{alert.Severity.ToString().ToLower()}'>{alert.Severity}</span>
            </div>

            {(alert.MetricValue.HasValue ? $@"
            <div class='metric'>
                <strong>Metric:</strong> {alert.MetricName ?? "N/A"}<br>
                <strong>Value:</strong> {alert.MetricValue.Value:F2}{(alert.MetricName?.ToLower().Contains("usage") ?? false ? "%" : "")}
            </div>" : "")}
        </div>

        <div class='footer'>
            <p>This alert was generated by SuperPanel monitoring system.</p>
            <p>Alert ID: {alert.Id} | Server ID: {alert.ServerId}</p>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(emails, subject, body, true);

                _logger.LogInformation("Email notification sent to {Count} recipients for alert {AlertId}",
                    emails.Length, alert.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification for alert {AlertId}", alert.Id);
            }
        }

        private async Task SendSlackNotificationAsync(string slackWebhookUrl, Alert alert)
        {
            try
            {
                var payload = new
                {
                    text = $"🚨 *{alert.Title}*\n{alert.Message}\nSeverity: {alert.Severity}\nServer: {alert.Server?.Name ?? "Unknown"}",
                    attachments = new[]
                    {
                        new
                        {
                            color = GetSlackColorForSeverity(alert.Severity),
                            fields = new[]
                            {
                                new { title = "Alert ID", value = alert.Id.ToString(), @short = true },
                                new { title = "Server", value = alert.Server?.Name ?? "Unknown", @short = true },
                                new { title = "Triggered", value = alert.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true },
                                alert.MetricValue.HasValue ?
                                    new { title = alert.MetricName ?? "Metric", value = alert.MetricValue.Value.ToString(), @short = true } :
                                    null
                            }.Where(f => f != null)
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(slackWebhookUrl, content);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Slack notification sent for alert {AlertId}", alert.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Slack notification for alert {AlertId}", alert.Id);
            }
        }

        private string GetSlackColorForSeverity(AlertRuleSeverity severity)
        {
            return severity switch
            {
                AlertRuleSeverity.Info => "good",
                AlertRuleSeverity.Warning => "warning",
                AlertRuleSeverity.Error => "danger",
                AlertRuleSeverity.Critical => "danger",
                _ => "warning"
            };
        }

        // Statistics
        public async Task<AlertStats> GetAlertStatsAsync()
        {
            var stats = new AlertStats();

            var alerts = await _context.Alerts.ToListAsync();

            stats.TotalAlerts = alerts.Count;
            stats.ActiveAlerts = alerts.Count(a => a.Status == AlertStatus.Active);
            stats.AcknowledgedAlerts = alerts.Count(a => a.Status == AlertStatus.Acknowledged);
            stats.ResolvedAlerts = alerts.Count(a => a.Status == AlertStatus.Resolved);

            // Severity breakdown
            stats.InfoAlerts = alerts.Count(a => a.Severity == AlertRuleSeverity.Info);
            stats.WarningAlerts = alerts.Count(a => a.Severity == AlertRuleSeverity.Warning);
            stats.ErrorAlerts = alerts.Count(a => a.Severity == AlertRuleSeverity.Error);
            stats.CriticalAlerts = alerts.Count(a => a.Severity == AlertRuleSeverity.Critical);

            // Recent alerts (last 24 hours)
            var yesterday = DateTime.UtcNow.AddDays(-1);
            stats.RecentAlerts = alerts.Count(a => a.TriggeredAt >= yesterday);

            return stats;
        }

        // Alert History and Comments
        public async Task AddAlertHistoryAsync(int alertId, string action, AlertStatus oldStatus, AlertStatus newStatus, string description = null, string performedBy = "System")
        {
            var history = new AlertHistory
            {
                AlertId = alertId,
                Action = action,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Description = description,
                PerformedBy = performedBy,
                Timestamp = DateTime.UtcNow
            };

            _context.AlertHistories.Add(history);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Alert history recorded: Alert {AlertId}, Action: {Action}, Status: {OldStatus} -> {NewStatus}",
                alertId, action, oldStatus, newStatus);
        }

        public async Task<IEnumerable<AlertHistory>> GetAlertHistoryAsync(int alertId)
        {
            return await _context.AlertHistories
                .Where(h => h.AlertId == alertId)
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync();
        }

        public async Task<AlertComment> AddAlertCommentAsync(int alertId, string comment, string commentType = "General", string createdBy = "System")
        {
            var alertComment = new AlertComment
            {
                AlertId = alertId,
                Comment = comment,
                CommentType = commentType,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.AlertComments.Add(alertComment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Alert comment added: Alert {AlertId}, Type: {CommentType}, By: {CreatedBy}",
                alertId, commentType, createdBy);

            return alertComment;
        }

        public async Task<IEnumerable<AlertComment>> GetAlertCommentsAsync(int alertId)
        {
            return await _context.AlertComments
                .Where(c => c.AlertId == alertId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task AcknowledgeAlertWithCommentAsync(int alertId, string comment = null, string acknowledgedBy = "System")
        {
            var alert = await AcknowledgeAlertAsync(alertId);

            if (!string.IsNullOrEmpty(comment))
            {
                await AddAlertCommentAsync(alertId, comment, "Acknowledgment", acknowledgedBy);
            }
        }

        public async Task ResolveAlertWithCommentAsync(int alertId, string comment = null, string resolvedBy = "System")
        {
            var alert = await ResolveAlertAsync(alertId);

            if (!string.IsNullOrEmpty(comment))
            {
                await AddAlertCommentAsync(alertId, comment, "Resolution", resolvedBy);
            }
        }

        // Testing
        public async Task TestAlertRuleAsync(int alertRuleId)
        {
            var alertRule = await _context.AlertRules.FindAsync(alertRuleId);
            if (alertRule == null)
            {
                throw new KeyNotFoundException($"Alert rule with ID {alertRuleId} not found");
            }

            // Ensure server exists
            var server = await _context.Servers.FirstOrDefaultAsync();
            if (server == null)
            {
                server = new Server
                {
                    Name = "TestServer-01",
                    IpAddress = "127.0.0.1",
                    Description = "Test server for alerts",
                    OperatingSystem = "Test OS",
                    Status = ServerStatus.Online,
                    CpuUsage = 10.0,
                    MemoryUsage = 20.0,
                    DiskUsage = 30.0,
                    UserId = 1,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Servers.Add(server);
                await _context.SaveChangesAsync();
            }

            // Create a test alert
            var testAlert = new Alert
            {
                AlertRuleId = alertRule.Id,
                ServerId = server.Id,
                Title = $"Test Alert: {alertRule.Name}",
                Message = $"Test alert for rule: {alertRule.Name}. Metric value: {(alertRule.Threshold + 5):F2}",
                Severity = alertRule.Severity,
                Status = AlertStatus.Active,
                TriggeredAt = DateTime.UtcNow,
                MetricValue = alertRule.Threshold + 5,
                MetricName = alertRule.MetricName,
                ContextData = $"Test alert generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}"
            };

            // Save the test alert
            _context.Alerts.Add(testAlert);
            await _context.SaveChangesAsync();

            // Send notifications
            await SendEmailNotificationAsync(alertRule.EmailRecipients ?? "[]", testAlert);
            await SendWebhookNotificationAsync(alertRule.WebhookUrl, testAlert);
            await SendSlackNotificationAsync(alertRule.SlackWebhookUrl, testAlert);

            // Broadcast to SignalR clients
            await _hubContext.Clients.All.SendAsync("ReceiveAlert", testAlert);

            _logger.LogInformation("Test alert created and notifications sent for rule {RuleId}", alertRuleId);
        }
    }

    public class AlertStats
    {
        public int TotalAlerts { get; set; }
        public int ActiveAlerts { get; set; }
        public int AcknowledgedAlerts { get; set; }
        public int ResolvedAlerts { get; set; }
        public int InfoAlerts { get; set; }
        public int WarningAlerts { get; set; }
        public int ErrorAlerts { get; set; }
        public int CriticalAlerts { get; set; }
        public int RecentAlerts { get; set; }
    }
}