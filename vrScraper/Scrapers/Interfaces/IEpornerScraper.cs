using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Scrapers.ParsingModels;

namespace vrScraper.Scrapers.Interfaces
{
    public interface IEpornerScraper : IVideoScraper
  {
    Task<(VideoPlayerSettings PlayerSettings, List<string> Tags, List<string> Stars, AdditionalVideoDetails VideoDetails)> GetDetails(DbVideoItem item);
    Task<Quality?> GetBestVideoQuality(DbVideoItem item, VideoPlayerSettings settings);
    Task ParseMissingInformations();
    Task ReparseInformations(CancellationToken cancellationToken = default);
    Task RemoveDeadByPicture(CancellationToken cancellationToken = default);
    Task DeleteErrorItems(CancellationToken cancellationToken = default);
    void StartRemoveByDeadPicture();
    void StartDeleteErrorItems();
    void StartReparseInformations();
  }
}
