# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Development Commands

### Building and Running
```bash
# Build the project
dotnet build

# Run in development mode
dotnet run --project vrScraper

# Publish a single-file executable for Windows
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release
```

### Docker
```bash
docker-compose up -d        # Start containerized
docker-compose down          # Stop
```

### Database Management
```bash
# Add a new Entity Framework migration
dotnet ef migrations add [MIGRATION_NAME] -o ./DB/Migrations

# Apply migrations to database
dotnet ef database update
```

### Testing
No test projects exist in this solution.

## Architecture Overview

VrScraper is an ASP.NET Core 8.0 / Blazor Server application that scrapes VR video metadata and serves it via API endpoints compatible with VR players (DeoVR, HereSphere). Uses SQLite via EF Core, Serilog for logging, HtmlAgilityPack + RestSharp for scraping.

### Project Structure (single project: `vrScraper/`)

- **Controllers/** — API endpoints for VR players and video proxying
  - `VrScraperBaseController` — base with common `BaseUrl` helper
  - `DeoVrController` (`GET /deovr`, `GET /deovr/detail/{id}`) — DeoVR JSON format
  - `HeresphereController` (`POST /heresphere`, `GET /heresphere/{id}`) — HereSphere JSON format
  - `VideoProxyController` (`GET /api/videoproxy/{videoId}`) — video streaming with range requests
- **Services/** — business logic, all registered as **singletons** in `Program.cs`
  - `VideoService` — in-memory cache of all videos (loaded once at startup), playback tracking, likes/dislikes
  - `EpornerScraper` — web scraper with adaptive rate limiting and CancellationToken support
  - `ScheduledScrapingService` — BackgroundService for automated daily scraping
  - `SettingService` — in-memory cache of DB settings (key-value)
  - `TabService` — CRUD for configurable content tabs
- **DB/** — EF Core with SQLite
  - `VrScraperContext` — DbContext with 5 DbSets: VideoItems, Stars, Tags, Tabs, Settings
  - `Models/` — `DbVideoItem`, `DbStar`, `DbTag`, `DbVrTab`, `DbSetting` (many-to-many between videos↔stars and videos↔tags)
  - `Migrations/` — 17 migration files
  - `Seed/DbDefaults.cs` — seeds default tabs and settings on startup
- **Pages/** — Blazor pages: Index, Videos, Settings, Tabs, Live
- **Components/** — ScrapingTools, VideoPlayer, TagOverview, ActorOverview (with SignalR for real-time scraping progress)

### Key Design Decisions

- **All-in-Memory Architecture**: All videos loaded into memory at startup for fast VR API responses. Services are singletons.
- **Global Tag Blacklist**: JSON array in Settings DB, applied across both VR APIs and the UI.
- **Auto-Migration**: Database migrations run automatically on startup in `Program.cs`.
- **CORS AllowAll**: Required for VR headset browsers to reach the API.
- **Browser Auto-Launch**: Application opens the browser on startup (platform-aware).

### Configuration

- Port: `appsettings.json` → `"Port"` (default: 5001)
- Database: SQLite connection string in `appsettings.json`
  - Dev: `d:\data\vrscraper\vrscraper.db` (from `appsettings.Development.json`)
- Version: embedded `VERSION` file in project, read at runtime

## Code Style

- **CSS Isolation**: Each Razor component gets its own `.razor.css` file for scoped styles — no inline styles except for truly dynamic values
- **Commit messages**: nur Einzeiler (single-line only)
