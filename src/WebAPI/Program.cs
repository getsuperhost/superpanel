using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Services;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Hubs;
using System.Text;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Configure database based on environment
if (builder.Environment.IsEnvironment("Testing"))
{
    Console.WriteLine("Configuring for Testing environment");
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
    
    // Add JWT configuration for testing
    builder.Configuration["Jwt:Issuer"] = "SuperPanel";
    builder.Configuration["Jwt:Audience"] = "SuperPanelUsers";
    builder.Configuration["Jwt:Key"] = "SuperSecretKey1234567890123456789012345678901234567890";
}
else
{
    Console.WriteLine("Configuring for Production environment");
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Register all required services
builder.Services.AddScoped<IServerService, ServerService>();
builder.Services.AddScoped<IDomainService, DomainService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddScoped<ISystemMonitoringService, SystemMonitoringService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<ISslCertificateService, SslCertificateService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IDnsService, DnsService>();

// Add HttpClient for notifications
builder.Services.AddHttpClient();

// Add SignalR
builder.Services.AddSignalR();

// Add background services (skip in testing)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<ServerMonitoringService>();
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "SuperPanel Web Host Control Panel API", 
        Version = "v1",
        Description = "Comprehensive web hosting control panel API"
    });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// CORS - Updated to include HTTPS URLs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebUI",
        policy => policy
            .WithOrigins(
                "http://localhost:3000", 
                "https://localhost:3000", 
                "https://localhost:3001",
                "https://superpanel.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Configure HTTPS redirection only for local development scenarios
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
        options.HttpsPort = 5001;
    });
}

// Configure Data Protection based on environment
if (builder.Environment.IsEnvironment("Testing"))
{
    var testDataProtectionPath = Path.Combine(Path.GetTempPath(), "asp-keys-test");
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(testDataProtectionPath));
}
else
{
    var dataProtectionPath = builder.Configuration["DataProtection:Keys:Path"] ?? Path.Combine(Path.GetTempPath(), "asp-keys");
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
}

// Configure JWT authentication (use in both production and testing)
Console.WriteLine("Adding JWT authentication and authorization services");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SuperPanel",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SuperPanelUsers",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForTesting12345678901234567890"))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure database is created and seeded (skip in testing)
if (!builder.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        
        // Seed with sample data if database is empty
        // Ensure at least one Administrator user exists. If not, create one.
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        User? adminUser = null;
        if (!await dbContext.Users.AnyAsync(u => u.Role == "Administrator"))
        {
            var adminPassword = builder.Configuration["Seed:AdminPassword"] ?? "Admin123!";
            var adminUsername = builder.Configuration["Seed:AdminUsername"] ?? "admin";
            var adminEmail = builder.Configuration["Seed:AdminEmail"] ?? "admin@localhost";

            // Avoid username/email collisions by appending a short suffix if necessary
            if (await dbContext.Users.AnyAsync(u => u.Username == adminUsername))
            {
                adminUsername = adminUsername + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            }

            if (await dbContext.Users.AnyAsync(u => u.Email == adminEmail))
            {
                adminEmail = "admin+" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@localhost";
            }

            try
            {
                adminUser = await authService.RegisterAsync(adminUsername, adminEmail, adminPassword, "Administrator");
                Console.WriteLine($"Seeded admin user: {adminUser.Username} (id={adminUser.Id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to seed admin user: {ex.Message}");
            }
        }
        else
        {
            adminUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Role == "Administrator");
        }

        if (!await dbContext.Servers.AnyAsync())
        {
            if (adminUser == null)
            {
                Console.WriteLine("Cannot seed servers: no admin user found");
            }
            else
            {
                var sampleServers = new List<Server>
                {
                    new Server
                    {
                        Name = "WebServer-01",
                        IpAddress = "192.168.1.100",
                        Description = "Primary web server",
                        OperatingSystem = "Ubuntu 22.04 LTS",
                        Status = ServerStatus.Online,
                        CpuUsage = 45.2,
                        MemoryUsage = 68.7,
                        DiskUsage = 72.1,
                        UserId = adminUser.Id,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Server
                    {
                        Name = "Database-01",
                        IpAddress = "192.168.1.101",
                        Description = "Database server",
                        OperatingSystem = "Ubuntu 22.04 LTS",
                        Status = ServerStatus.Online,
                        CpuUsage = 23.8,
                        MemoryUsage = 54.3,
                        DiskUsage = 45.6,
                        UserId = adminUser.Id,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Server
                    {
                        Name = "MailServer-01",
                        IpAddress = "192.168.1.102",
                        Description = "Email server",
                        OperatingSystem = "Ubuntu 22.04 LTS",
                        Status = ServerStatus.Maintenance,
                        CpuUsage = 12.4,
                        MemoryUsage = 34.2,
                        DiskUsage = 28.9,
                        UserId = adminUser.Id,
                        CreatedAt = DateTime.UtcNow
                    }
                };
                
                await dbContext.Servers.AddRangeAsync(sampleServers);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}
app.UseCors("AllowWebUI");

// Authentication middleware - always add for both production and testing
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
Console.WriteLine("Adding authentication and authorization middleware");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR hubs
app.MapHub<MonitoringHub>("/hubs/monitoring");

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }