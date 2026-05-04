using vrScraper.DB.Models;

namespace vrScraper.Services.Interfaces
{
  public interface IDailySnapshotService
  {
    Task<List<DbDailySnapshot>> GetLastSnapshots(int days);
    Task EnsureTodaySnapshot();
  }
}
