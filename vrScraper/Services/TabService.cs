using vrScraper.DB.Models;
using vrScraper.DB;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class TabService(ILogger<TabService> logger, IServiceProvider serviceProvider) : ITabService
  {
    private List<DbVrTab> tabs = new List<DbVrTab>();

    public async Task Initialize()
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      this.tabs = await context.Tabs.AsNoTracking().ToListAsync();
      logger.LogInformation("{c} Vr tabs loaded", this.tabs.Count);
    }

    public Task<List<DbVrTab>> GetAllTabs()
    {
      return Task.FromResult(tabs);
    }

    public async Task AddTab(DbVrTab newTab)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      context.Tabs.Add(newTab);
      await context.SaveChangesAsync();

      // Füge das neue Tab der lokalen Liste hinzu
      tabs.Add(newTab);
    }

    public async Task DeleteTab(long id)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var tabToDelete = await context.Tabs.FindAsync(id);

      if (tabToDelete != null)
      {
        context.Tabs.Remove(tabToDelete);
        await context.SaveChangesAsync();

        // Entferne das Tab aus der lokalen Liste
        tabs.RemoveAll(a => a.Id == tabToDelete.Id);
      }
    }

    public async Task UpdateTab(DbVrTab tab)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
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
