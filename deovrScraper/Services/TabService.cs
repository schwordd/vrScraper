using deovrScraper.DB.Models;
using deovrScraper.DB;
using Microsoft.EntityFrameworkCore;

namespace deovrScraper.Services
{
  public class TabService(ILogger<TabService> logger, IServiceProvider serviceProvider) : ITabService
  {
    private List<DbDeoVrTab> tabs = new List<DbDeoVrTab>();

    public async Task Initialize()
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<DeovrScraperContext>();
      this.tabs = await context.Tabs.AsNoTracking().ToListAsync();
      logger.LogInformation("{c} DeoVr tabs loaded", this.tabs.Count);
    }

    public Task<List<DbDeoVrTab>> GetAllTabs()
    {
      return Task.FromResult(tabs);
    }

    public async Task AddTab(DbDeoVrTab newTab)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<DeovrScraperContext>();
      context.Tabs.Add(newTab);
      await context.SaveChangesAsync();

      // Füge das neue Tab der lokalen Liste hinzu
      tabs.Add(newTab);
    }

    public async Task DeleteTab(long id)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<DeovrScraperContext>();
      var tabToDelete = await context.Tabs.FindAsync(id);

      if (tabToDelete != null)
      {
        context.Tabs.Remove(tabToDelete);
        await context.SaveChangesAsync();

        // Entferne das Tab aus der lokalen Liste
        tabs.RemoveAll(a => a.Id == tabToDelete.Id);
      }
    }

    public async Task UpdateTab(DbDeoVrTab tab)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<DeovrScraperContext>();
      context.Tabs.Update(tab);
      await context.SaveChangesAsync();

      // Optional: Aktualisieren der lokalen Liste, falls erforderlich
      var index = tabs.FindIndex(t => t.Id == tab.Id);
      if (index != -1)
      {
        tabs[index] = tab;
      }
    }
  }
}
