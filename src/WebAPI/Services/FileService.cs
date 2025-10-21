using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public interface IFileService
{
    Task<List<FileSystemItem>> GetDirectoryContentsAsync(string path);
    Task<string> ReadFileAsync(string filePath);
    Task<bool> WriteFileAsync(string filePath, string content);
    Task<bool> DeleteFileAsync(string filePath);
    Task<bool> CreateDirectoryAsync(string path);
    Task<bool> DeleteDirectoryAsync(string path);
    Task<bool> MoveFileAsync(string sourcePath, string destinationPath);
    Task<bool> CopyFileAsync(string sourcePath, string destinationPath);
    Task<FileSystemItem?> GetFileInfoAsync(string path);
}

public class FileService : IFileService
{
    private readonly string _rootPath;

    public FileService(IConfiguration configuration)
    {
        _rootPath = configuration["FileService:RootPath"] ?? "/var/www";
    }

    public async Task<List<FileSystemItem>> GetDirectoryContentsAsync(string path)
    {
        var fullPath = GetSafePath(path);
        if (!Directory.Exists(fullPath))
            return new List<FileSystemItem>();

        var items = new List<FileSystemItem>();

        try
        {
            // Add directories
            var directories = Directory.GetDirectories(fullPath);
            foreach (var dir in directories)
            {
                var dirInfo = new DirectoryInfo(dir);
                items.Add(new FileSystemItem
                {
                    Name = dirInfo.Name,
                    FullPath = GetRelativePath(dir),
                    IsDirectory = true,
                    SizeBytes = 0,
                    LastModified = dirInfo.LastWriteTime,
                    Permissions = GetPermissions(dir)
                });
            }

            // Add files
            var files = Directory.GetFiles(fullPath);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                items.Add(new FileSystemItem
                {
                    Name = fileInfo.Name,
                    FullPath = GetRelativePath(file),
                    IsDirectory = false,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Permissions = GetPermissions(file)
                });
            }
        }
        catch (Exception)
        {
            // Handle access denied or other errors
        }

        return await Task.FromResult(items.OrderBy(i => !i.IsDirectory).ThenBy(i => i.Name).ToList());
    }

    public async Task<string> ReadFileAsync(string filePath)
    {
        var fullPath = GetSafePath(filePath);
        if (!File.Exists(fullPath))
            return string.Empty;

        try
        {
            return await File.ReadAllTextAsync(fullPath);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<bool> WriteFileAsync(string filePath, string content)
    {
        var fullPath = GetSafePath(filePath);
        try
        {
            await File.WriteAllTextAsync(fullPath, content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        var fullPath = GetSafePath(filePath);
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CreateDirectoryAsync(string path)
    {
        var fullPath = GetSafePath(path);
        try
        {
            Directory.CreateDirectory(fullPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteDirectoryAsync(string path)
    {
        var fullPath = GetSafePath(path);
        try
        {
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> MoveFileAsync(string sourcePath, string destinationPath)
    {
        var sourceFullPath = GetSafePath(sourcePath);
        var destFullPath = GetSafePath(destinationPath);
        
        try
        {
            if (File.Exists(sourceFullPath))
            {
                File.Move(sourceFullPath, destFullPath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CopyFileAsync(string sourcePath, string destinationPath)
    {
        var sourceFullPath = GetSafePath(sourcePath);
        var destFullPath = GetSafePath(destinationPath);
        
        try
        {
            if (File.Exists(sourceFullPath))
            {
                File.Copy(sourceFullPath, destFullPath, true);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<FileSystemItem?> GetFileInfoAsync(string path)
    {
        var fullPath = GetSafePath(path);
        
        try
        {
            if (Directory.Exists(fullPath))
            {
                var dirInfo = new DirectoryInfo(fullPath);
                return new FileSystemItem
                {
                    Name = dirInfo.Name,
                    FullPath = GetRelativePath(fullPath),
                    IsDirectory = true,
                    SizeBytes = 0,
                    LastModified = dirInfo.LastWriteTime,
                    Permissions = GetPermissions(fullPath)
                };
            }
            else if (File.Exists(fullPath))
            {
                var fileInfo = new FileInfo(fullPath);
                return new FileSystemItem
                {
                    Name = fileInfo.Name,
                    FullPath = GetRelativePath(fullPath),
                    IsDirectory = false,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Permissions = GetPermissions(fullPath)
                };
            }
        }
        catch
        {
            // Handle access denied or other errors
        }

        return null;
    }

    private string GetSafePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return _rootPath;

        // Normalize path separators
        path = path.Replace('\\', '/');
        
        // Remove leading slash if present
        if (path.StartsWith('/'))
            path = path.Substring(1);

        // Combine with root path
        var fullPath = Path.Combine(_rootPath, path);
        
        // Ensure the path is within the root directory
        var fullInfo = new DirectoryInfo(fullPath);
        var rootInfo = new DirectoryInfo(_rootPath);
        
        if (!fullInfo.FullName.StartsWith(rootInfo.FullName))
            return _rootPath;

        return fullPath;
    }

    private string GetRelativePath(string fullPath)
    {
        var rootUri = new Uri(_rootPath + "/");
        var fullUri = new Uri(fullPath);
        return rootUri.MakeRelativeUri(fullUri).ToString();
    }

    private string GetPermissions(string path)
    {
        // Simplified permissions - in a real implementation, you'd want to use
        // platform-specific APIs to get actual file permissions
        try
        {
            var isReadonly = File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
            return isReadonly ? "r--" : "rw-";
        }
        catch
        {
            return "---";
        }
    }
}