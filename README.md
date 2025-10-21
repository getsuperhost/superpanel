# SuperPanel - Web Host Control Panel

A comprehensive web hosting control panel built with C/C++/C# and modern web technologies.

## Features

- **Multi-language Architecture**: C# ASP.NET Core Web API, C++ native libraries, C# WPF desktop app, and React web UI
- **Server Management**: Monitor and manage multiple servers
- **Domain Management**: Handle domains and subdomains with SSL integration
- **Email Management**: Complete email hosting with accounts, forwarders, and aliases
- **SSL Certificate Management**: Let's Encrypt integration and certificate lifecycle
- **File Management**: Web-based file manager
- **Database Management**: Manage databases and users
- **System Monitoring**: Real-time system metrics and performance monitoring
- **User Management**: Role-based access control and user administration
- **Modern UI**: Both desktop (WPF) and web (React) interfaces
- **Docker Containerization**: Complete containerized deployment

## Architecture

### Backend Components

1. **Web API (C# ASP.NET Core 8.0)**
   - RESTful API for all operations
   - JWT authentication with role-based access
   - Entity Framework Core for data access
   - SignalR for real-time monitoring
   - Swagger/OpenAPI documentation

2. **Native Library (C++)**
   - High-performance system monitoring
   - Low-level system operations
   - Cross-platform compatibility (Windows/Linux)

3. **Desktop Application (C# WPF)**
   - Native Windows administration interface
   - Real-time monitoring dashboards
   - System tray integration

### Frontend Components

1. **Web UI (React + TypeScript)**
   - Modern web-based control panel
   - Responsive design with Ant Design
   - Real-time updates with React Query

## Technology Stack

### Backend

- **C# ASP.NET Core 8.0** - Web API
- **C++17** - Native system libraries
- **Entity Framework Core** - ORM
- **SQL Server** - Database
- **JWT** - Authentication
- **SignalR** - Real-time communication

### Desktop

- **WPF** - Windows Presentation Foundation
- **ModernWPF** - Modern UI library
- **LiveCharts** - Real-time charting

### Web Frontend

- **React 18** - UI framework
- **TypeScript** - Type safety
- **Ant Design** - UI components
- **React Query** - Data fetching
- **Vite** - Build tool

### Deployment

- **Docker** - Containerization
- **Docker Compose** - Multi-service orchestration
- **Nginx** - Reverse proxy and static file serving

## Project Structure

```text
SuperPanel/
├── src/
│   ├── WebAPI/                 # ASP.NET Core Web API
│   │   ├── Controllers/        # API controllers
│   │   ├── Services/          # Business logic
│   │   ├── Models/            # Data models
│   │   └── Data/              # Entity Framework context
│   ├── NativeLibrary/         # C++ native library
│   │   ├── SystemMonitor.cpp  # System monitoring functions
│   │   └── SystemMonitor.h    # Header file
│   ├── DesktopApp/            # WPF Desktop application
│   │   ├── Views/             # XAML views
│   │   ├── ViewModels/        # MVVM view models
│   │   ├── Services/          # Application services
│   │   └── Models/            # Data models
│   └── WebUI/                 # React web interface
│       ├── src/               # Source files
│       ├── components/        # React components
│       └── pages/             # Page components
└── SuperPanel.sln             # Visual Studio solution
```

## Getting Started

### Prerequisites

- **Visual Studio 2022** (with C++ and .NET workloads)
- **.NET 8.0 SDK**
- **Node.js 18+** (for Web UI)
- **SQL Server** (LocalDB or full instance)
- **Docker** (for containerized deployment)

### Building the Solution

1. **Clone the repository**

   ```bash
   git clone https://github.com/yourusername/superpanel.git
   cd superpanel
   ```

2. **Build the C++ Native Library**

   ```bash
   # Open in Visual Studio and build the NativeLibrary project
   # Or use MSBuild from command line
   msbuild src/NativeLibrary/SuperPanel.NativeLibrary.vcxproj /p:Configuration=Release
   ```

3. **Build and Run the Web API**

   ```bash
   cd src/WebAPI
   dotnet restore
   dotnet ef database update  # Create/update database
   dotnet run
   ```

4. **Build and Run the Desktop App**

   ```bash
   cd src/DesktopApp
   dotnet restore
   dotnet run
   ```

5. **Build and Run the Web UI**

   ```bash
   cd src/WebUI
   npm install
   npm run dev
   ```

### Docker Deployment

For containerized deployment:

```bash
# Build and start all services
docker-compose up --build

# Or run in background
docker-compose up -d --build
```

### Configuration

1. **Database Connection**
   - Update `ConnectionStrings:DefaultConnection` in `src/WebAPI/appsettings.json`

2. **API Settings**
   - Configure API base URL in `src/DesktopApp/appsettings.json`
   - Update proxy settings in `src/WebUI/vite.config.ts`

3. **File Service**
   - Set `FileService:RootPath` in Web API configuration for file management

## API Documentation

Once the Web API is running, visit:

- Swagger UI: `https://localhost:7001/swagger`
- API Explorer: `https://localhost:7001/swagger/v1/swagger.json`

## Key Features

### System Monitoring

- Real-time CPU, memory, and disk usage
- Process monitoring
- Network statistics
- Drive space monitoring

### Server Management

- Add/remove servers
- Monitor server status
- View server metrics
- Manage server configurations

### Domain Management

- Domain and subdomain management
- SSL certificate monitoring
- DNS configuration

### File Management

- Web-based file browser
- File upload/download
- Directory operations
- Permission management

### Database Management

- Multiple database engine support
- User and permission management
- Backup and restore operations

### Email Management

- Email account creation and management
- Forwarders and aliases
- Quota management
- Domain-based email routing

## Security Features

- JWT-based authentication
- Role-based authorization
- Secure file operations
- Input validation and sanitization
- CORS configuration

## Performance Optimizations

- Native C++ libraries for system operations
- Efficient data caching
- Async/await patterns throughout
- Optimized database queries
- Frontend code splitting

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/new-feature`
3. Commit your changes: `git commit -am 'Add new feature'`
4. Push to the branch: `git push origin feature/new-feature`
5. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

For support and questions:

- Create an issue on GitHub
- Contact: `support@superpanel.com`
- Documentation: <https://docs.superpanel.com>

## Roadmap

- [x] Docker containerization
- [ ] Kubernetes support
- [ ] Plugin system
- [ ] Advanced monitoring and alerting
- [ ] Multi-tenant support
- [ ] Mobile app
- [ ] Cloud provider integrations
