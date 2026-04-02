using vrScraper.DB.Models;

namespace vrScraper.Services.Interfaces
{
  public record ScoredVideo(DbVideoItem Video, double Score, string? Reason);

  public interface IRecommendationService
  {
    List<ScoredVideo> GetRecommendedVideos(List<DbVideoItem> allItems, int limit = 500);
    List<ScoredVideo> GetSimilarVideos(long videoId, List<DbVideoItem> allItems, int limit = 20);
    (Dictionary<string, double> TagAffinities, Dictionary<string, double> StarAffinities) GetAffinities(List<DbVideoItem> allItems);
  }
}
