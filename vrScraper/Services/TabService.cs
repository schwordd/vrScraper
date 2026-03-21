using vrScraper.DB.Models;
using vrScraper.DB;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class TabService(ILogger<TabService> logger, IServiceProvider serviceProvider) : ITabService
  {
    private List<DbVrTab> tabs = new List<DbVrTab>();
    private readonly object _tabsLock = new object();

    public async Task Initialize()
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var items = await context.Tabs.AsNoTracking().ToListAsync();
      lock (_tabsLock)
      {
        this.tabs = items;
      }
      logger.LogInformation("{c} Vr tabs loaded", items.Count);
    }

    public Task<List<DbVrTab>> GetAllTabs()
    {
      lock (_tabsLock)
      {
        return Task.FromResult(new List<DbVrTab>(tabs));
      }
    }

    public async Task AddTab(DbVrTab newTab)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      context.Tabs.Add(newTab);
      await context.SaveChangesAsync();

      lock (_tabsLock)
      {
        tabs.Add(newTab);
      }
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

        lock (_tabsLock)
        {
          tabs.RemoveAll(a => a.Id == tabToDelete.Id);
        }
      }
    }

    public async Task UpdateTab(DbVrTab tab)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      context.Tabs.Update(tab);
      await context.SaveChangesAsync();

      lock (_tabsLock)
      {
        var index = tabs.FindIndex(t => t.Id == tab.Id);
        if (index != -1)
        {
          tabs[index] = tab;
        }
      }
    }

    public List<DbVrTab> GetWatchlistTabs()
    {
      lock (_tabsLock)
      {
        return tabs.Where(t => t.Type == "WATCHLIST").ToList();
      }
    }

    public async Task AddVideoToWatchlist(long tabId, long videoId)
    {
      DbVrTab? tab;
      lock (_tabsLock)
      {
        tab = tabs.FirstOrDefault(t => t.Id == tabId && t.Type == "WATCHLIST");
      }
      if (tab == null) return;

      var videoIds = tab.VideoWhitelistList.ToList();
      var videoIdStr = videoId.ToString();
      if (videoIds.Contains(videoIdStr)) return;

      videoIds.Insert(0, videoIdStr);
      tab.VideoWhitelistList = videoIds;

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      context.Tabs.Update(tab);
      await context.SaveChangesAsync();

      lock (_tabsLock)
      {
        var index = tabs.FindIndex(t => t.Id == tab.Id);
        if (index != -1) tabs[index] = tab;
      }
    }

    public async Task RemoveVideoFromWatchlist(long tabId, long videoId)
    {
      DbVrTab? tab;
      lock (_tabsLock)
      {
        tab = tabs.FirstOrDefault(t => t.Id == tabId && t.Type == "WATCHLIST");
      }
      if (tab == null) return;

      var videoIds = tab.VideoWhitelistList.ToList();
      var videoIdStr = videoId.ToString();
      if (!videoIds.Contains(videoIdStr)) return;

      videoIds.Remove(videoIdStr);
      tab.VideoWhitelistList = videoIds;

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      context.Tabs.Update(tab);
      await context.SaveChangesAsync();

      lock (_tabsLock)
      {
        var index = tabs.FindIndex(t => t.Id == tab.Id);
        if (index != -1) tabs[index] = tab;
      }
    }
  }
}
