using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Hubs;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using System.Net.Http;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

public class AlertServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<AlertService>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ISystemMonitoringService> _systemMonitoringServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly IHubContext<MonitoringHub>? _hubContextMock;
    private readonly AlertService _alertService;

    public AlertServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<AlertService>>();
        _httpClient = new HttpClient();
        _systemMonitoringServiceMock = new Mock<ISystemMonitoringService>();
        _emailServiceMock = new Mock<IEmailService>();
        // Skip SignalR for now - focus on core alert functionality
        _hubContextMock = null;

        _alertService = new AlertService(_context, _loggerMock.Object, _httpClient, _systemMonitoringServiceMock.Object, _emailServiceMock.Object, _hubContextMock);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var server = new Server
        {
            Id = 1,
            Name = "Test Server",
            IpAddress = "192.168.1.100",
            Description = "Test server",
            OperatingSystem = "Ubuntu 22.04",
            Status = ServerStatus.Online,
            CpuUsage = 50.0,
            MemoryUsage = 60.0,
            DiskUsage = 70.0,
            UserId = 1,
            CreatedAt = DateTime.UtcNow
        };

        var alertRule = new AlertRule
        {
            Id = 1,
            Name = "High CPU Alert",
            Description = "Alert when CPU usage is high",
            Type = AlertRuleType.HighCpuUsage,
            ServerId = 1,
            MetricName = "CPU",
            Condition = ">",
            Threshold = 80.0,
            Severity = AlertRuleSeverity.Warning,
            Enabled = true,
            CooldownMinutes = 5,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            WebhookUrl = "",
            EmailRecipients = "",
            SlackWebhookUrl = ""
        };

        _context.Servers.Add(server);
        _context.AlertRules.Add(alertRule);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetAllAlertRulesAsync_ShouldReturnAllRules()
    {
        // Act
        var result = await _alertService.GetAllAlertRulesAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("High CPU Alert");
    }

    [Fact]
    public async Task GetAlertRuleByIdAsync_WithValidId_ShouldReturnRule()
    {
        // Act
        var result = await _alertService.GetAlertRuleByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("High CPU Alert");
    }

    [Fact]
    public async Task GetAlertRuleByIdAsync_WithInvalidId_ShouldThrowKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _alertService.GetAlertRuleByIdAsync(999));
    }

    [Fact]
    public async Task CreateAlertRuleAsync_ShouldCreateAndReturnRule()
    {
        // Arrange
        var newRule = new AlertRule
        {
            Name = "Memory Alert",
            Description = "High memory usage",
            ServerId = 1,
            MetricName = "Memory",
            Condition = ">",
            Threshold = 90.0,
            Severity = AlertRuleSeverity.Error,
            Enabled = true,
            CooldownMinutes = 10,
            WebhookUrl = "",
            EmailRecipients = "",
            SlackWebhookUrl = ""
        };

        // Act
        var result = await _alertService.CreateAlertRuleAsync(newRule);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(0);
        result.Name.Should().Be("Memory Alert");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Verify it was saved to database
        var savedRule = await _context.AlertRules.FindAsync(result.Id);
        savedRule.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAlertStatsAsync_ShouldReturnCorrectStats()
    {
        // Act
        var result = await _alertService.GetAlertStatsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalAlerts.Should().Be(0); // No alerts in test data
        result.ActiveAlerts.Should().Be(0);
        result.InfoAlerts.Should().Be(0);
        result.WarningAlerts.Should().Be(0);
        result.ErrorAlerts.Should().Be(0);
        result.CriticalAlerts.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}