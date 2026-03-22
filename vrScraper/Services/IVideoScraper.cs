using vrScraper.DB;
using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public interface IVideoScraper
  {
    string SiteName { get; }
    string DisplayName { get; }
    void Initialize();
    Task<VideoSource?> GetSource(DbVideoItem item, VrScraperContext context);
    Dictionary<string, string> GetProxyHeaders();
    void StartScraping(int start, int count);
    void StopScraping();
    bool ScrapingInProgress { get; }
    string ScrapingStatus { get; }
    string? CurrentVideoThumbnail { get; }
    string? CurrentVideoTitle { get; }
    bool IsScheduledScraping { get; set; }
  }
}
