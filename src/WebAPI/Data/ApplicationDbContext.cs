using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Server> Servers { get; set; }
    public DbSet<Domain> Domains { get; set; }
    public DbSet<Subdomain> Subdomains { get; set; }
    public DbSet<Database> Databases { get; set; }
    public DbSet<DatabaseUser> DatabaseUsers { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<SslCertificate> SslCertificates { get; set; }
    public DbSet<EmailAccount> EmailAccounts { get; set; }
    public DbSet<EmailForwarder> EmailForwarders { get; set; }
    public DbSet<EmailAlias> EmailAliases { get; set; }
    public DbSet<Backup> Backups { get; set; }
    public DbSet<BackupLog> BackupLogs { get; set; }
    public DbSet<BackupSchedule> BackupSchedules { get; set; }
    public DbSet<AlertRule> AlertRules { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<AlertHistory> AlertHistories { get; set; }
    public DbSet<AlertComment> AlertComments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Server configuration
        modelBuilder.Entity<Server>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.IpAddress).IsUnique();
            
            entity.HasOne(s => s.User)
                  .WithMany()
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Domain configuration
        modelBuilder.Entity<Domain>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Name).IsUnique();
            
            entity.HasOne(d => d.User)
                  .WithMany()
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(d => d.Server)
                  .WithMany(s => s.Domains)
                  .HasForeignKey(d => d.ServerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Subdomain configuration
        modelBuilder.Entity<Subdomain>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            
            entity.HasOne(s => s.Domain)
                  .WithMany(d => d.Subdomains)
                  .HasForeignKey(s => s.DomainId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Database configuration
        modelBuilder.Entity<Database>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            
            entity.HasOne(db => db.User)
                  .WithMany()
                  .HasForeignKey(db => db.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(db => db.Server)
                  .WithMany(s => s.Databases)
                  .HasForeignKey(db => db.ServerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // DatabaseUser configuration
        modelBuilder.Entity<DatabaseUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            
            entity.HasOne(u => u.Database)
                  .WithMany(db => db.Users)
                  .HasForeignKey(u => u.DatabaseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.PasswordSalt).IsRequired();
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // SSL Certificate configuration
        modelBuilder.Entity<SslCertificate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DomainName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CertificatePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.PrivateKeyPath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ChainPath).HasMaxLength(1000);
            entity.Property(e => e.Issuer).HasMaxLength(500);
            entity.Property(e => e.ValidationMethod).HasMaxLength(1000);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.HasIndex(e => e.DomainName).IsUnique();
            
            entity.HasOne(c => c.User)
                  .WithMany()
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(c => c.Domain)
                  .WithMany()
                  .HasForeignKey(c => c.DomainId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Email Account configuration
        modelBuilder.Entity<EmailAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmailAddress).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.HasIndex(e => e.EmailAddress).IsUnique();
            
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(e => e.Domain)
                  .WithMany()
                  .HasForeignKey(e => e.DomainId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Email Forwarder configuration
        modelBuilder.Entity<EmailForwarder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ForwardTo).IsRequired().HasMaxLength(255);
            
            entity.HasOne(f => f.EmailAccount)
                  .WithMany(e => e.Forwarders)
                  .HasForeignKey(f => f.EmailAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Email Alias configuration
        modelBuilder.Entity<EmailAlias>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AliasAddress).IsRequired().HasMaxLength(255);
            
            entity.HasOne(a => a.EmailAccount)
                  .WithMany(e => e.Aliases)
                  .HasForeignKey(a => a.EmailAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Backup configuration
        modelBuilder.Entity<Backup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.BackupPath).HasMaxLength(500);
            entity.Property(e => e.EncryptionKey).HasMaxLength(100);
            entity.Property(e => e.ScheduleCron).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            
            entity.HasOne(b => b.Server)
                  .WithMany()
                  .HasForeignKey(b => b.ServerId)
                  .OnDelete(DeleteBehavior.NoAction);
                  
            entity.HasOne(b => b.Database)
                  .WithMany()
                  .HasForeignKey(b => b.DatabaseId)
                  .OnDelete(DeleteBehavior.NoAction);
                  
            entity.HasOne(b => b.Domain)
                  .WithMany()
                  .HasForeignKey(b => b.DomainId)
                  .OnDelete(DeleteBehavior.NoAction);
                  
            entity.HasOne(b => b.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(b => b.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // BackupLog configuration
        modelBuilder.Entity<BackupLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Level).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Details).HasMaxLength(1000);
            
            entity.HasOne(l => l.Backup)
                  .WithMany(b => b.Logs)
                  .HasForeignKey(l => l.BackupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // BackupSchedule configuration
        modelBuilder.Entity<BackupSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CronExpression).IsRequired().HasMaxLength(100);
            
            entity.HasOne(s => s.Server)
                  .WithMany()
                  .HasForeignKey(s => s.ServerId)
                  .OnDelete(DeleteBehavior.NoAction);
                  
            entity.HasOne(s => s.Database)
                  .WithMany()
                  .HasForeignKey(s => s.DatabaseId)
                  .OnDelete(DeleteBehavior.NoAction);
                  
            entity.HasOne(s => s.Domain)
                  .WithMany()
                  .HasForeignKey(s => s.DomainId)
                  .OnDelete(DeleteBehavior.NoAction);
                  
            entity.HasOne(s => s.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(s => s.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AlertRule configuration
        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.MetricName).HasMaxLength(50);
            entity.Property(e => e.Condition).IsRequired().HasMaxLength(2);
            entity.Property(e => e.WebhookUrl).HasMaxLength(500);
            entity.Property(e => e.EmailRecipients).HasMaxLength(1000);
            entity.Property(e => e.SlackWebhookUrl).HasMaxLength(500);
            
            entity.HasOne(r => r.User)
                  .WithMany()
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(r => r.Server)
                  .WithMany()
                  .HasForeignKey(r => r.ServerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Alert configuration
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.MetricName).HasMaxLength(50);
            entity.Property(e => e.ContextData).HasMaxLength(2000);
            
            entity.HasOne(a => a.AlertRule)
                  .WithMany(r => r.Alerts)
                  .HasForeignKey(a => a.AlertRuleId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(a => a.Server)
                  .WithMany()
                  .HasForeignKey(a => a.ServerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AlertHistory configuration
        modelBuilder.Entity<AlertHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PerformedBy).IsRequired().HasMaxLength(100);
            
            entity.HasOne(h => h.Alert)
                  .WithMany(a => a.AlertHistories)
                  .HasForeignKey(h => h.AlertId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AlertComment configuration
        modelBuilder.Entity<AlertComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Comment).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CommentType).HasMaxLength(50);
            
            entity.HasOne(c => c.Alert)
                  .WithMany(a => a.AlertComments)
                  .HasForeignKey(c => c.AlertId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}