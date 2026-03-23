using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public interface IScrapeLogService
  {
    Task<DbScrapeLog> StartLog(string site, string triggerType);
    Task FinishLog(long logId, int pages, int newVids, int dupes, int errors, string status);
    Task<List<DbScrapeLog>> GetRecentLogs(int count = 10);
  }
}
