using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public interface IDatabaseService
{
    Task<List<Database>> GetAllDatabasesAsync();
    Task<Database?> GetDatabaseByIdAsync(int id);
    Task<Database> CreateDatabaseAsync(Database database);
    Task<Database?> UpdateDatabaseAsync(int id, Database database);
    Task<bool> DeleteDatabaseAsync(int id);
    Task<List<Database>> GetDatabasesByServerIdAsync(int serverId);
}

public class DatabaseService : IDatabaseService
{
    private readonly ApplicationDbContext _context;

    public DatabaseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Database>> GetAllDatabasesAsync()
    {
        return await _context.Databases
            .Include(db => db.Server)
            .Include(db => db.Users)
            .ToListAsync();
    }

    public async Task<Database?> GetDatabaseByIdAsync(int id)
    {
        return await _context.Databases
            .Include(db => db.Server)
            .Include(db => db.Users)
            .FirstOrDefaultAsync(db => db.Id == id);
    }

    public async Task<Database> CreateDatabaseAsync(Database database)
    {
        database.CreatedAt = DateTime.UtcNow;
        _context.Databases.Add(database);
        await _context.SaveChangesAsync();
        return database;
    }

    public async Task<Database?> UpdateDatabaseAsync(int id, Database database)
    {
        var existingDatabase = await _context.Databases.FindAsync(id);
        if (existingDatabase == null)
            return null;

        existingDatabase.Name = database.Name;
        existingDatabase.Type = database.Type;
        existingDatabase.Username = database.Username;
        existingDatabase.SizeInMB = database.SizeInMB;
        existingDatabase.Status = database.Status;
        existingDatabase.BackupDate = database.BackupDate;

        await _context.SaveChangesAsync();
        return existingDatabase;
    }

    public async Task<bool> DeleteDatabaseAsync(int id)
    {
        var database = await _context.Databases.FindAsync(id);
        if (database == null)
            return false;

        _context.Databases.Remove(database);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Database>> GetDatabasesByServerIdAsync(int serverId)
    {
        return await _context.Databases
            .Where(db => db.ServerId == serverId)
            .Include(db => db.Users)
            .ToListAsync();
    }
}