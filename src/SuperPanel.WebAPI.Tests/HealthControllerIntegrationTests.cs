using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SuperPanel.WebAPI.Controllers;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

/// <summary>
/// Integration tests for HealthController endpoints.
/// Tests the full HTTP pipeline for health check functionality.
/// </summary>
public class HealthControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task GetHealth_ShouldReturnHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        content.Should().NotBeNull();
        content!.Status.Should().Be("Healthy");
        content.Version.Should().Be("1.0.0");
        content.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        content.Environment.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSystemInfo_ShouldReturnSystemInformation()
    {
        // Act
        var response = await _client.GetAsync("/api/health/system");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<SystemInfoResponse>();
        content.Should().NotBeNull();
        content!.MachineName.Should().NotBeNullOrEmpty();
        content.OSVersion.Should().NotBeNullOrEmpty();
        content.ProcessorCount.Should().BeGreaterThan(0);
        content.WorkingSet.Should().BeGreaterThanOrEqualTo(0);
        content.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetMockServers_ShouldReturnMockServerData()
    {
        // Act
        var response = await _client.GetAsync("/api/health/mock-servers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<MockServerResponse[]>();
        content.Should().NotBeNull();
        content!.Length.Should().Be(3);

        // Verify first server
        var firstServer = content[0];
        firstServer.Id.Should().Be(1);
        firstServer.Name.Should().Be("Web Server 01");
    firstServer.IpAddress.Should().NotBeNullOrEmpty();
        firstServer.Port.Should().Be(80);
        firstServer.Status.Should().Be("Running");
        firstServer.OperatingSystem.Should().Be("Ubuntu 22.04 LTS");
        firstServer.TotalMemoryMB.Should().Be(8192);
        firstServer.AvailableMemoryMB.Should().Be(5400);
        firstServer.CpuUsagePercent.Should().Be(25.3);
        firstServer.DiskUsagePercent.Should().Be(42.1);
        firstServer.CreatedAt.Should().Be("2024-01-15T10:30:00Z");
        firstServer.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify second server
        var secondServer = content[1];
        secondServer.Id.Should().Be(2);
        secondServer.Name.Should().Be("Database Server");
    secondServer.IpAddress.Should().NotBeNullOrEmpty();
        secondServer.Port.Should().Be(3306);
        secondServer.Status.Should().Be("Running");

        // Verify third server (stopped)
        var thirdServer = content[2];
        thirdServer.Id.Should().Be(3);
        thirdServer.Name.Should().Be("Backup Server");
        thirdServer.Status.Should().Be("Stopped");
    }

    [Fact]
    public async Task GetMockDomains_ShouldReturnMockDomainData()
    {
        // Act
        var response = await _client.GetAsync("/api/health/mock-domains");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<MockDomainResponse[]>();
        content.Should().NotBeNull();
        content!.Length.Should().Be(3);

        // Verify first domain
        var firstDomain = content[0];
        firstDomain.Id.Should().Be(1);
        firstDomain.DomainName.Should().Be("example.com");
        firstDomain.ServerId.Should().Be(1);
        firstDomain.ServerName.Should().Be("Web Server 01");
        firstDomain.DocumentRoot.Should().Be("/var/www/example.com");
        firstDomain.IsActive.Should().BeTrue();
        firstDomain.SslEnabled.Should().BeTrue();
        firstDomain.CreatedAt.Should().Be("2024-01-15T11:00:00Z");
        firstDomain.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify second domain
        var secondDomain = content[1];
        secondDomain.Id.Should().Be(2);
        secondDomain.DomainName.Should().Be("test.org");
        secondDomain.IsActive.Should().BeTrue();
        secondDomain.SslEnabled.Should().BeFalse();

        // Verify third domain (inactive)
        var thirdDomain = content[2];
        thirdDomain.Id.Should().Be(3);
        thirdDomain.DomainName.Should().Be("demo.net");
        thirdDomain.IsActive.Should().BeFalse();
        thirdDomain.SslEnabled.Should().BeTrue();
    }

    // Response models for deserialization
    private class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
    }

    private class SystemInfoResponse
    {
        public string MachineName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long WorkingSet { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class MockServerResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Status { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public int TotalMemoryMB { get; set; }
        public int AvailableMemoryMB { get; set; }
        public double CpuUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }

    private class MockDomainResponse
    {
        public int Id { get; set; }
        public string DomainName { get; set; } = string.Empty;
        public int ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string DocumentRoot { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool SslEnabled { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }
}