using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public class BackupService : IBackupService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<BackupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly string _backupPath;

    public BackupService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<BackupService> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        
        _backupPath = _environment.IsEnvironment("Testing") 
            ? Path.Combine("/tmp/", "SuperPanel", "backups")
            : "/var/backups/superpanel";
        
        _logger.LogInformation("BackupService created with environment: {Environment}", _environment.EnvironmentName);
    }

    public async Task<Backup> CreateBackupAsync(BackupRequest request, int userId)
    {
        var backup = new Backup
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            ServerId = request.ServerId,
            DatabaseId = request.DatabaseId,
            DomainId = request.DomainId,
            BackupPath = request.BackupPath,
            IsCompressed = request.IsCompressed,
            IsEncrypted = request.IsEncrypted,
            RetentionDays = request.RetentionDays,
            CreatedByUserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(request.RetentionDays)
        };

            await using var context = await _dbContextFactory.CreateDbContextAsync();
            _logger.LogInformation("Created DbContext for CreateBackupAsync. Provider: {provider}; ContextHash: {hash}", context.Database.ProviderName, context.GetHashCode());
        context.Backups.Add(backup);
        await context.SaveChangesAsync();

        // Start the backup process asynchronously using a fresh DbContext
        _ = Task.Run(() => ExecuteBackupAsync(backup.Id));

        return backup;
    }

    public async Task<Backup?> GetBackupAsync(int id)
    {
          await using var context = await _dbContextFactory.CreateDbContextAsync();
          _logger.LogInformation("Created DbContext for GetBackupAsync. Provider: {provider}; ContextHash: {hash}; BackupsCount: {count}", context.Database.ProviderName, context.GetHashCode(), await context.Backups.CountAsync());
        return await context.Backups
            .Include(b => b.Server)
            .Include(b => b.Database)
            .Include(b => b.Domain)
            .Include(b => b.CreatedByUser)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IEnumerable<Backup>> GetBackupsAsync(int? serverId = null, int? databaseId = null, int? domainId = null)
    {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            _logger.LogInformation("Created DbContext for GetBackupsAsync. Provider: {provider}; ContextHash: {hash}; BackupsCount: {count}", context.Database.ProviderName, context.GetHashCode(), await context.Backups.CountAsync());
        var query = context.Backups
            .Include(b => b.Server)
            .Include(b => b.Database)
            .Include(b => b.Domain)
            .Include(b => b.CreatedByUser)
            .AsQueryable();

        if (serverId.HasValue)
            query = query.Where(b => b.ServerId == serverId);
        if (databaseId.HasValue)
            query = query.Where(b => b.DatabaseId == databaseId);
        if (domainId.HasValue)
            query = query.Where(b => b.DomainId == domainId);

        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
    }

    public async Task<bool> DeleteBackupAsync(int id)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var backup = await context.Backups.FindAsync(id);
        if (backup == null)
            return false;

        // Delete the physical file if it exists
        if (File.Exists(backup.FilePath))
        {
            try
            {
                File.Delete(backup.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to delete backup file {backup.FilePath}: {ex.Message}");
            }
        }

        context.Backups.Remove(backup);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<RestoreResult> RestoreBackupAsync(int backupId, RestoreRequest request)
    {
        var backup = await GetBackupAsync(backupId);
        if (backup == null)
            throw new ArgumentException("Backup not found");

        if (backup.Status != BackupStatus.Completed)
            throw new InvalidOperationException("Backup is not in completed state");

        // Update status using a fresh context
        await using (var context = await _dbContextFactory.CreateDbContextAsync())
        {
            var toUpdate = await context.Backups.FindAsync(backupId);
            if (toUpdate == null) throw new ArgumentException("Backup not found");
            toUpdate.Status = BackupStatus.Running;
            toUpdate.StartedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        try
        {
            var result = await ExecuteRestoreAsync(backup, request);
            // mark completed
            await using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                var toUpdate = await context.Backups.FindAsync(backupId);
                if (toUpdate != null)
                {
                    toUpdate.Status = BackupStatus.Completed;
                    toUpdate.CompletedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            await using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                var toUpdate = await context.Backups.FindAsync(backupId);
                if (toUpdate != null)
                {
                    toUpdate.Status = BackupStatus.Failed;
                    toUpdate.ErrorMessage = ex.Message;
                    toUpdate.CompletedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }
            }
            throw;
        }
    }

    private async Task ExecuteBackupAsync(int backupId)
    {
        // Use a fresh DbContext for background execution to avoid concurrent usage of a single DbContext
        var context = await _dbContextFactory.CreateDbContextAsync();
        _logger.LogInformation("Created DbContext for ExecuteBackupAsync. Provider: {provider}; ContextHash: {hash}; BackupsCountBefore: {count}", context.Database.ProviderName, context.GetHashCode(), await context.Backups.CountAsync());
        var backup = await context.Backups.FindAsync(backupId);
        if (backup == null) 
        {
            context.Dispose();
            return;
        }

        try
        {
            backup.Status = BackupStatus.Running;
            backup.StartedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            context.Dispose();

            await LogBackupAsync(backupId, "Info", "Starting backup process");

            // Use system temp directory for both testing and production to ensure write permissions
            string tempBasePath = Path.GetTempPath();
            _logger.LogInformation("Using tempBasePath: {TempBasePath}, IsTesting: {IsTesting}", tempBasePath, _environment.IsEnvironment("Testing"));
            string superPanelTempDir = Path.Combine(tempBasePath, "SuperPanel");
            Directory.CreateDirectory(superPanelTempDir);
            string tempPath = Path.Combine(superPanelTempDir, Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string finalPath = GenerateBackupFilePath(backup);

            // Execute backup based on type
            switch (backup.Type)
            {
                case BackupType.Database:
                    await BackupDatabaseAsync(backup, tempPath);
                    break;
                case BackupType.Files:
                    await BackupFilesAsync(backup, tempPath);
                    break;
                case BackupType.FullServer:
                    await BackupFullServerAsync(backup, tempPath);
                    break;
                case BackupType.Website:
                    await BackupWebsiteAsync(backup, tempPath);
                    break;
                case BackupType.Email:
                    await BackupEmailAsync(backup, tempPath);
                    break;
                default:
                    throw new NotSupportedException($"Backup type {backup.Type} is not supported");
            }

            // Compress if requested
            if (backup.IsCompressed)
            {
                await LogBackupAsync(backupId, "Info", "Compressing backup");
                string compressedPath = tempPath + ".zip";

                // Ensure the temp directory has proper permissions for reading
                var tempDirInfo = new DirectoryInfo(tempPath);
                tempDirInfo.Attributes &= ~FileAttributes.ReadOnly; // Remove read-only if set

                ZipFile.CreateFromDirectory(tempPath, compressedPath);
                Directory.Delete(tempPath, true);
                tempPath = compressedPath;
            }

            // Encrypt if requested
            if (backup.IsEncrypted)
            {
                await LogBackupAsync(backupId, "Info", "Encrypting backup");
                string encryptedPath = tempPath + ".enc";
                await EncryptFileAsync(tempPath, encryptedPath);
                File.Delete(tempPath);
                tempPath = encryptedPath;
            }

            // Move to final location
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            // Ensure final path doesn't exist (cleanup from previous test runs)
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            else if (Directory.Exists(finalPath))
            {
                Directory.Delete(finalPath, true);
            }
            if (File.Exists(tempPath))
            {
                File.Move(tempPath, finalPath);
            }
            else if (Directory.Exists(tempPath))
            {
                CopyDirectory(tempPath, finalPath);
                Directory.Delete(tempPath, true);
            }

            var fileInfo = new FileInfo(finalPath);
            var finalContext = await _dbContextFactory.CreateDbContextAsync();
            var backupToUpdate = await finalContext.Backups.FindAsync(backupId);
            if (backupToUpdate != null)
            {
                backupToUpdate.FilePath = finalPath;
                backupToUpdate.FileSizeInBytes = fileInfo.Length;
                backupToUpdate.Status = BackupStatus.Completed;
                backupToUpdate.CompletedAt = DateTime.UtcNow;
                await finalContext.SaveChangesAsync();
            }
            finalContext.Dispose();
            await LogBackupAsync(backupId, "Info", $"Backup completed successfully. Size: {fileInfo.Length} bytes");
        }
        catch (Exception ex)
        {
            var failedContext = await _dbContextFactory.CreateDbContextAsync();
            var backupToFail = await failedContext.Backups.FindAsync(backupId);
            if (backupToFail != null)
            {
                backupToFail.Status = BackupStatus.Failed;
                backupToFail.ErrorMessage = ex.Message;
                backupToFail.CompletedAt = DateTime.UtcNow;
                await failedContext.SaveChangesAsync();
            }
            failedContext.Dispose();
            await LogBackupAsync(backupId, "Error", $"Backup failed: {ex.Message}");
        }
    }

    private async Task<RestoreResult> ExecuteRestoreAsync(Backup backup, RestoreRequest request)
    {
        await LogBackupAsync(backup.Id, "Info", "Starting restore process");

        string extractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        long bytesRestored = 0;

        try
        {
            // Diagnostic: log the file path and whether it exists before attempting to decompress
            try
            {
                var exists = File.Exists(backup.FilePath);
                string details;
                if (exists)
                {
                    var fi = new FileInfo(backup.FilePath);
                    details = $"Exists: true; Size: {fi.Length} bytes; LastWrite: {fi.LastWriteTimeUtc:O}; BackupStatus: {backup.Status}";
                }
                else
                {
                    details = "Exists: false";
                }
                await LogBackupAsync(backup.Id, "Info", $"Restore target file: {backup.FilePath}; {details}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log restore file existence for backup {BackupId}", backup.Id);
            }

            // Decrypt if needed
            if (backup.IsEncrypted && File.Exists(backup.FilePath))
            {
                await LogBackupAsync(backup.Id, "Info", "Decrypting backup");
                string decryptedPath = Path.Combine(extractPath, "decrypted");
                await DecryptFileAsync(backup.FilePath, decryptedPath);
                // Replace the encrypted file path with decrypted path for further processing
                backup.FilePath = decryptedPath;
            }

            // Decompress if needed
            if (backup.IsCompressed && File.Exists(backup.FilePath))
            {
                await LogBackupAsync(backup.Id, "Info", "Decompressing backup");
                ZipFile.ExtractToDirectory(backup.FilePath, extractPath);
            }
            else if (Directory.Exists(backup.FilePath))
            {
                CopyDirectory(backup.FilePath, extractPath);
            }

            // Execute restore based on type
            switch (backup.Type)
            {
                case BackupType.Database:
                    bytesRestored = await RestoreDatabaseAsync(backup, extractPath, request);
                    break;
                case BackupType.Files:
                    bytesRestored = await RestoreFilesAsync(backup, extractPath, request);
                    break;
                case BackupType.FullServer:
                    bytesRestored = await RestoreFullServerAsync(backup, extractPath, request);
                    break;
                case BackupType.Website:
                    bytesRestored = await RestoreWebsiteAsync(backup, extractPath, request);
                    break;
                case BackupType.Email:
                    bytesRestored = await RestoreEmailAsync(backup, extractPath, request);
                    break;
                default:
                    throw new NotSupportedException($"Restore type {backup.Type} is not supported");
            }

            await LogBackupAsync(backup.Id, "Info", "Restore completed successfully");
            return new RestoreResult
            {
                Success = true,
                Message = "Restore completed successfully",
                BytesRestored = bytesRestored
            };
        }
        catch (Exception ex)
        {
            await LogBackupAsync(backup.Id, "Error", $"Restore failed: {ex.Message}");
            return new RestoreResult
            {
                Success = false,
                Message = $"Restore failed: {ex.Message}",
                BytesRestored = 0
            };
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
        }
    }

    private async Task BackupDatabaseAsync(Backup backup, string outputPath)
    {
        if (!backup.DatabaseId.HasValue)
            throw new InvalidOperationException("Database ID is required for database backup");

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var database = await context.Databases.FindAsync(backup.DatabaseId.Value);
        if (database == null)
            throw new InvalidOperationException("Database not found");

        await LogBackupAsync(backup.Id, "Info", $"Backing up database: {database.Name}");

        // This is a simplified implementation - in a real system you'd use database-specific tools
        string backupFile = Path.Combine(outputPath, $"{database.Name}_backup.sql");

        // For demonstration, create a simple SQL dump
        var sql = new StringBuilder();
        sql.AppendLine($"-- Backup of database: {database.Name}");
        sql.AppendLine($"-- Created: {DateTime.UtcNow}");
        sql.AppendLine();

        // Add database schema and data (simplified)
        sql.AppendLine($"CREATE DATABASE IF NOT EXISTS `{database.Name}`;");
        sql.AppendLine($"USE `{database.Name}`;");

        await File.WriteAllTextAsync(backupFile, sql.ToString());
    }

    private async Task BackupFilesAsync(Backup backup, string outputPath)
    {
        if (string.IsNullOrEmpty(backup.BackupPath))
            throw new InvalidOperationException("Backup path is required for file backup");

        await LogBackupAsync(backup.Id, "Info", $"Backing up files from: {backup.BackupPath}");

        if (Directory.Exists(backup.BackupPath))
        {
            CopyDirectory(backup.BackupPath, outputPath);
        }
        else
        {
            throw new DirectoryNotFoundException($"Backup path does not exist: {backup.BackupPath}");
        }
    }

    private async Task BackupFullServerAsync(Backup backup, string outputPath)
    {
        if (!backup.ServerId.HasValue)
            throw new InvalidOperationException("Server ID is required for full server backup");
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var server = await context.Servers.FindAsync(backup.ServerId.Value);
        if (server == null)
            throw new InvalidOperationException("Server not found");

        await LogBackupAsync(backup.Id, "Info", $"Creating full backup of server: {server.Name}");

        // Backup all server data (simplified implementation)
        var serverBackupPath = Path.Combine(outputPath, "server_backup");
        Directory.CreateDirectory(serverBackupPath);

        // Backup databases
        var databases = await context.Databases.Where(d => d.ServerId == server.Id).ToListAsync();
        foreach (var db in databases)
        {
            await LogBackupAsync(backup.Id, "Info", $"Backing up database: {db.Name}");
            // Database backup logic here
        }

        // Backup websites
        var domains = await context.Domains.Where(d => d.ServerId == server.Id).ToListAsync();
        foreach (var domain in domains)
        {
            await LogBackupAsync(backup.Id, "Info", $"Backing up website: {domain.Name}");
            // Website backup logic here
        }
    }

    private async Task BackupWebsiteAsync(Backup backup, string outputPath)
    {
        if (!backup.DomainId.HasValue)
            throw new InvalidOperationException("Domain ID is required for website backup");

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var domain = await context.Domains.FindAsync(backup.DomainId.Value);
        if (domain == null)
            throw new InvalidOperationException("Domain not found");

        await LogBackupAsync(backup.Id, "Info", $"Backing up website: {domain.Name}");

        // Website backup logic (simplified)
        string websitePath = $"/var/www/{domain.Name}";
        if (Directory.Exists(websitePath))
        {
            CopyDirectory(websitePath, outputPath);
        }
    }

    private async Task BackupEmailAsync(Backup backup, string outputPath)
    {
        if (!backup.DomainId.HasValue)
            throw new InvalidOperationException("Domain ID is required for email backup");
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var domain = await context.Domains.FindAsync(backup.DomainId.Value);
        if (domain == null)
            throw new InvalidOperationException("Domain not found");

        await LogBackupAsync(backup.Id, "Info", $"Backing up email for domain: {domain.Name}");

        // Email backup logic (simplified)
        var emailAccounts = await context.EmailAccounts.Where(e => e.DomainId == domain.Id).ToListAsync();
        var emailData = new StringBuilder();

        foreach (var account in emailAccounts)
        {
            emailData.AppendLine($"Account: {account.EmailAddress}");
            // Add email data export logic here
        }

        await File.WriteAllTextAsync(Path.Combine(outputPath, "email_backup.txt"), emailData.ToString());
    }

    private async Task<long> RestoreDatabaseAsync(Backup backup, string extractPath, RestoreRequest request)
    {
        // Database restore logic
        await LogBackupAsync(backup.Id, "Info", "Restoring database");
        // Implementation would depend on database type
        return 0; // Placeholder - would return actual bytes restored
    }

    private async Task<long> RestoreFilesAsync(Backup backup, string extractPath, RestoreRequest request)
    {
        // File restore logic
        await LogBackupAsync(backup.Id, "Info", "Restoring files");
        if (Directory.Exists(extractPath) && !string.IsNullOrEmpty(request.RestorePath))
        {
            CopyDirectory(extractPath, request.RestorePath);
            // Calculate bytes restored
            return Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        return 0;
    }

    private async Task<long> RestoreFullServerAsync(Backup backup, string extractPath, RestoreRequest request)
    {
        // Full server restore logic
        await LogBackupAsync(backup.Id, "Info", "Restoring full server");
        // Complex restore logic for complete server restoration
        return 0; // Placeholder - would return actual bytes restored
    }

    private async Task<long> RestoreWebsiteAsync(Backup backup, string extractPath, RestoreRequest request)
    {
        // Website restore logic
        await LogBackupAsync(backup.Id, "Info", "Restoring website");
        if (!string.IsNullOrEmpty(request.RestorePath))
        {
            CopyDirectory(extractPath, request.RestorePath);
            // Calculate bytes restored
            return Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        return 0;
    }

    private async Task<long> RestoreEmailAsync(Backup backup, string extractPath, RestoreRequest request)
    {
        // Email restore logic
        await LogBackupAsync(backup.Id, "Info", "Restoring email data");
        // Email restoration logic
        return 0; // Placeholder - would return actual bytes restored
    }

    private async Task LogBackupAsync(int backupId, string level, string message, string? details = null)
    {
        var log = new BackupLog
        {
            BackupId = backupId,
            Level = level,
            Message = message,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        context.BackupLogs.Add(log);
        await context.SaveChangesAsync();

        _logger.LogInformation($"Backup {backupId}: {message}");
    }

    private string GenerateBackupFilePath(Backup backup)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string extension = backup.IsCompressed ? ".zip" : ".bak";

        return Path.Combine(_backupPath, $"{backup.Type}_{backup.Id}_{timestamp}{extension}");
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    private async Task EncryptFileAsync(string inputFile, string outputFile)
    {
        // Get encryption key from configuration or generate one
        string encryptionKey = _configuration["BackupSettings:EncryptionKey"] ?? "SuperPanelBackupKey2024!";
        
        // Ensure key is 32 bytes for AES-256
        if (encryptionKey.Length < 32)
        {
            encryptionKey = encryptionKey.PadRight(32, '0');
        }
        else if (encryptionKey.Length > 32)
        {
            encryptionKey = encryptionKey.Substring(0, 32);
        }

        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
            aes.IV = new byte[16]; // Use zero IV for simplicity (in production, use random IV)

            using (var inputStream = File.OpenRead(inputFile))
            using (var outputStream = File.Create(outputFile))
            using (var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                await inputStream.CopyToAsync(cryptoStream);
            }
        }
    }

    private async Task DecryptFileAsync(string inputFile, string outputFile)
    {
        // Get encryption key from configuration
        string encryptionKey = _configuration["BackupSettings:EncryptionKey"] ?? "SuperPanelBackupKey2024!";
        
        // Ensure key is 32 bytes for AES-256
        if (encryptionKey.Length < 32)
        {
            encryptionKey = encryptionKey.PadRight(32, '0');
        }
        else if (encryptionKey.Length > 32)
        {
            encryptionKey = encryptionKey.Substring(0, 32);
        }

        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
            aes.IV = new byte[16]; // Use zero IV for simplicity

            using (var inputStream = File.OpenRead(inputFile))
            using (var outputStream = File.Create(outputFile))
            using (var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
            {
                await cryptoStream.CopyToAsync(outputStream);
            }
        }
    }
}

public class BackupRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BackupType Type { get; set; }
    public int? ServerId { get; set; }
    public int? DatabaseId { get; set; }
    public int? DomainId { get; set; }
    public string? BackupPath { get; set; }
    public bool IsCompressed { get; set; } = true;
    public bool IsEncrypted { get; set; } = false;
    public int RetentionDays { get; set; } = 30;
}

public class RestoreRequest
{
    public string? RestorePath { get; set; }
    public bool OverwriteExisting { get; set; } = false;
}

public class RestoreResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long BytesRestored { get; set; }
}