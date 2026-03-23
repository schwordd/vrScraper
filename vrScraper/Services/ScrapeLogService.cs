using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class ScrapeLogService(ILogger<ScrapeLogService> logger, IServiceProvider serviceProvider) : IScrapeLogService
  {
    public async Task<DbScrapeLog> StartLog(string site, string triggerType)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var log = new DbScrapeLog
      {
        Site = site,
        StartedUtc = DateTime.UtcNow,
        TriggerType = triggerType,
        Status = "Running"
      };

      context.ScrapeLogs.Add(log);
      await context.SaveChangesAsync();

      logger.LogInformation("Scrape log started: {Site} ({TriggerType}), Id={Id}", site, triggerType, log.Id);
      return log;
    }

    public async Task FinishLog(long logId, int pages, int newVids, int dupes, int errors, string status)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var log = await context.ScrapeLogs.FindAsync(logId);
      if (log == null)
      {
        logger.LogWarning("Scrape log {Id} not found for finish", logId);
        return;
      }

      log.FinishedUtc = DateTime.UtcNow;
      log.PagesScraped = pages;
      log.NewVideos = newVids;
      log.DuplicatesSkipped = dupes;
      log.Errors = errors;
      log.Status = status;

      await context.SaveChangesAsync();
      logger.LogInformation("Scrape log finished: {Id} - {Status}, {NewVids} new, {Dupes} dupes, {Errors} errors",
        logId, status, newVids, dupes, errors);
    }

    public async Task<List<DbScrapeLog>> GetRecentLogs(int count = 10)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      return await context.ScrapeLogs
        .OrderByDescending(l => l.StartedUtc)
        .Take(count)
        .AsNoTracking()
        .ToListAsync();
    }
  }
}
