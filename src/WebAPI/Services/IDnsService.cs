using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public interface IDnsService
{
    // DNS Record operations
    Task<List<DnsRecord>> GetDnsRecordsByDomainIdAsync(int domainId);
    Task<DnsRecord?> GetDnsRecordByIdAsync(int id);
    Task<DnsRecord> CreateDnsRecordAsync(DnsRecord record);
    Task<DnsRecord?> UpdateDnsRecordAsync(int id, DnsRecord record);
    Task<bool> DeleteDnsRecordAsync(int id);

    // DNS Zone operations
    Task<DnsZone?> GetDnsZoneByDomainIdAsync(int domainId);
    Task<DnsZone> CreateOrUpdateDnsZoneAsync(int domainId, DnsZone zone);

    // DNS Propagation operations
    Task<DnsPropagationStatus?> GetDnsPropagationStatusByDomainIdAsync(int domainId);
    Task<DnsPropagationStatus> CreateOrUpdateDnsPropagationStatusAsync(int domainId, DnsPropagationStatus status);
    Task<DnsPropagationStatus> CheckDnsPropagationAsync(int domainId);

    // Zone file operations
    Task<string?> GenerateZoneFileAsync(int domainId);
    Task<bool> ValidateZoneFileAsync(string zoneFile);
}