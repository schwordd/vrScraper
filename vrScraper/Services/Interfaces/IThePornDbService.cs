using vrScraper.Services.Models;

namespace vrScraper.Services.Interfaces
{
  public interface IThePornDbService
  {
    Task<PerformerInfo?> SearchPerformer(string name);
    Task<int> EnrichAllStars(Action<int, int>? progressCallback = null);
  }
}
