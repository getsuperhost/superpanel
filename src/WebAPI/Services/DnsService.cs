using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using System.Text;

namespace SuperPanel.WebAPI.Services;

public class DnsService : IDnsService
{
    private readonly ApplicationDbContext _context;

    public DnsService(ApplicationDbContext context)
    {
        _context = context;
    }

    // DNS Record operations
    public async Task<List<DnsRecord>> GetDnsRecordsByDomainIdAsync(int domainId)
    {
        return await _context.DnsRecords
            .Where(r => r.DomainId == domainId)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Type)
            .ToListAsync();
    }

    public async Task<DnsRecord?> GetDnsRecordByIdAsync(int id)
    {
        return await _context.DnsRecords.FindAsync(id);
    }

    public async Task<DnsRecord> CreateDnsRecordAsync(DnsRecord record)
    {
        record.CreatedAt = DateTime.UtcNow;
        _context.DnsRecords.Add(record);
        await _context.SaveChangesAsync();
        return record;
    }

    public async Task<DnsRecord?> UpdateDnsRecordAsync(int id, DnsRecord record)
    {
        var existingRecord = await _context.DnsRecords.FindAsync(id);
        if (existingRecord == null)
            return null;

        existingRecord.Name = record.Name;
        existingRecord.Type = record.Type;
        existingRecord.Value = record.Value;
        existingRecord.Ttl = record.Ttl;
        existingRecord.Priority = record.Priority;
        existingRecord.Status = record.Status;
        existingRecord.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existingRecord;
    }

    public async Task<bool> DeleteDnsRecordAsync(int id)
    {
        var record = await _context.DnsRecords.FindAsync(id);
        if (record == null)
            return false;

        _context.DnsRecords.Remove(record);
        await _context.SaveChangesAsync();
        return true;
    }

    // DNS Zone operations
    public async Task<DnsZone?> GetDnsZoneByDomainIdAsync(int domainId)
    {
        return await _context.DnsZones
            .FirstOrDefaultAsync(z => z.DomainId == domainId);
    }

    public async Task<DnsZone> CreateOrUpdateDnsZoneAsync(int domainId, DnsZone zone)
    {
        var existingZone = await GetDnsZoneByDomainIdAsync(domainId);

        if (existingZone == null)
        {
            zone.DomainId = domainId;
            zone.CreatedAt = DateTime.UtcNow;
            _context.DnsZones.Add(zone);
        }
        else
        {
            existingZone.ZoneFile = zone.ZoneFile;
            existingZone.AutoUpdate = zone.AutoUpdate;
            existingZone.UpdatedAt = DateTime.UtcNow;
            zone = existingZone;
        }

        await _context.SaveChangesAsync();
        return zone;
    }

    // DNS Propagation operations
    public async Task<DnsPropagationStatus?> GetDnsPropagationStatusByDomainIdAsync(int domainId)
    {
        return await _context.DnsPropagationStatuses
            .FirstOrDefaultAsync(p => p.DomainId == domainId);
    }

    public async Task<DnsPropagationStatus> CreateOrUpdateDnsPropagationStatusAsync(int domainId, DnsPropagationStatus status)
    {
        var existingStatus = await GetDnsPropagationStatusByDomainIdAsync(domainId);

        if (existingStatus == null)
        {
            status.DomainId = domainId;
            status.CreatedAt = DateTime.UtcNow;
            _context.DnsPropagationStatuses.Add(status);
        }
        else
        {
            existingStatus.State = status.State;
            existingStatus.StartedAt = status.StartedAt;
            existingStatus.CompletedAt = status.CompletedAt;
            existingStatus.ErrorMessage = status.ErrorMessage;
            existingStatus.UpdatedAt = DateTime.UtcNow;
            status = existingStatus;
        }

        await _context.SaveChangesAsync();
        return status;
    }

    public async Task<DnsPropagationStatus> CheckDnsPropagationAsync(int domainId)
    {
        var domain = await _context.Domains.FindAsync(domainId);
        if (domain == null)
            throw new ArgumentException("Domain not found");

        var status = await GetDnsPropagationStatusByDomainIdAsync(domainId) ?? new DnsPropagationStatus
        {
            DomainId = domainId,
            State = PropagationState.InProgress,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Simulate DNS propagation check (in real implementation, this would query DNS servers)
            // For now, we'll mark as completed after a short delay
            status.State = PropagationState.Completed;
            status.CompletedAt = DateTime.UtcNow;
            status.ErrorMessage = null;
        }
        catch (Exception ex)
        {
            status.State = PropagationState.Failed;
            status.ErrorMessage = ex.Message;
        }

        return await CreateOrUpdateDnsPropagationStatusAsync(domainId, status);
    }

    // Zone file operations
    public async Task<string?> GenerateZoneFileAsync(int domainId)
    {
        var domain = await _context.Domains
            .Include(d => d.DnsRecords)
            .FirstOrDefaultAsync(d => d.Id == domainId);

        if (domain == null)
            return null;

        var zoneFile = new StringBuilder();

        // SOA record
        zoneFile.AppendLine($"$TTL 3600");
        zoneFile.AppendLine($"@ IN SOA ns1.{domain.Name}. admin.{domain.Name}. (");
        zoneFile.AppendLine($"    {DateTime.UtcNow:yyyyMMddHH} ; Serial");
        zoneFile.AppendLine($"    3600 ; Refresh");
        zoneFile.AppendLine($"    1800 ; Retry");
        zoneFile.AppendLine($"    604800 ; Expire");
        zoneFile.AppendLine($"    86400 ; Minimum TTL");
        zoneFile.AppendLine($")");
        zoneFile.AppendLine();

        // NS records
        zoneFile.AppendLine($"@ IN NS ns1.{domain.Name}.");
        zoneFile.AppendLine($"@ IN NS ns2.{domain.Name}.");
        zoneFile.AppendLine();

        // Generate records from DNS records
        foreach (var record in domain.DnsRecords.Where(r => r.Status == DnsRecordStatus.Active))
        {
            var recordLine = GenerateRecordLine(record, domain.Name);
            if (!string.IsNullOrEmpty(recordLine))
            {
                zoneFile.AppendLine(recordLine);
            }
        }

        return zoneFile.ToString();
    }

    public async Task<bool> ValidateZoneFileAsync(string zoneFile)
    {
        // Basic validation - check for required SOA record and proper syntax
        if (string.IsNullOrWhiteSpace(zoneFile))
            return false;

        var lines = zoneFile.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var hasSoa = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(";") || string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.Contains("SOA"))
            {
                hasSoa = true;
            }

            // Basic syntax check - should have at least name, TTL/class, type, value
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 && !trimmed.Contains("$TTL") && !trimmed.Contains("$ORIGIN"))
            {
                return false;
            }
        }

        return hasSoa;
    }

    private string GenerateRecordLine(DnsRecord record, string domainName)
    {
        var name = record.Name == "@" ? "@" : record.Name.Replace($".{domainName}", "");
        var ttl = record.Ttl > 0 ? $" {record.Ttl}" : "";
        var priority = record.Type == DnsRecordType.MX && record.Priority > 0 ? $"{record.Priority} " : "";

        return $"{name}{ttl} IN {record.Type} {priority}{record.Value}";
    }
}