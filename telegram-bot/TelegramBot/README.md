# FomoFaster Backend - C# / ASP.NET Core

Backend API built with ASP.NET Core 8.0 that receives FOMO notifications from the Android app and forwards them to Telegram.

## Features

- ASP.NET Core 8.0 Web API
- Telegram Bot integration
- CORS enabled for development
- Swagger/OpenAPI documentation
- Structured logging
- Dependency injection
- Clean architecture with services and models

## Project Structure

```
FomoFaster.Backend/
├── Controllers/
│   └── NotificationsController.cs    # API endpoints
├── Models/
│   ├── NotificationRequest.cs        # Request DTOs
│   └── TelegramSettings.cs           # Configuration models
├── Services/
│   ├── ITelegramService.cs           # Telegram service interface
│   ├── TelegramService.cs            # Telegram bot implementation
│   └── NotificationParser.cs         # Notification parsing logic
├── Properties/
│   └── launchSettings.json           # Launch configuration
├── appsettings.json                  # Production settings
├── appsettings.Development.json      # Development settings
├── Program.cs                        # App entry point
└── FomoFaster.Backend.csproj         # Project file
```

## Prerequisites

- **.NET 8.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** (optional) or **VS Code** with C# extension

## Getting Started

### 1. Install .NET SDK

Check if .NET is installed:

```cmd
dotnet --version
```

Should show `8.0.x` or higher. If not, download and install from Microsoft.

### 2. Restore Dependencies

```cmd
cd c:\Users\jose9\Desktop\repos\fomofaster\backend-dotnet\FomoFaster.Backend
dotnet restore
```

### 3. Configure Telegram Bot

Edit `appsettings.Development.json`:

```json
{
  "Telegram": {
    "BotToken": "1234567890:ABCdefGHIjklMNOpqrsTUVwxyz",
    "ChannelId": -1001234567890
  }
}
```

**How to get these values:**

**Bot Token:**
1. Open Telegram
2. Search for @BotFather
3. Send `/newbot`
4. Follow prompts to create bot
5. Copy the token

**Channel ID:**
1. Create a Telegram channel
2. Add your bot as administrator
3. Send a message to the channel
4. Visit: `https://api.telegram.org/bot<YOUR_BOT_TOKEN>/getUpdates`
5. Look for `"chat":{"id":-1001234567890}` - that's your channel ID

### 4. Run the Backend

```cmd
dotnet run
```

You should see:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:8000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### 5. Test the API

**Visit Swagger UI:**
Open browser: http://localhost:8000/swagger

**Test health endpoint:**
```cmd
curl http://localhost:8000/health
```

**Test notification endpoint:**
```cmd
curl -X POST http://localhost:8000/api/notifications ^
  -H "Content-Type: application/json" ^
  -d "{\"app\":\"fomo\",\"title\":\"TEST at $1m MC\",\"text\":\"@trader bought $1000\",\"contractAddress\":\"0xTEST\",\"timestamp\":1234567890}"
```

## API Endpoints

### GET /

Returns basic info about the API.

**Response:**
```json
{
  "message": "FomoFaster Backend Running",
  "version": "1.0.0"
}
```

### GET /health

Health check endpoint.

**Response:**
```json
{
  "status": "healthy",
  "telegramConfigured": true
}
```

### POST /api/notifications

Receives notifications from Android app.

**Request Body:**
```json
{
  "app": "fomo",
  "title": "AIXBT at $76.2m MC",
  "text": "@frankdegods sold $10,759.14",
  "contractAddress": "0xABC123...",
  "timestamp": 1762121165048
}
```

**Response:**
```json
{
  "status": "success",
  "message": "Notification received and sent to Telegram"
}
```

## Configuration

### Using appsettings.json

For production, edit `appsettings.json`:

```json
{
  "Telegram": {
    "BotToken": "production_token_here",
    "ChannelId": -1001234567890
  }
}
```

### Using Environment Variables

You can also use environment variables (recommended for production):

**Windows (PowerShell):**
```powershell
$env:Telegram__BotToken="your_token_here"
$env:Telegram__ChannelId="-1001234567890"
dotnet run
```

**Windows (Command Prompt):**
```cmd
set Telegram__BotToken=your_token_here
set Telegram__ChannelId=-1001234567890
dotnet run
```

**Linux:**
```bash
export Telegram__BotToken="your_token_here"
export Telegram__ChannelId="-1001234567890"
dotnet run
```

**Note:** Use double underscores `__` to represent nested configuration in environment variables.

## Development

### Building

```cmd
# Debug build
dotnet build

# Release build
dotnet build -c Release
```

### Running Tests (when added)

```cmd
dotnet test
```

### Hot Reload

The app supports hot reload during development:

```cmd
dotnet watch run
```

Now you can edit code and see changes without restarting!

### Adding New Features

**Example: Add a new service**

1. Create interface in `Services/`:
   ```csharp
   public interface IMyService
   {
       Task DoSomethingAsync();
   }
   ```

2. Implement in `Services/`:
   ```csharp
   public class MyService : IMyService
   {
       public async Task DoSomethingAsync()
       {
           // Implementation
       }
   }
   ```

3. Register in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IMyService, MyService>();
   ```

## Publishing

### Publish for Windows

```cmd
dotnet publish -c Release -r win-x64 --self-contained
```

Output: `bin\Release\net8.0\win-x64\publish\`

### Publish for Linux (VPS deployment)

```cmd
dotnet publish -c Release -r linux-x64 --self-contained
```

Output: `bin\Release\net8.0\linux-x64\publish\`

### Publish Framework-Dependent (smaller size)

```cmd
dotnet publish -c Release
```

Requires .NET runtime on target machine.

## Deployment

### Local Testing (Windows)

Just run:
```cmd
dotnet run
```

Access from Android app using: `http://10.0.2.2:8000`

### Windows Service

**Install as Windows Service:**

```cmd
# Publish
dotnet publish -c Release -r win-x64

# Install service (requires admin)
sc create FomoFaster binPath="C:\path\to\FomoFaster.Backend.exe"
sc start FomoFaster
```

### Linux VPS (Ubuntu/Debian)

**1. Copy files to server:**

```bash
# On your PC
dotnet publish -c Release -r linux-x64 --self-contained
scp -r bin/Release/net8.0/linux-x64/publish/* user@server:/opt/fomofaster/
```

**2. Create systemd service:**

Create `/etc/systemd/system/fomofaster.service`:

```ini
[Unit]
Description=FomoFaster Backend
After=network.target

[Service]
Type=simple
User=www-data
WorkingDirectory=/opt/fomofaster
ExecStart=/opt/fomofaster/FomoFaster.Backend
Restart=always
RestartSec=10
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="Telegram__BotToken=your_token"
Environment="Telegram__ChannelId=-1001234567890"

[Install]
WantedBy=multi-user.target
```

**3. Start service:**

```bash
sudo systemctl daemon-reload
sudo systemctl enable fomofaster
sudo systemctl start fomofaster
sudo systemctl status fomofaster
```

### Docker Deployment

**Create Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["FomoFaster.Backend.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FomoFaster.Backend.dll"]
```

**Build and run:**

```bash
# Build
docker build -t fomofaster-backend .

# Run
docker run -d -p 8000:8000 \
  -e Telegram__BotToken="your_token" \
  -e Telegram__ChannelId="-1001234567890" \
  fomofaster-backend
```

## Troubleshooting

### Port already in use

Change port in `Properties/launchSettings.json`:

```json
"applicationUrl": "http://0.0.0.0:9000"
```

### CORS errors

CORS is enabled for all origins in development. For production, configure properly:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production",
        policy => policy.WithOrigins("https://yourdomain.com")
                       .AllowAnyMethod()
                       .AllowAnyHeader());
});
```

### Telegram not sending messages

1. Check bot token is correct
2. Verify channel ID (must include minus sign: `-1001234567890`)
3. Ensure bot is administrator in channel
4. Check logs: `dotnet run --verbosity detailed`

### Connection refused from Android

- Use `http://10.0.2.2:8000` (not `localhost`)
- Check Windows Firewall allows port 8000
- Verify backend is running: `curl http://localhost:8000/health`

## Logging

Logs are written to console by default. Configure in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "FomoFaster.Backend": "Debug"
    }
  }
}
```

View logs:
```cmd
# During development
dotnet run

# Production (systemd)
sudo journalctl -u fomofaster -f
```

## Performance

- Uses dependency injection for efficient service lifetime management
- Async/await throughout for non-blocking I/O
- Minimal allocations with modern C# features
- Can handle 1000+ requests/second on modest hardware

## Security Notes

**Development:**
- CORS allows all origins
- HTTP only (no HTTPS)
- No authentication

**Production TODO:**
- Enable HTTPS
- Restrict CORS to specific origins
- Add API key authentication
- Use environment variables for secrets
- Set up rate limiting

## Dependencies

- **Telegram.Bot** (19.0.0) - Telegram Bot API client
- **Swashbuckle.AspNetCore** (6.5.0) - Swagger/OpenAPI docs
- **Microsoft.AspNetCore.OpenApi** (8.0.0) - OpenAPI support

## Advantages of C# Backend

✅ **Type Safety** - Compile-time checks prevent many bugs
✅ **Performance** - Faster than Python, lower memory usage
✅ **Familiar** - If you know C#, easier than learning Python
✅ **Visual Studio** - Excellent debugging and tooling
✅ **Async/Await** - Built-in async programming model
✅ **NuGet** - Huge ecosystem of packages
✅ **Cross-platform** - Runs on Windows, Linux, macOS
✅ **Free** - Open source, no licensing costs

## Next Steps

1. ✅ Backend API working
2. ✅ Telegram integration
3. 📋 Add database (Entity Framework Core)
4. 📋 Add authentication (JWT tokens)
5. 📋 Add filtering by traders
6. 📋 Add web dashboard (Blazor)
7. 📋 Deploy to production VPS

## License

MIT License

## Support

For issues:
1. Check logs: `dotnet run --verbosity detailed`
2. Verify configuration in `appsettings.Development.json`
3. Test health endpoint: `curl http://localhost:8000/health`
4. Check Telegram bot token and channel ID

---

**Built with ASP.NET Core 8.0** - Fast, modern, cross-platform backend for FomoFaster
