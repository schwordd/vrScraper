using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public interface ITabFilteringService
  {
    Task<List<(string Name, List<DbVideoItem> Videos)>> GetFilteredTabVideos(
        List<DbVideoItem> allItems,
        List<string> globalBlackList);
  }
}
