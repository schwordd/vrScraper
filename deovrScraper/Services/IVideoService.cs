using deovrScraper.DB.Models;

namespace deovrScraper.Services
{
  public interface IVideoService
  {
    Task Initialize();
    Task<List<DbVideoItem>> GetVideoItems();
    Task<List<(DbTag Tag, long Count)>> GetTagInfos();
    Task<List<(DbStar Star, long Count)>> GetActorInfos();

  }
}
