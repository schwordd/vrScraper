using vrScraper.DB.Models;

namespace vrScraper.Services.Interfaces
{
  public interface ITabService
  {
    Task Initialize();
    Task<List<DbVrTab>> GetAllTabs();
    Task AddTab(DbVrTab newTab);
    Task DeleteTab(long id);
    Task UpdateTab(DbVrTab tab);
    List<DbVrTab> GetWatchlistTabs();
    Task AddVideoToWatchlist(long tabId, long videoId);
    Task RemoveVideoFromWatchlist(long tabId, long videoId);
    Task<int> RemoveWatchedVideosFromWatchlist(long tabId, HashSet<long> watchedVideoIds);
  }
}
