using vrScraper.Services;
using vrScraper.Services.Interfaces;
using vrScraper.Scrapers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace vrScraper.Scrapers
{
  public class ScheduledScrapingService : BackgroundService
  {
    private readonly ILogger<ScheduledScrapingService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ScheduledScrapingService(ILogger<ScheduledScrapingService> logger, IServiceProvider serviceProvider)
    {
      _logger = logger;
      _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("ScheduledScrapingService started");

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          await CheckAndRunScheduledScraping(stoppingToken);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error in scheduled scraping check");
        }

        // Check every hour
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
      }

      _logger.LogInformation("ScheduledScrapingService stopped");
    }

    private async Task CheckAndRunScheduledScraping(CancellationToken cancellationToken)
    {
      using var scope = _serviceProvider.CreateScope();
      var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
      var registry = scope.ServiceProvider.GetRequiredService<IScraperRegistry>();

      // Check if any scraping is already running
      if (registry.GetAllScrapers().Any(s => s.ScrapingInProgress))
      {
        _logger.LogDebug("Scheduled check skipped: scraping already in progress");
        return;
      }

      var tz = await ResolveTimeZone(settingService);
      var nowUtc = DateTime.UtcNow;
      var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

      // Check if we already scraped today (in configured TZ, so "today" matches the user's calendar)
      var lastScrapeSetting = await settingService.GetSetting("LastScheduledScrape");
      if (lastScrapeSetting != null && DateTime.TryParse(lastScrapeSetting.Value, out DateTime lastScrape))
      {
        if (lastScrape.Date == nowLocal.Date)
        {
          _logger.LogDebug("Scheduled check skipped: already scraped today (last: {Date}, TZ: {Tz})", lastScrape.ToString("yyyy-MM-dd"), tz.Id);
          return;
        }
      }

      await RunScheduledScraping(registry, settingService, tz, nowUtc, nowLocal, cancellationToken);
    }

    private async Task RunScheduledScraping(IScraperRegistry registry, ISettingService settingService, TimeZoneInfo tz, DateTime nowUtc, DateTime nowLocal, CancellationToken cancellationToken)
    {
      try
      {
        bool anyRan = false;

        foreach (var scraper in registry.GetAllScrapers())
        {
          if (cancellationToken.IsCancellationRequested) break;
          if (scraper.ScrapingInProgress) continue;

          var site = scraper.SiteName;

          var siteEnabledSetting = await settingService.GetSetting($"Site:{site}:Enabled");
          var siteEnabledDefault = site.Equals("eporner.com", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
          var siteEnabled = siteEnabledSetting?.Value ?? siteEnabledDefault;
          if (!siteEnabled.Equals("true", StringComparison.OrdinalIgnoreCase))
          {
            _logger.LogDebug("Site {Site} skipped: not enabled", site);
            continue;
          }

          var enabledSetting = await settingService.GetSetting($"Site:{site}:AutoScrapeEnabled");
          if (enabledSetting == null || !enabledSetting.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
          {
            _logger.LogDebug("Site {Site} skipped: auto-scrape not enabled", site);
            continue;
          }

          var timeSetting = await settingService.GetSetting($"Site:{site}:AutoScrapeTime");
          if (timeSetting == null || !TimeSpan.TryParse(timeSetting.Value, out TimeSpan siteScheduledLocal))
          {
            _logger.LogInformation("Site {Site} skipped: no valid AutoScrapeTime setting", site);
            continue;
          }

          var timeDifference = MinutesUntilNextOccurrence(siteScheduledLocal, tz, nowUtc);
          if (timeDifference > 60)
          {
            _logger.LogInformation("Site {Site} skipped: configured time {Configured} ({Tz}) not within 60min of now (delta {Diff:F0} min)", site, siteScheduledLocal, tz.Id, timeDifference);
            continue;
          }

          var maxPagesSetting = await settingService.GetSetting($"Site:{site}:AutoScrapeMaxPages");
          int maxPages = (maxPagesSetting != null && int.TryParse(maxPagesSetting.Value, out int mp)) ? mp : 50;

          _logger.LogInformation("Starting auto-scrape for {Site} with max {MaxPages} pages (configured {Configured} {Tz})", site, maxPages, siteScheduledLocal, tz.Id);
          scraper.IsScheduledScraping = true;
          scraper.StartScraping(1, maxPages);
          anyRan = true;

          var timeoutUtc = DateTime.UtcNow.AddHours(3);
          while (scraper.ScrapingInProgress && !cancellationToken.IsCancellationRequested)
          {
            if (DateTime.UtcNow > timeoutUtc)
            {
              _logger.LogWarning("Auto-scrape timeout for {Site}, stopping", site);
              scraper.StopScraping();
              break;
            }
            await Task.Delay(30000, cancellationToken);
          }

          scraper.IsScheduledScraping = false;
          _logger.LogInformation("Auto-scrape completed for {Site}", site);
        }

        if (anyRan)
        {
          var lastScrapeSetting2 = await settingService.GetSetting("LastScheduledScrape");
          if (lastScrapeSetting2 != null)
          {
            lastScrapeSetting2.Value = nowLocal.ToString("yyyy-MM-dd");
            await settingService.UpdateSetting(lastScrapeSetting2);
          }
          _logger.LogInformation("Scheduled scraping completed for all sites");
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during scheduled scraping");
      }
    }

    private async Task<TimeZoneInfo> ResolveTimeZone(ISettingService settingService)
    {
      var tzSetting = await settingService.GetSetting("TimeZone");
      var id = tzSetting?.Value;
      if (string.IsNullOrWhiteSpace(id))
      {
        return TimeZoneInfo.Utc;
      }
      var resolved = TimeZoneResolver.TryResolve(id);
      if (resolved != null) return resolved;
      _logger.LogWarning("Unknown TimeZone setting '{Id}', falling back to UTC. Ensure tzdata is installed in the container.", id);
      return TimeZoneInfo.Utc;
    }

    // Returns the absolute minutes between now (UTC) and the next (or most recent) occurrence
    // of the local time-of-day in the given timezone. Result is in [0, 720].
    private static double MinutesUntilNextOccurrence(TimeSpan localTimeOfDay, TimeZoneInfo tz, DateTime nowUtc)
    {
      var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
      var todayLocal = DateTime.SpecifyKind(nowLocal.Date + localTimeOfDay, DateTimeKind.Unspecified);

      double best = double.MaxValue;
      foreach (var candidateLocal in new[] { todayLocal.AddDays(-1), todayLocal, todayLocal.AddDays(1) })
      {
        DateTime candidateUtc;
        try
        {
          candidateUtc = TimeZoneInfo.ConvertTimeToUtc(candidateLocal, tz);
        }
        catch (ArgumentException)
        {
          // Time falls in a DST gap — skip
          continue;
        }
        var diff = Math.Abs((candidateUtc - nowUtc).TotalMinutes);
        if (diff < best) best = diff;
      }
      return best;
    }
  }
}
