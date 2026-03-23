using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public interface ISettingService
  {
    Task Initialize();
    Task<List<DbSetting>> GetAllSettings();
    Task<DbSetting?> GetSetting(string key);
    string? GetSettingValue(string key);
    Task<DbSetting?> UpdateSetting(DbSetting setting);
    Task SaveSetting(string key, string value);
  }
}
