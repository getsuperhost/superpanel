using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SuperPanel.WebAPI.Controllers;
using SuperPanel.WebAPI.Models;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

/// <summary>
/// Integration tests for AlertRulesController endpoints.
/// Tests the full HTTP pipeline for alert rule CRUD operations.
/// </summary>
public class AlertRulesControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string _authToken = string.Empty;
    private int _testUserId;

    public AlertRulesControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();

        // Setup: Register and login a test user
        SetupTestUserAsync().Wait();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task SetupTestUserAsync()
    {
        // Register a test user with unique identifier
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        var registerRequest = new RegisterRequest
        {
            Username = $"alertrulesuser_{uniqueId}",
            Email = $"alertrules_{uniqueId}@example.com",
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

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loginContent.Should().NotBeNull();
        _authToken = loginContent!.Token;
        _testUserId = loginContent.User.Id;

        // Set authorization header for subsequent requests
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
    }

    [Fact]
    public async Task GetAlertRules_ShouldReturnEmptyListInitially()
    {
        // Act
        var response = await _client.GetAsync("/api/alertrules");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<AlertRule[]>();
        content.Should().NotBeNull();
        content!.Length.Should().Be(0);
    }

        [Fact]
        public async Task CreateAlertRule_ShouldReturnCreated()
        {
            // Arrange
            var newRule = new
            {
                Name = "Test Alert Rule",
                Description = "A test alert rule",
                Type = 2, // HighCpuUsage as int
                UserId = _testUserId, // Use the test user ID
                ServerId = (int?)null, // Explicitly set to null
                MetricName = (string)null, // Explicitly set to null
                Condition = "gt", // Use 2-character condition
                Threshold = 80.0,
                Severity = 2, // Warning as int
                Enabled = true,
                CooldownMinutes = 5,
                WebhookUrl = (string)null, // Explicitly set to null
                EmailRecipients = (string)null, // Explicitly set to null
                SlackWebhookUrl = (string)null, // Explicitly set to null
                User = (object)null, // Explicitly set navigation property to null
                Server = (object)null // Explicitly set navigation property to null
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/AlertRules", newRule);

            // Debug: Check response content if BadRequest
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"BadRequest response: {errorContent}");
            }

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var createdRule = await response.Content.ReadFromJsonAsync<AlertRule>();
            createdRule.Should().NotBeNull();
            createdRule!.Id.Should().BeGreaterThan(0);
            createdRule!.Name.Should().Be("Test Alert Rule");
            createdRule!.UserId.Should().Be(_testUserId);
        }    [Fact]
    public async Task GetAlertRule_WithValidId_ShouldReturnAlertRule()
    {
        // Arrange - Create an alert rule first
        var alertRule = new AlertRule
        {
            Name = "Get Test Alert",
            Description = "Test alert for get operation",
            Type = AlertRuleType.HighMemoryUsage,
            UserId = _testUserId,
            MetricName = "Memory",
            Condition = "gt",
            Threshold = 90.0,
            Severity = AlertRuleSeverity.Error,
            Enabled = true,
            CooldownMinutes = 10
        };

        var createResponse = await _client.PostAsJsonAsync("/api/alertrules", alertRule);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdRule = await createResponse.Content.ReadFromJsonAsync<AlertRule>();
        var ruleId = createdRule!.Id;

        // Act
        var response = await _client.GetAsync($"/api/alertrules/{ruleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<AlertRule>();
        content.Should().NotBeNull();
        content!.Id.Should().Be(ruleId);
        content.Name.Should().Be("Get Test Alert");
        content.Type.Should().Be(AlertRuleType.HighMemoryUsage);
        content.UserId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task GetAlertRule_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/alertrules/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

        [Fact]
        public async Task UpdateAlertRule_ShouldReturnOk()
        {
            // Arrange
            var newRule = new AlertRule
            {
                Name = "Original Rule",
                Type = AlertRuleType.HighCpuUsage,
                UserId = 1,
                Condition = "gt",
                Threshold = 80.0,
                Severity = AlertRuleSeverity.Warning,
                Enabled = true,
                CooldownMinutes = 5
            };
            var createResponse = await _client.PostAsJsonAsync("/api/AlertRules", newRule);
            var createdRule = await createResponse.Content.ReadFromJsonAsync<AlertRule>();

            var updateRule = new AlertRule
            {
                Name = "Updated Rule",
                Type = AlertRuleType.ServerDown,
                UserId = 1,
                Condition = "eq",
                Threshold = 90.0,
                Severity = AlertRuleSeverity.Critical,
                Enabled = false,
                CooldownMinutes = 10
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/AlertRules/{createdRule!.Id}", updateRule);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var updatedRule = await response.Content.ReadFromJsonAsync<AlertRule>();
            updatedRule.Should().NotBeNull();
            updatedRule!.Name.Should().Be("Updated Rule");
            updatedRule!.Type.Should().Be(AlertRuleType.ServerDown);
            updatedRule!.Condition.Should().Be("eq");
            updatedRule!.Threshold.Should().Be(90.0);
            updatedRule!.Severity.Should().Be(AlertRuleSeverity.Critical);
            updatedRule!.Enabled.Should().BeFalse();
            updatedRule!.CooldownMinutes.Should().Be(10);
        }    [Fact]
    public async Task DeleteAlertRule_WithValidId_ShouldReturnNoContent()
    {
        // Arrange - Create an alert rule first
        var alertRule = new AlertRule
        {
            Name = "Delete Test Alert",
            Description = "Test alert for deletion",
            Type = AlertRuleType.ServiceUnavailable,
            UserId = _testUserId,
            MetricName = "Service",
            Condition = "eq",
            Threshold = 0.0,
            Severity = AlertRuleSeverity.Critical,
            Enabled = true,
            CooldownMinutes = 1
        };

        var createResponse = await _client.PostAsJsonAsync("/api/alertrules", alertRule);
        var createdRule = await createResponse.Content.ReadFromJsonAsync<AlertRule>();
        var ruleId = createdRule!.Id;

        // Act
        var response = await _client.DeleteAsync($"/api/alertrules/{ruleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's actually deleted
        var getResponse = await _client.GetAsync($"/api/alertrules/{ruleId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTestAlertRule_ShouldReturnOk()
    {
        // Act (no auth required for this endpoint)
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.PostAsync("/api/AlertRules/test-create", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<TestCreateResponse>();
        content.Should().NotBeNull();
        content!.Message.Should().Be("Test alert rule created successfully");
        content.AlertRuleId.Should().BeGreaterThan(0);
        content.TestUrl.Should().Contain($"/api/alertrules/{content.AlertRuleId}/test");
    }

    [Fact]
    public async Task TestAlertRule_WithValidId_ShouldReturnOk()
    {
        // Arrange - Create a test alert rule first
        var unauthClient = _factory.CreateClient();
        var createResponse = await unauthClient.PostAsync("/api/AlertRules/test-create", null);
        var createContent = await createResponse.Content.ReadFromJsonAsync<TestCreateResponse>();
        var ruleId = createContent!.AlertRuleId;

        // Act (no auth required for this endpoint)
        var response = await unauthClient.PostAsync($"/api/AlertRules/{ruleId}/test", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<TestResponse>();
        content.Should().NotBeNull();
        content!.Message.Should().Be("Test alert created and notifications sent successfully");
        content.AlertRuleId.Should().Be(ruleId);
    }

    [Fact]
    public async Task TestAlertRule_WithInvalidId_ShouldReturnNotFound()
    {
        // Act (no auth required for this endpoint)
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.PostAsync("/api/alertrules/99999/test", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Request/Response models for deserialization
    private class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    private class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    private class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public UserResponse User { get; set; } = new();
    }

    private class UserResponse
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    private class AlertRule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AlertRuleType Type { get; set; }
        public int UserId { get; set; }
        public int? ServerId { get; set; }
        public string MetricName { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public AlertRuleSeverity Severity { get; set; }
        public bool Enabled { get; set; }
        public int CooldownMinutes { get; set; }
        public string WebhookUrl { get; set; } = string.Empty;
        public string EmailRecipients { get; set; } = string.Empty;
        public string SlackWebhookUrl { get; set; } = string.Empty;
    }

    private class TestCreateResponse
    {
        public string Message { get; set; } = string.Empty;
        public int AlertRuleId { get; set; }
        public string TestUrl { get; set; } = string.Empty;
    }

    private class TestResponse
    {
        public string Message { get; set; } = string.Empty;
        public int AlertRuleId { get; set; }
    }
}