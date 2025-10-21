# SuperPanel AI Coding Instructions

## Architecture Overview

SuperPanel is a multi-language web hosting control panel with:
- **WebAPI** (C# ASP.NET Core 8.0): REST API with JWT auth, SignalR real-time updates
- **DesktopApp** (C# WPF): Native Windows admin interface using MVVM pattern
- **WebUI** (React/TypeScript): Modern web interface with Ant Design
- **NativeLibrary** (C++17): High-performance system monitoring (Windows-only, disabled on Linux)

## Key Design Patterns

### Service Layer Architecture
- All business logic in `IService` interfaces with implementations in `Services/`
- Dependency injection configured in `Program.cs` for WebAPI, `App.xaml.cs` for Desktop
- Example: `IAuthService` → `AuthService`, `IServerService` → `ServerService`

### Data Flow
- DesktopApp → HttpClient calls → WebAPI Controllers → Services → EF Core → SQL Server
- WebUI → axios calls → WebAPI (same backend)
- Real-time updates via SignalR hubs (`MonitoringHub`)

### Configuration Patterns
- WebAPI: `appsettings.json` with `Jwt:`, `Smtp:`, `FileService:`, `ConnectionStrings:`
- Desktop: `appsettings.json` with `ApiSettings:BaseUrl`
- Environment-specific configs override base settings

## Critical Workflows

### Database Setup
```bash
cd src/WebAPI
dotnet ef database update  # Creates/migrates database
dotnet run                # Seeds admin user + sample servers automatically
```

### Multi-Component Build
```bash
# Build order matters - native library first
./build.sh  # Builds C++, WebAPI, Desktop, WebUI in sequence
```

### Testing
```bash
cd src/SuperPanel.WebAPI.Tests
dotnet test  # xUnit with FluentAssertions, in-memory DB for isolation
```

### Docker Deployment
```bash
docker-compose up --build  # Spins up SQL Server + WebAPI + Nginx WebUI
```

## Project-Specific Conventions

### Error Handling
- Services throw custom exceptions (e.g., `InvalidOperationException` for business rules)
- Controllers catch and return appropriate HTTP status codes
- Desktop/WebUI handle API errors gracefully with user-friendly messages

### Authentication Flow
- JWT tokens stored in `Authorization: Bearer` header
- SignalR passes tokens via query string: `?access_token=...`
- Role-based access: "Administrator", "User" roles

### File Operations
- Root path configured in `FileService:RootPath` (defaults to `/var/www`)
- All file paths validated for security (no `..` traversal)
- Permissions checked before operations

### Cross-Platform Considerations
- Native library reference commented out in WebAPI csproj for Linux builds
- System monitoring falls back to .NET APIs when native library unavailable
- Docker deployment uses Linux containers

## Code Examples

### Adding New Service
```csharp
// Services/INewService.cs
public interface INewService
{
    Task<NewModel> GetByIdAsync(int id);
}

// Services/NewService.cs
public class NewService : INewService
{
    private readonly ApplicationDbContext _context;
    
    public NewService(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<NewModel> GetByIdAsync(int id)
    {
        return await _context.NewModels.FindAsync(id);
    }
}

// Program.cs
builder.Services.AddScoped<INewService, NewService>();
```

### API Controller Pattern
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NewController : ControllerBase
{
    private readonly INewService _service;
    
    public NewController(INewService service)
    {
        _service = service;
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<NewModel>> Get(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result != null ? Ok(result) : NotFound();
    }
}
```

### Desktop ViewModel Pattern
```csharp
public class NewViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private ObservableCollection<NewModel> _items;
    
    public NewViewModel(IApiService apiService)
    {
        _apiService = apiService;
        LoadCommand = new RelayCommand(async () => await LoadAsync());
    }
    
    public ObservableCollection<NewModel> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }
    
    public ICommand LoadCommand { get; }
    
    private async Task LoadAsync()
    {
        try
        {
            var items = await _apiService.GetNewItemsAsync();
            Items = new ObservableCollection<NewModel>(items);
        }
        catch (Exception ex)
        {
            // Handle error, show user message
        }
    }
}
```

## Key Files to Reference

- `src/WebAPI/Program.cs` - DI setup, middleware config, database seeding
- `src/WebAPI/appsettings.json` - All configuration patterns
- `src/DesktopApp/Services/ApiService.cs` - HTTP client patterns, JSON serialization
- `src/WebUI/package.json` - Frontend dependencies and scripts
- `docker-compose.yml` - Deployment architecture
- `SuperPanel.sln` - Project structure and dependencies</content>
<parameter name="filePath">/mnt/c/Users/james/OneDrive/Desktop/panel/2/getsuperpanel/.github/copilot-instructions.md