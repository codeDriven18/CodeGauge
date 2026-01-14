# CodeGauge Web Interface - Quick Start

## Running the Web Application

### Option 1: Using dotnet run (Development)

```powershell
cd C:\proggggg\CodeGauge\CodeGauge
dotnet run --project src\CodeGauge.Web
```

The app will start on `https://localhost:5001` (and `http://localhost:5000`)

### Option 2: Build and Run

```powershell
cd C:\proggggg\CodeGauge\CodeGauge
dotnet build src\CodeGauge.Web
dotnet src\CodeGauge.Web\bin\Debug\net10.0\CodeGauge.Web.dll
```

## How to Use

1. **Open your browser** to `https://localhost:5001`

2. **Enter a repository URL** in the form:
   - GitHub: `https://github.com/username/repo.git`
   - GitLab: `https://gitlab.com/username/repo.git`
   - Any public Git URL

3. **Click "Analyze Repository"**
   - The app will clone the repo (2 min timeout)
   - Run all 7 analyzers
   - Display results on screen

4. **View Results**
   - See total score with color badge
   - Review category breakdowns
   - Read detailed feedback

5. **Download PDF**
   - Click "Download PDF" button
   - Get professional report ready to share

## Requirements

- Git must be installed and in PATH
- Repository must be publicly accessible (no authentication)
- .NET 10 SDK

## Features

✅ **Paste & Analyze** - No local cloning needed  
✅ **Visual Dashboard** - Color-coded scores and progress bars  
✅ **PDF Export** - Professional reports with QuestPDF  
✅ **Detailed Feedback** - Every issue with recommendations  
✅ **Automatic Cleanup** - Temp files removed after analysis  

## Deployment

### Deploy to Azure App Service

```bash
# Publish
dotnet publish src/CodeGauge.Web -c Release -o publish

# Deploy (requires Azure CLI)
az webapp up --name codegauge-app --resource-group myResourceGroup --location eastus
```

### Deploy to IIS

1. Publish: `dotnet publish src/CodeGauge.Web -c Release`
2. Copy `publish` folder to IIS directory
3. Create Application Pool (.NET CLR Version: No Managed Code)
4. Create Website pointing to publish folder

### Docker

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/CodeGauge.Web/", "CodeGauge.Web/"]
COPY ["src/CodeGauge.Core/", "CodeGauge.Core/"]
COPY ["src/CodeGauge.CSharp/", "CodeGauge.CSharp/"]
COPY ["src/CodeGauge.Report/", "CodeGauge.Report/"]
RUN dotnet restore "CodeGauge.Web/CodeGauge.Web.csproj"
RUN dotnet build "CodeGauge.Web/CodeGauge.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CodeGauge.Web/CodeGauge.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN apt-get update && apt-get install -y git
ENTRYPOINT ["dotnet", "CodeGauge.Web.dll"]
```

Build and run:
```bash
docker build -t codegauge-web .
docker run -p 8080:80 codegauge-web
```

## Troubleshooting

**"Git clone failed"**
- Ensure Git is installed: `git --version`
- Check repository URL is correct and public
- Verify network connectivity

**"Analysis timed out"**
- Large repositories may take >2 minutes
- Increase timeout in `AnalysisService.cs` (line 35)

**PDF download fails**
- Check QuestPDF license is set (Community)
- Verify result is cached in session

## Security Notes

⚠️ **Public Use Warning**: This web app clones arbitrary Git URLs  
- Only deploy on trusted networks or add authentication
- Consider rate limiting to prevent abuse
- Validate/sanitize repository URLs
- Add CAPTCHA for public deployments

## Configuration

Edit `appsettings.json`:

```json
{
  "CodeGauge": {
    "CloneTimeout": 120,
    "AnalysisTimeout": 300,
    "TempPath": "C:\\Temp\\CodeGauge"
  }
}
```

## Performance Tips

- Use SSD for temp directory (faster clone)
- Increase memory for large repos
- Consider background job queue for async processing
- Cache results by repo URL + commit hash
