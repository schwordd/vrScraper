using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services.ParsingModels;

namespace vrScraper.Services
{
    public interface IEpornerScraper
  {
    void Initialize();

    Task<(VideoPlayerSettings PlayerSettings, List<string> Tags, List<string> Stars, AdditionalVideoDetails VideoDetails)> GetDetails(DbVideoItem item);
    Task<Quality?> GetBestVideoQuality(DbVideoItem item, VideoPlayerSettings settings);
    Task<VideoSource?> GetSource(DbVideoItem item, VrScraperContext context);
    Task ParseMissingInformations();
    Task ReparseInformations();
    void StartRemoveByDeadPicture();
    void StartScraping(int start, int count);
    void StartDeleteErrorItems();
    bool ScrapingInProgress { get; }
    string ScrapingStatus { get; }
  }
}
