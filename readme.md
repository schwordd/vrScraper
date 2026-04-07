# VRScraper

VR video metadata scraper with web UI and VR headset integration (DeoVR, HereSphere).

> **Important Update Notice:**
> If you are updating from a version < 1.0.15, **rename your database file** from `deovrscraper.db` to `vrscraper.db`.

## Setup

### Windows
1. Download the latest [Release](https://github.com/schwordd/vrScraper/releases) (.zip) and extract it.
2. (Optional) Edit `appsettings.json` to set a different port than 5001.
3. Run `vrScraper.exe`.
4. Open `http://[YOUR-IP]:[PORT]` in your browser.
5. Start the scraper and scrape a few pages, reload the page when finished.
6. Leave the application running.

### Docker
```bash
docker pull schwordd/vrscraper:latest
docker run -d -p 8080:5001 -v ./data:/app/data --restart unless-stopped schwordd/vrscraper:latest
```
Access the web UI at `http://[YOUR-IP]:8080`.

### Docker Compose
```yaml
services:
  vrscraper:
    image: schwordd/vrscraper:latest
    ports:
      - "8080:5001"
    volumes:
      - ./data:/app/data
    environment:
      - TZ=Europe/Berlin
    restart: unless-stopped
```

## Connecting a VR Headset

### HereSphere (Recommended)
In the HereSphere browser, open `http://[YOUR-IP]:[PORT]/heresphere`

### DeoVR
In the DeoVR browser, open `http://[YOUR-IP]:[PORT]/deovr`

## Updating
1. Backup `appsettings.json` and `vrscraper.db`.
2. Download the latest release and replace the old files.
3. Restore your backed-up files.

## Development
```bash
dotnet run --project vrScraper                           # Run in dev mode
dotnet ef migrations add [NAME] -o ./DB/Migrations       # Add migration
```

## Changelog

<!-- CHANGELOG:START -->
# Changelog

## Unreleased

## v1.2.5 (2026-04-07)
- Allow VR headset API endpoints through auth guard

## v1.2.4 (2026-04-05)
- Add automated changelog generation and README overhaul
- Fix scrape logging, live page stop, recommendations, filter UI polish, auth guard, player improvements

## v1.2.3 (2026-04-05)
- Restructure project: extract Scrapers/ and Normalization/ from Services/, move interfaces to subfolders, extract CharacterMappings, remove legacy CSS
- Add focus-visible states, mobile responsiveness, reduced-motion support, design token colors, localStorage safety
- Fix JS event listener leaks, remove pill style duplication, standardize JSON lib, add recommendation fallback, fix tab order and Math.Log guard
- Fix thread safety with ReaderWriterLockSlim, eliminate EventServer race conditions, add filtered items cache and static file caching
- Fix critical crash bugs, remove eval(), add rating bounds, dead thumbnail check and DB indexes
- Add graph page, clickable filter links for tags/actresses, fix rating normalization, persist mute state
- Tune recommendation engine and taste profile display

## v1.2.0 (2026-03-30)
- Add units to settings labels: Min Duration (sec), Scrape Time (HH:MM)
- Tracking & recommendation engine overhaul: VideoEngagement table, multi-feature similarity, diversity injection
- Upgrade VR player libs: video.js 8.23.7, @blaineam/videojs-vr 3.1.1 for working 180 mouse interaction
- Extract VideoCard component, improve filter UX with tooltips, empty states, and vr-panel styling

## v1.1.0 (2026-03-30)
- Fix CI: update .NET SDK from 8.0 to 10.0, setup-dotnet v4
- Decoder fixes: 16 new confusable mappings, protect clean titles from false corrections, 4K leet protection
- Auto-enrich per video after scraping/rescrape, possessive star name matching, mobile hamburger fix
- Remove Ollama/LLM integration completely, decoder-only normalization
- Two-pass decoder architecture, gold testset (5547), UI cleanup, progress fix

## v1.0.26 (2025-12-05)
- Bug fixes

## v1.0.25 (2025-11-20)
- Refactor: extract inline styles to CSS isolation files
- Style fixes and download feature

## v1.0.24 (2025-09-08)
- Fix: remove stopAtKnownVideo option

## v1.0.22 (2025-09-08)
- Add scheduled scraping, thumbnail preview during rescrape, and modern settings UI
- PlayCount visualization

## v1.0.15 (2024-12-17)
- Name refactoring (deovrscraper to vrscraper)

## v1.0.14 (2024-12-17)
- HereSphere support

## v1.0.13 (2024-11-30)
- Tab management and multiple fixes

## v1.0.10 (2024-10-08)
- Tab system, favorites, likes, live view, stats

## v1.0.0 (2024-08-30)
- Initial release

<!-- CHANGELOG:END -->
