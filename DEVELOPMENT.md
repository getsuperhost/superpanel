# Development Setup Guide

## Prerequisites

### Windows Development
- **Visual Studio 2022** (Community, Professional, or Enterprise)
  - Workloads: ASP.NET and web development, .NET desktop development, Desktop development with C++
- **.NET 8.0 SDK** (included with Visual Studio 2022)
- **Node.js 18.x or later** (for Web UI development)
- **SQL Server** (LocalDB, Express, or full edition)

### Linux Development (API only)
- **.NET 8.0 SDK**
- **GCC/G++** (for C++ native library)
- **Node.js 18.x or later**
- **PostgreSQL or SQL Server on Linux**

## Initial Setup

### 1. Clone and Setup Repository
```bash
git clone <repository-url>
cd superpanel
```

### 2. Database Setup
```bash
# Navigate to Web API project
cd src/WebAPI

# Install Entity Framework tools (if not already installed)
dotnet tool install --global dotnet-ef

# Create database and run migrations
dotnet ef database update
```

### 3. Native Library Setup (Windows)
1. Open Visual Studio 2022
2. Load the solution file `SuperPanel.sln`
3. Build the `SuperPanel.NativeLibrary` project first
4. Ensure the compiled DLL is accessible to the Web API project

### 4. Web API Setup
```bash
cd src/WebAPI
dotnet restore
dotnet run
```
The API will be available at `https://localhost:7001`

### 5. Desktop Application Setup
```bash
cd src/DesktopApp
dotnet restore
dotnet run
```

### 6. Web UI Setup
```bash
cd src/WebUI
npm install
npm run dev
```
The Web UI will be available at `http://localhost:3000`

## Development Workflow

### Running All Components
1. **Start SQL Server** (or ensure it's running)
2. **Start Web API**: `cd src/WebAPI && dotnet run`
3. **Start Web UI**: `cd src/WebUI && npm run dev`
4. **Start Desktop App**: `cd src/DesktopApp && dotnet run`

### Hot Reload
- **Web API**: Supports hot reload with `dotnet watch run`
- **Web UI**: Vite provides instant hot reload
- **Desktop App**: Supports hot reload in Visual Studio

### Debugging
- **Web API**: Use Visual Studio or VS Code with C# extension
- **Desktop App**: Use Visual Studio debugger
- **Web UI**: Use browser developer tools + VS Code
- **Native Library**: Debug through Web API project in Visual Studio

## Project Structure Details

### Web API (`src/WebAPI/`)
```
WebAPI/
├── Controllers/        # API endpoints
├── Services/          # Business logic
├── Models/            # Data models
├── Data/              # Entity Framework context
├── Program.cs         # Application entry point
└── appsettings.json   # Configuration
```

### Native Library (`src/NativeLibrary/`)
```
NativeLibrary/
├── SystemMonitor.h    # Header file with exports
├── SystemMonitor.cpp  # Implementation
├── pch.h             # Precompiled header
└── dllmain.cpp       # DLL entry point
```

### Desktop App (`src/DesktopApp/`)
```
DesktopApp/
├── Views/            # XAML user interfaces
├── ViewModels/       # MVVM view models
├── Services/         # Application services
├── Models/           # Data models
└── App.xaml         # Application definition
```

### Web UI (`src/WebUI/`)
```
WebUI/
├── src/
│   ├── components/   # React components
│   ├── pages/        # Page components
│   ├── services/     # API services
│   └── types/        # TypeScript type definitions
├── package.json      # Dependencies
└── vite.config.ts    # Build configuration
```

## Configuration

### Environment Variables
Create `.env` files for each project:

#### Web API (`src/WebAPI/.env`)
```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Your_Connection_String
Jwt__Key=Your_JWT_Secret_Key
```

#### Web UI (`src/WebUI/.env`)

```env
VITE_API_BASE_URL=https://localhost:7001
```

### Database Configuration

Update `appsettings.json` in the Web API project:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SuperPanelDb;Trusted_Connection=true"
  }
}
```

## Testing

### Unit Tests
```bash
# Run all tests
dotnet test

# Run specific project tests
dotnet test src/WebAPI.Tests/
```

### Integration Tests
```bash
# Ensure test database is set up
dotnet ef database update --project src/WebAPI --connection "YourTestConnectionString"

# Run integration tests
dotnet test --filter Category=Integration
```

### API Testing
- Use Swagger UI at `https://localhost:7001/swagger`
- Import Postman collection (if available)
- Use REST Client extensions in VS Code

## Common Issues and Solutions

### Native Library Issues
- **DLL not found**: Ensure the C++ project builds successfully and the DLL is in the output directory
- **P/Invoke errors**: Check function signatures match between C++ and C#
- **Platform mismatch**: Ensure both native library and API are built for the same architecture (x64/x86)

### Database Issues
- **Connection failed**: Verify SQL Server is running and connection string is correct
- **Migration errors**: Delete the database and run `dotnet ef database update` again
- **Seed data**: Run database seeders if available

### CORS Issues
- Ensure CORS is properly configured in the Web API for the Web UI origin
- Check that the Web UI is making requests to the correct API URL

### Port Conflicts
- Web API: Default port 7001 (HTTPS), 5000 (HTTP)
- Web UI: Default port 3000
- Change ports in `launchSettings.json` and `vite.config.ts` if needed

## IDE Setup

### Visual Studio 2022
1. Install required workloads
2. Set multiple startup projects: WebAPI + DesktopApp
3. Configure debugging for mixed-mode (managed + native)

### VS Code
1. Install C# extension
2. Install ESLint and Prettier for Web UI
3. Configure tasks.json for building all projects

## Performance Tips

### Development
- Use `dotnet watch run` for hot reload
- Enable detailed errors in development
- Use browser dev tools for frontend debugging

### Production
- Build in Release mode
- Enable response compression
- Use connection pooling for database
- Implement proper caching strategies

## Deployment

### Local IIS (Windows)
1. Publish Web API to folder
2. Create IIS application
3. Configure application pool for .NET 8.0

### Docker
```bash
# Build and run with Docker Compose
docker-compose up --build
```

### Cloud Deployment
- Azure App Service for Web API
- Azure Static Web Apps for Web UI
- AWS EC2 or Google Cloud for full stack

## Contributing Guidelines

1. Create feature branches from `main`
2. Follow established coding conventions
3. Write unit tests for new features
4. Update documentation
5. Submit pull requests for review

## Support

For development questions:
- Check existing GitHub issues
- Create new issue with detailed description
- Include error logs and environment details