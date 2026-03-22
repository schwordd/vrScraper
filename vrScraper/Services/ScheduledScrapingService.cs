using vrScraper.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace vrScraper.Services
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
        return;
      }

      var now = DateTime.Now;

      // Check if we already scraped today
      var lastScrapeSetting = await settingService.GetSetting("LastScheduledScrape");
      if (lastScrapeSetting != null && DateTime.TryParse(lastScrapeSetting.Value, out DateTime lastScrape))
      {
        if (lastScrape.Date == now.Date)
        {
          return; // Already scraped today
        }
      }

      // Run per-site auto-scraping (each site has its own enabled/time/maxpages settings)
      await RunScheduledScraping(registry, settingService, now, cancellationToken);
    }

    private async Task RunScheduledScraping(IScraperRegistry registry, ISettingService settingService, DateTime now, CancellationToken cancellationToken)
    {
      try
      {
        foreach (var scraper in registry.GetAllScrapers())
        {
          if (cancellationToken.IsCancellationRequested) break;
          if (scraper.ScrapingInProgress) continue;

          var site = scraper.SiteName;

          // Check per-site auto-scrape enabled
          var enabledSetting = await settingService.GetSetting($"Site:{site}:AutoScrapeEnabled");
          if (enabledSetting == null || enabledSetting.Value != "True") continue;

          // Check per-site scrape time
          var timeSetting = await settingService.GetSetting($"Site:{site}:AutoScrapeTime");
          if (timeSetting == null || !TimeSpan.TryParse(timeSetting.Value, out TimeSpan siteScheduledTime))
            continue;

          var currentTime = now.TimeOfDay;
          var diff = (siteScheduledTime - currentTime).TotalMinutes;
          if (diff < 0) diff += 24 * 60;
          var timeDifference = Math.Min(diff, 24 * 60 - diff);
          if (timeDifference > 60) continue; // Not time yet for this site

          // Get per-site max pages
          var maxPagesSetting = await settingService.GetSetting($"Site:{site}:AutoScrapeMaxPages");
          int maxPages = (maxPagesSetting != null && int.TryParse(maxPagesSetting.Value, out int mp)) ? mp : 50;

          _logger.LogInformation("Starting auto-scrape for {Site} with max {MaxPages} pages", site, maxPages);
          scraper.IsScheduledScraping = true;
          scraper.StartScraping(1, maxPages);

          var timeout = DateTime.Now.AddHours(3);
          while (scraper.ScrapingInProgress && !cancellationToken.IsCancellationRequested)
          {
            if (DateTime.Now > timeout)
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

        // Update last scrape date
        var lastScrapeSetting2 = await settingService.GetSetting("LastScheduledScrape");
        if (lastScrapeSetting2 != null)
        {
          lastScrapeSetting2.Value = DateTime.Now.ToString("yyyy-MM-dd");
          await settingService.UpdateSetting(lastScrapeSetting2);
        }

        _logger.LogInformation("Scheduled scraping completed for all sites");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during scheduled scraping");
      }
    }
  }
}
