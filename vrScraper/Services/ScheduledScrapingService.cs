using vrScraper.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
      var scraper = scope.ServiceProvider.GetRequiredService<IEpornerScraper>();

      // Check if scheduled scraping is enabled
      var enabledSetting = await settingService.GetSetting("ScheduledScrapingEnabled");
      if (!bool.TryParse(enabledSetting.Value, out bool isEnabled) || !isEnabled)
      {
        return; // Scheduled scraping is disabled
      }

      // Check if another scraping is already running
      if (scraper.ScrapingInProgress)
      {
        _logger.LogInformation("Scraping already in progress, skipping scheduled scraping");
        return;
      }

      // Check if it's time to scrape
      var timeSetting = await settingService.GetSetting("ScheduledScrapingTime");
      if (!TimeSpan.TryParse(timeSetting.Value, out TimeSpan scheduledTime))
      {
        _logger.LogWarning("Invalid scheduled scraping time format: {Time}", timeSetting.Value);
        return;
      }

      var now = DateTime.Now;
      var currentTime = now.TimeOfDay;
      
      // Check if we're within 1 hour of the scheduled time
      var timeDifference = Math.Abs((currentTime - scheduledTime).TotalMinutes);
      if (timeDifference > 60)
      {
        return; // Not time to scrape yet
      }

      // Check if we already scraped today
      var lastScrapeSetting = await settingService.GetSetting("LastScheduledScrape");
      if (DateTime.TryParse(lastScrapeSetting.Value, out DateTime lastScrape))
      {
        if (lastScrape.Date == now.Date)
        {
          return; // Already scraped today
        }
      }

      // Run scheduled scraping
      _logger.LogInformation("Starting scheduled scraping at {Time}", now);
      await RunScheduledScraping(scraper, settingService, cancellationToken);
    }

    private async Task RunScheduledScraping(IEpornerScraper scraper, ISettingService settingService, CancellationToken cancellationToken)
    {
      try
      {
        // Get max pages setting
        var maxPagesSetting = await settingService.GetSetting("ScheduledScrapingMaxPages");
        if (!int.TryParse(maxPagesSetting.Value, out int maxPages))
        {
          maxPages = 50; // Default fallback
        }

        // Set scraping options
        scraper.StopAtKnownVideo = true;
        scraper.IsScheduledScraping = true;

        _logger.LogInformation("Starting scheduled scraping with max {MaxPages} pages", maxPages);

        // Start scraping from page 1 with high page limit
        scraper.StartScraping(1, maxPages);

        // Wait for completion (with timeout)
        var timeout = TimeSpan.FromHours(6); // Max 6 hours
        var start = DateTime.UtcNow;

        while (scraper.ScrapingInProgress && !cancellationToken.IsCancellationRequested)
        {
          if (DateTime.UtcNow - start > timeout)
          {
            _logger.LogWarning("Scheduled scraping timeout reached, stopping");
            scraper.StopScraping();
            break;
          }

          await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        // Update last scrape date
        var lastScrapeSetting = await settingService.GetSetting("LastScheduledScrape");
        lastScrapeSetting.Value = DateTime.Now.ToString("yyyy-MM-dd");
        await settingService.UpdateSetting(lastScrapeSetting);

        _logger.LogInformation("Scheduled scraping completed. Status: {Status}", scraper.ScrapingStatus);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during scheduled scraping");
      }
      finally
      {
        // Reset scraping options
        scraper.StopAtKnownVideo = false;
        scraper.IsScheduledScraping = false;
      }
    }
  }
}