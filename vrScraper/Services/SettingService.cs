using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class SettingService(ILogger<SettingService> logger, IServiceProvider serviceProvider) : ISettingService
  {
    private List<DbSetting> _settings = new List<DbSetting>();

    public async Task Initialize()
    {
      logger.LogInformation("loading all settings");
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      this._settings = await context.Settings.AsNoTracking().AsSplitQuery().ToListAsync();
      logger.LogInformation("all settings loaded ({items} settings)", this._settings.Count);
    }

    public Task<List<DbSetting>> GetAllSettings()
    {
      return Task.FromResult(this._settings);
    }

    public Task<DbSetting> GetSetting(string key)
    {
      return Task.FromResult(this._settings.Where(a => a.Key == key).Single());
    }

    public async Task<DbSetting> UpdateSetting(DbSetting setting)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var dbSetting = await context.Settings.Where(s => s.Key == setting.Key).SingleAsync();
      var memSetting = this._settings.Where(s => s.Key == setting.Key).Single();

      dbSetting.Value = setting.Value;
      memSetting.Value = setting.Value;

      await context.SaveChangesAsync();

      return setting;
    }
  }
}
