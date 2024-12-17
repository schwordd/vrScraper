using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services.ParsingModels;

namespace vrScraper.Services
{
    public interface IEpornerScraper
  {
    void Initialize();

    Task<(VideoPlayerSettings PlayerSettings, List<string> Tags, List<string> Stars)> GetDetails(DbVideoItem item);
    Task<Quality?> GetBestVideoQuality(DbVideoItem item, VideoPlayerSettings settings);
    Task<VideoSource?> GetSource(DbVideoItem item, VrScraperContext context);
    Task ParseMissingInformations();
    void StartScraping(int start, int count);
    bool ScrapingInProgress { get; }
    string ScrapingStatus { get; }
  }
}
