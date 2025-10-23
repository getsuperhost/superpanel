using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperPanel.WebAPI.Controllers;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

/// <summary>
/// Integration tests for AlertsController endpoints.
/// Tests the full HTTP pipeline for alert CRUD operations.
/// </summary>
public class AlertsControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string _authToken = string.Empty;
    private int _testUserId;

    public AlertsControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        // Register a test user with unique identifier
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        var registerRequest = new RegisterRequest
        {
            Username = $"alertsuser_{uniqueId}",
            Email = $"alerts_{uniqueId}@example.com",
            Password = "AlertPass123!",
            Role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Login to get token
        var loginRequest = new LoginRequest
        {
            Username = registerRequest.Username,
            Password = registerRequest.Password
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loginResult.Should().NotBeNull();
        loginResult!.Token.Should().NotBeNullOrEmpty();
        _testUserId = loginResult.User.Id;

        return loginResult.Token;
    }

    private async Task<int> CreateTestServerAsync(string token)
    {
        var server = new Server
        {
            Name = "Test Server",
            // Use a unique IpAddress string per call to avoid UNIQUE constraint collisions in shared in-memory DB
            IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
            Status = ServerStatus.Online,
            OperatingSystem = "Linux",
            UserId = _testUserId
        };

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/Servers", server);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<Server>();
        return result?.Id ?? throw new InvalidOperationException("Failed to create test server");
    }

    private async Task<int> CreateTestAlertRuleAsync(string token)
    {
        var alertRule = new AlertRule
        {
            Name = "Test Alert Rule",
            Description = "A test alert rule",
            Type = AlertRuleType.HighCpuUsage,
            UserId = _testUserId,
            Condition = "gt",
            Threshold = 80.0,
            Severity = AlertRuleSeverity.Warning,
            Enabled = true,
            CooldownMinutes = 5
        };

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/AlertRules", alertRule);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AlertRule>();
        return result?.Id ?? throw new InvalidOperationException("Failed to create test alert rule");
    }

    private async Task<int> CreateTestAlertAsync(string token, int alertRuleId, int serverId)
    {
        var alert = new Alert
        {
            AlertRuleId = alertRuleId,
            ServerId = serverId,
            Title = "Test Alert",
            Message = "This is a test alert",
            Severity = AlertRuleSeverity.Warning,
            Status = AlertStatus.Active,
            MetricValue = 85.0,
            MetricName = "cpu_usage",
            ContextData = "{\"additional_info\": \"test data\"}"
        };

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/Alerts", alert);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create alert: {response.StatusCode} - {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<Alert>();
        return result?.Id ?? throw new InvalidOperationException("Failed to create test alert");
    }



        [Fact]
        public async Task GetAlerts_ShouldReturnEmptyList_WhenNoAlertsExist()
        {
            // Arrange - Reset database to ensure no alerts exist
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Alerts.RemoveRange(db.Alerts);
            db.AlertRules.RemoveRange(db.AlertRules);
            db.Servers.RemoveRange(db.Servers);
            db.Users.RemoveRange(db.Users);
            await db.SaveChangesAsync();

            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/Alerts");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var alerts = await response.Content.ReadFromJsonAsync<List<Alert>>();
            alerts.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAlerts_ShouldReturnAlerts_WhenAlertsExist()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/Alerts");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var alerts = await response.Content.ReadFromJsonAsync<List<Alert>>();
            alerts.Should().NotBeEmpty();
            alerts.Should().Contain(a => a.Id == alertId);
        }

        [Fact]
        public async Task GetAlerts_ShouldFilterByServerId_WhenServerIdProvided()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync($"/api/Alerts?serverId={serverId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var alerts = await response.Content.ReadFromJsonAsync<List<Alert>>();
            alerts.Should().NotBeEmpty();
            alerts.Should().AllSatisfy(a => a.ServerId.Should().Be(serverId));
        }

        [Fact]
        public async Task GetAlerts_ShouldFilterByStatus_WhenStatusProvided()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/Alerts?status=1"); // Active = 1

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var alerts = await response.Content.ReadFromJsonAsync<List<Alert>>();
            alerts.Should().NotBeEmpty();
            alerts.Should().AllSatisfy(a => a.Status.Should().Be(AlertStatus.Active));
        }

        [Fact]
        public async Task GetAlert_ShouldReturnAlert_WhenAlertExists()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync($"/api/Alerts/{alertId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var alert = await response.Content.ReadFromJsonAsync<Alert>();
            alert.Should().NotBeNull();
            alert!.Id.Should().Be(alertId);
            alert.Title.Should().Be("Test Alert");
        }

        [Fact]
        public async Task GetAlert_ShouldReturnNotFound_WhenAlertDoesNotExist()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/Alerts/99999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateAlert_ShouldCreateAlert_WhenValidDataProvided()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);

            var alert = new Alert
            {
                AlertRuleId = alertRuleId,
                ServerId = serverId,
                Title = "New Test Alert",
                Message = "This is a new test alert",
                Severity = AlertRuleSeverity.Error,
                Status = AlertStatus.Active,
                MetricValue = 95.0,
                MetricName = "memory_usage",
                ContextData = "{\"test\": \"data\"}"
            };

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PostAsJsonAsync("/api/Alerts", alert);

            // Debug: Check error response
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response: {response.StatusCode} - {errorContent}");
            }

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var createdAlert = await response.Content.ReadFromJsonAsync<Alert>();
            createdAlert.Should().NotBeNull();
            createdAlert!.Title.Should().Be("New Test Alert");
            createdAlert.Severity.Should().Be(AlertRuleSeverity.Error);
        }

        [Fact]
        public async Task CreateAlert_ShouldReturnBadRequest_WhenInvalidDataProvided()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var serverId = await CreateTestServerAsync(token);

            var invalidAlert = new Alert
            {
                AlertRuleId = 0, // Invalid
                ServerId = serverId,
                Title = "", // Invalid - empty
                Message = "Test message",
                Severity = AlertRuleSeverity.Warning,
                Status = AlertStatus.Active
            };

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PostAsJsonAsync("/api/Alerts", invalidAlert);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AcknowledgeAlert_ShouldAcknowledgeAlert_WhenAlertExists()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PutAsync($"/api/Alerts/{alertId}/acknowledge", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var acknowledgedAlert = await response.Content.ReadFromJsonAsync<Alert>();
            acknowledgedAlert.Should().NotBeNull();
            acknowledgedAlert!.Status.Should().Be(AlertStatus.Acknowledged);
            acknowledgedAlert.AcknowledgedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task AcknowledgeAlert_ShouldReturnNotFound_WhenAlertDoesNotExist()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PutAsync("/api/Alerts/99999/acknowledge", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ResolveAlert_ShouldResolveAlert_WhenAlertExists()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PutAsync($"/api/Alerts/{alertId}/resolve", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var resolvedAlert = await response.Content.ReadFromJsonAsync<Alert>();
            resolvedAlert.Should().NotBeNull();
            resolvedAlert!.Status.Should().Be(AlertStatus.Resolved);
            resolvedAlert.ResolvedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task AcknowledgeAlertWithComment_ShouldAcknowledgeWithComment_WhenValidData()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            var commentRequest = new
            {
                Comment = "Acknowledging this alert for testing",
                CommentType = "Acknowledgment",
                User = "TestUser"
            };

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PutAsJsonAsync($"/api/Alerts/{alertId}/acknowledge-with-comment", commentRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var jsonDoc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
            var message = jsonDoc!.RootElement.GetProperty("message").GetString();
            message.Should().NotBeNull();
            message.Should().Be("Alert acknowledged with comment");
        }

        [Fact]
        public async Task ResolveAlertWithComment_ShouldResolveWithComment_WhenValidData()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            var commentRequest = new
            {
                Comment = "Resolving this alert after investigation",
                CommentType = "Resolution",
                User = "TestUser"
            };

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PutAsJsonAsync($"/api/Alerts/{alertId}/resolve-with-comment", commentRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var jsonDoc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
            var message = jsonDoc!.RootElement.GetProperty("message").GetString();
            message.Should().NotBeNull();
            message.Should().Be("Alert resolved with comment");
        }

        [Fact]
        public async Task GetAlertHistory_ShouldReturnHistory_WhenAlertExists()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            // Acknowledge the alert to create history
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            await _client.PutAsync($"/api/Alerts/{alertId}/acknowledge", null);

            // Act
            var response = await _client.GetAsync($"/api/Alerts/{alertId}/history");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var history = await response.Content.ReadFromJsonAsync<List<AlertHistory>>();
            history.Should().NotBeEmpty();
            history.Should().Contain(h => h.Action == "Acknowledged");
        }

        [Fact]
        public async Task GetAlertComments_ShouldReturnComments_WhenAlertHasComments()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            // Add a comment
            var commentRequest = new
            {
                Comment = "This is a test comment",
                CommentType = "General",
                User = "TestUser"
            };

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            await _client.PostAsJsonAsync($"/api/Alerts/{alertId}/comments", commentRequest);

            // Act
            var response = await _client.GetAsync($"/api/Alerts/{alertId}/comments");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var comments = await response.Content.ReadFromJsonAsync<List<AlertComment>>();
            comments.Should().NotBeEmpty();
            comments.Should().Contain(c => c.Comment == "This is a test comment");
        }

        [Fact]
        public async Task AddAlertComment_ShouldAddComment_WhenValidData()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            var commentRequest = new
            {
                Comment = "Adding a new comment for testing",
                CommentType = "General",
                User = "TestUser"
            };

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PostAsJsonAsync($"/api/Alerts/{alertId}/comments", commentRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var comment = await response.Content.ReadFromJsonAsync<AlertComment>();
            comment.Should().NotBeNull();
            comment!.Comment.Should().Be("Adding a new comment for testing");
            comment.CreatedBy.Should().Be("TestUser");
        }

        [Fact]
        public async Task AddAlertComment_ShouldReturnBadRequest_WhenCommentIsEmpty()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            var invalidCommentRequest = new
            {
                Comment = "", // Empty comment
                CommentType = "General",
                User = "TestUser"
            };

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PostAsJsonAsync($"/api/Alerts/{alertId}/comments", invalidCommentRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetAlertStats_ShouldReturnStats()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            await CreateTestAlertAsync(token, alertRuleId, serverId);

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/Alerts/stats");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var stats = await response.Content.ReadFromJsonAsync<AlertStats>();
            stats.Should().NotBeNull();
            stats!.TotalAlerts.Should().BeGreaterThan(0);
            stats.ActiveAlerts.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task EvaluateAlertRules_ShouldCompleteSuccessfully()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PostAsync("/api/Alerts/evaluate", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var jsonDoc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
            var message = jsonDoc!.RootElement.GetProperty("message").GetString();
            message.Should().NotBeNull();
            message.Should().Be("Alert rules evaluation completed");
        }

        [Fact]
        public async Task DeleteAlert_ShouldMarkAsResolved_WhenAlertExists()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var alertRuleId = await CreateTestAlertRuleAsync(token);
            var serverId = await CreateTestServerAsync(token);
            var alertId = await CreateTestAlertAsync(token, alertRuleId, serverId);

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.DeleteAsync($"/api/Alerts/{alertId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var jsonDoc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
            var message = jsonDoc!.RootElement.GetProperty("message").GetString();
            message.Should().Be("Alert marked as resolved");
        }

        [Fact]
        public async Task DeleteAlert_ShouldReturnNotFound_WhenAlertDoesNotExist()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.DeleteAsync("/api/Alerts/99999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
