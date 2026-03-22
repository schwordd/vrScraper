using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class SettingService(ILogger<SettingService> logger, IServiceProvider serviceProvider) : ISettingService
  {
    private List<DbSetting> _settings = new List<DbSetting>();
    private readonly object _settingsLock = new object();

    public async Task Initialize()
    {
      logger.LogInformation("loading all settings");
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var items = await context.Settings.AsNoTracking().AsSplitQuery().ToListAsync();
      lock (_settingsLock)
      {
        this._settings = items;
      }
      logger.LogInformation("all settings loaded ({items} settings)", items.Count);
    }

    public Task<List<DbSetting>> GetAllSettings()
    {
      lock (_settingsLock)
      {
        return Task.FromResult(new List<DbSetting>(_settings));
      }
    }

    public Task<DbSetting?> GetSetting(string key)
    {
      lock (_settingsLock)
      {
        return Task.FromResult(this._settings.Where(a => a.Key == key).FirstOrDefault());
      }
    }

    public async Task<DbSetting?> UpdateSetting(DbSetting setting)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var dbSetting = await context.Settings.Where(s => s.Key == setting.Key).FirstOrDefaultAsync();

      if (dbSetting == null)
      {
        // Create new setting (upsert)
        dbSetting = new DbSetting { Key = setting.Key, Type = setting.Type ?? "System.String", Value = setting.Value };
        context.Settings.Add(dbSetting);
        await context.SaveChangesAsync();

        lock (_settingsLock)
        {
          _settings.Add(dbSetting);
        }
        logger.LogInformation("Created new setting '{Key}'", setting.Key);
        return setting;
      }

      dbSetting.Value = setting.Value;
      await context.SaveChangesAsync();

      lock (_settingsLock)
      {
        var memSetting = this._settings.Where(s => s.Key == setting.Key).FirstOrDefault();
        if (memSetting != null)
        {
          memSetting.Value = setting.Value;
        }
      }

      return setting;
    }
  }
}
