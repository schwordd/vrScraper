using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public interface IRecommendationService
  {
    List<DbVideoItem> GetRecommendedVideos(List<DbVideoItem> allItems, int limit = 500);
    List<DbVideoItem> GetSimilarVideos(long videoId, List<DbVideoItem> allItems, int limit = 20);
    (Dictionary<string, double> TagAffinities, Dictionary<string, double> StarAffinities) GetAffinities(List<DbVideoItem> allItems);
  }
}
