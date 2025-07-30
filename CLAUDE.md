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

### Database Management
```bash
# Add a new Entity Framework migration
dotnet ef migrations add [MIGRATION_NAME] -o ./DB/Migrations

# Apply migrations to database
dotnet ef database update
```

### Testing
The project uses standard .NET testing patterns. Check for test projects in the solution to determine testing approach.

## Architecture Overview

VrScraper is an ASP.NET Core 8.0 web application that serves as a VR video scraper and organizer with API endpoints for VR players like DeoVR and HereSphere.

### Core Architecture Layers

**Controllers Layer** (`Controllers/`)
- `DeoVrController` - Provides DeoVR-compatible API endpoints for video listings and details
- `HeresphereController` - Provides HereSphere-compatible API endpoints with different data structures  
- `VideoProxyController` - Handles video streaming and proxying functionality
- `VrScraperBaseController` - Base controller with common functionality

**Services Layer** (`Services/`)
- `EpornerScraper` - Web scraping service for adult content site
- `VideoService` - Core video management and business logic
- `SettingService` - Application settings management
- `TabService` - Tab configuration and organization

**Data Layer** (`DB/`)
- Entity Framework Core with SQLite database
- Models: `DbVideoItem`, `DbStar`, `DbTag`, `DbVrTab`, `DbSetting`
- Migration-based schema management

**UI Layer** (`Pages/`, `Components/`)
- Blazor Server for web interface
- Components for video player, actor overview, tag management
- Real-time scraping progress via SignalR

### Key Design Patterns

- **Dependency Injection**: All services registered as singletons in `Program.cs`
- **Repository Pattern**: EF Context serves as repository layer
- **Service Layer Pattern**: Business logic encapsulated in service classes
- **MVC Pattern**: Controllers handle HTTP requests, delegate to services

### Configuration

- Port configuration via `appsettings.json` "Port" setting (default: 5001)
- Database connection string in `appsettings.json`
- Serilog for structured logging with console and optional file output
- CORS enabled for all origins to support VR headset browsers

### VR Player Integration

The application provides two distinct API formats:
- **DeoVR API** (`/deovr`) - JSON format for DeoVR player compatibility
- **HereSphere API** (`/heresphere`) - Different JSON structure for HereSphere player

Both APIs apply global tag blacklisting and support configurable tabs for content organization.

### Database Schema

Core entities:
- `VideoItems` - Main video metadata and statistics
- `Stars` - Adult performers with many-to-many relationship to videos
- `Tags` - Content tags with many-to-many relationship to videos  
- `Tabs` - Configurable content organization tabs
- `Settings` - Key-value application settings storage

## Development Environment

### Database Location
- **Development Database**: `d:\data\vrscraper\vrscraper.db` (configured in `appsettings.Development.json`)
- **Project Database**: `vrScraper\deovrscraper.db` (fallback, not used in development)

### Application Startup
- Default development port: 5001 (configured in `appsettings.Development.json`)
- All-in-memory video loading: ~21,960+ videos loaded at startup for optimal VR API performance
- Settings seeded automatically via `DbDefaults.SeedDefaultSettings()` in `DB/Seed/DbDefaults.cs`

### Implemented Features

#### Automated Daily Scraping System
- **Service**: `ScheduledScrapingService` - Background service for automated daily scraping
- **Settings**: Configurable via Settings UI and stored in database
  - `ScheduledScrapingEnabled` (boolean)
  - `ScheduledScrapingTime` (string, HH:MM format)
  - `ScheduledScrapingMaxPages` (integer, safety limit)
  - `LastScheduledScrape` (string, date tracking)
- **Smart Stopping**: Automatically stops when encountering known videos
- **Rate Limiting**: Defensive adaptive delays (2-5 seconds base, exponential backoff on errors)

#### Enhanced Scraping Options
- **Stop at Known Video**: Available for both manual and scheduled scraping
- **Backwards Rescraping**: Changed from newest-first to oldest-first (`.OrderBy(a => Convert.ToInt32(a.SiteVideoId))`)
- **Error Handling**: Automatic ErrorCount updates and comprehensive try-catch blocks

#### Modern Settings UI
- **Card-based Design**: Professional UI with hover animations and gradient backgrounds
- **Responsive Layout**: CSS Grid with mobile-friendly breakpoints
- **FontAwesome Icons**: Visual hierarchy and professional appearance
- **Consistent Styling**: Unified design across Tag Blacklist and Scheduled Scraping sections

### Content Filtering
- **Global Tag Blacklist**: JSON array stored in Settings, applies to all VR APIs and UI
- **Example Active Filters**: `["Japanese","Fat","Shemale"]` (customizable per user)
- **API Integration**: Both DeoVR and HereSphere APIs respect global blacklist settings

### Performance Optimizations
- **All-in-Memory Architecture**: Videos loaded once at startup, not queried per-request
- **Defensive Rate Limiting**: Prevents blocks from scraped sites with adaptive timing
- **Background Processing**: Scheduled tasks run independently without blocking main application