using System.Collections.Generic;
using System.Threading.Tasks;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public interface IBackupService
{
    Task<Backup> CreateBackupAsync(BackupRequest request, int userId);
    Task<Backup?> GetBackupAsync(int id);
    Task<IEnumerable<Backup>> GetBackupsAsync(int? serverId = null, int? databaseId = null, int? domainId = null);
    Task<bool> DeleteBackupAsync(int id);
    Task<RestoreResult> RestoreBackupAsync(int backupId, RestoreRequest request);
}