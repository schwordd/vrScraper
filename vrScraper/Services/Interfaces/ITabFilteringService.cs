using vrScraper.DB.Models;

namespace vrScraper.Services.Interfaces
{
  public interface ITabFilteringService
  {
    Task<List<(string Name, List<DbVideoItem> Videos)>> GetFilteredTabVideos(
        List<DbVideoItem> allItems,
        List<string> globalBlackList);
  }
}
