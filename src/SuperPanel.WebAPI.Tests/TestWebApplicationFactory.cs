using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IO;
using System.Linq;
using System.Text;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;

namespace SuperPanel.WebAPI.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // Keep-alive connection to a named in-memory SQLite DB so EF-created connections can share the same DB.
    private readonly SqliteConnection _keepAliveConnection;
    private readonly string _connectionString;

    public TestWebApplicationFactory()
    {
        // Use a named in-memory database so multiple independent connections can access the same DB.
        _connectionString = $"Data Source=file:memdb_{Guid.NewGuid()}?mode=memory&cache=shared";
        // Open a persistent connection to keep the in-memory DB alive for the factory lifetime.
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove any existing DbContext related registrations so tests can control the database
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbContextFactoryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>));
            if (dbContextFactoryDescriptor != null)
            {
                services.Remove(dbContextFactoryDescriptor);
            }

            var dbContextServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextServiceDescriptor != null)
            {
                services.Remove(dbContextServiceDescriptor);
            }

            // Register DbContextFactory and DbContext to use the named in-memory database (EF will open its own connections).
            services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
            {
                options.UseSqlite(_connectionString);
            });

            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseSqlite(_connectionString);
            });

            // Build a temporary provider to ensure the database schema is created before tests run
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Ensure the database is created (applies EF model to sqlite file)
            db.Database.EnsureCreated();

            // Seed a test user to satisfy foreign key constraints in integration tests.
            // Tests generate JWTs with userId = 1 by default, so ensure a user with Id = 1 exists.
            if (!db.Users.Any(u => u.Id == 1))
            {
                var testUser = new User
                {
                    Id = 1,
                    Username = "testuser",
                    Email = "testuser@example.com",
                    PasswordHash = "test-hash",
                    PasswordSalt = "test-salt",
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                db.Users.Add(testUser);
                db.SaveChanges();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // Close and dispose the in-memory SqliteConnection
            try
            {
                    _keepAliveConnection?.Close();
                    _keepAliveConnection?.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
    }
}