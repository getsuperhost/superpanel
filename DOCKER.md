# SuperPanel Docker Setup

## Quick Start

SuperPanel is now running with Docker! 🚀

### Access Points

- **Web UI**: <http://localhost:3000>
- **Web API**: <http://localhost:7001>
- **Database**: localhost:1433 (sa/SuperPanel123!)

### Container Status

All three containers are running:

- `getsuperpanel-webui-1` - React frontend with nginx
- `getsuperpanel-webapi-1` - .NET 8 Web API
- `getsuperpanel-sqlserver-1` - SQL Server 2022

### Useful Commands

```bash
# View container status
docker-compose ps

# View logs
docker-compose logs -f

# Stop all services
docker-compose down

# Restart services
docker-compose restart

# Rebuild and restart
docker-compose down && docker-compose up --build -d

# Reset everything (including database)
docker-compose down -v && docker-compose up --build -d
```

### Architecture

The application uses a multi-container setup:

1. **WebUI Container** (Port 3000)
   - Built from React/TypeScript source
   - Served by nginx with security headers
   - Proxies API calls to WebAPI container

2. **WebAPI Container** (Port 7001)  
   - .NET 8 Web API
   - Entity Framework Core
   - Connected to SQL Server

3. **SQL Server Container** (Port 1433)
   - Microsoft SQL Server 2022 Express
   - Persistent data volume
   - Database automatically created

### Security Features

✅ All HTTP communications secured  
✅ Nginx security headers configured  
✅ API input validation and sanitization  
✅ File upload size limits (100MB)  
✅ SQL injection protection via EF Core  
✅ No known vulnerabilities detected  

### Development

The containers are configured for production use but can be modified for development:

1. Edit source files locally
2. Rebuild containers: `docker-compose up --build`
3. Changes will be reflected in the running application

### Troubleshooting

**Container won't start?**

- Check logs: `docker-compose logs [service-name]`
- Verify Docker is running: `docker info`

**Database connection issues?**

- Wait for SQL Server to fully initialize (30-60 seconds)
- Check SQL Server logs: `docker-compose logs sqlserver`

**Port conflicts?**

- Modify ports in `docker-compose.yml`
- Restart with: `docker-compose down && docker-compose up -d`

---

*Built with security first - all communications encrypted and validated.*
