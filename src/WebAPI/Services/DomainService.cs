using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public interface IDomainService
{
    Task<List<Domain>> GetAllDomainsAsync();
    Task<Domain?> GetDomainByIdAsync(int id);
    Task<Domain> CreateDomainAsync(Domain domain);
    Task<Domain?> UpdateDomainAsync(int id, Domain domain);
    Task<bool> DeleteDomainAsync(int id);
    Task<List<Domain>> GetDomainsByServerIdAsync(int serverId);
}

public class DomainService : IDomainService
{
    private readonly ApplicationDbContext _context;

    public DomainService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Domain>> GetAllDomainsAsync()
    {
        return await _context.Domains
            .Include(d => d.Server)
            .Include(d => d.Subdomains)
            .ToListAsync();
    }

    public async Task<Domain?> GetDomainByIdAsync(int id)
    {
        return await _context.Domains
            .Include(d => d.Server)
            .Include(d => d.Subdomains)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Domain> CreateDomainAsync(Domain domain)
    {
        domain.CreatedAt = DateTime.UtcNow;
        _context.Domains.Add(domain);
        await _context.SaveChangesAsync();
        return domain;
    }

    public async Task<Domain?> UpdateDomainAsync(int id, Domain domain)
    {
        var existingDomain = await _context.Domains.FindAsync(id);
        if (existingDomain == null)
            return null;

        existingDomain.Name = domain.Name;
        existingDomain.DocumentRoot = domain.DocumentRoot;
        existingDomain.Status = domain.Status;
        existingDomain.SslEnabled = domain.SslEnabled;
        existingDomain.SslExpiry = domain.SslExpiry;
        existingDomain.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existingDomain;
    }

    public async Task<bool> DeleteDomainAsync(int id)
    {
        var domain = await _context.Domains.FindAsync(id);
        if (domain == null)
            return false;

        _context.Domains.Remove(domain);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Domain>> GetDomainsByServerIdAsync(int serverId)
    {
        return await _context.Domains
            .Where(d => d.ServerId == serverId)
            .Include(d => d.Subdomains)
            .ToListAsync();
    }
}