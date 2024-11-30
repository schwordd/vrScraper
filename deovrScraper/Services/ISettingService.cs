using deovrScraper.DB.Models;

namespace deovrScraper.Services
{
  public interface ISettingService
  {
    Task Initialize();
    Task<List<DbSetting>> GetAllSettings();
    Task<DbSetting> GetSetting(string key);
    Task<DbSetting> UpdateSetting(DbSetting setting);
  }
}
