using Microsoft.EntityFrameworkCore;
using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services.Interfaces;

namespace vrScraper.Services
{
  public class DailySnapshotService : IDailySnapshotService, IHostedService, IDisposable
  {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVideoService _videoService;
    private readonly ILogger<DailySnapshotService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public DailySnapshotService(IServiceScopeFactory scopeFactory, IVideoService videoService, ILogger<DailySnapshotService> logger)
    {
      _scopeFactory = scopeFactory;
      _videoService = videoService;
      _logger = logger;
    }

    public async Task EnsureTodaySnapshot()
    {
      try
      {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var videos = await _videoService.GetVideoItems();
        var total = videos.Count;
        var watched = videos.Count(v => v.PlayCount > 0);
        var unwatched = total - watched;
        var liked = videos.Count(v => v.Liked);

        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
        var existing = await ctx.DailySnapshots.FirstOrDefaultAsync(s => s.SnapshotDate == today);
        if (existing == null)
        {
          ctx.DailySnapshots.Add(new DbDailySnapshot
          {
            SnapshotDate = today,
            TotalCount = total,
            WatchedCount = watched,
            UnwatchedCount = unwatched,
            LikedCount = liked
          });
        }
        else
        {
          existing.TotalCount = total;
          existing.WatchedCount = watched;
          existing.UnwatchedCount = unwatched;
          existing.LikedCount = liked;
        }
        await ctx.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "EnsureTodaySnapshot failed");
      }
    }

    public async Task<List<DbDailySnapshot>> GetLastSnapshots(int days)
    {
      try
      {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
        return await ctx.DailySnapshots
          .Where(s => s.SnapshotDate >= cutoff)
          .OrderBy(s => s.SnapshotDate)
          .ToListAsync();
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "GetLastSnapshots failed");
        return new List<DbDailySnapshot>();
      }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      _loopTask = Task.Run(() => RunLoop(_cts.Token));
      return Task.CompletedTask;
    }

    private async Task RunLoop(CancellationToken ct)
    {
      try { await Task.Delay(TimeSpan.FromSeconds(15), ct); } catch { return; }
      while (!ct.IsCancellationRequested)
      {
        await EnsureTodaySnapshot();
        try { await Task.Delay(TimeSpan.FromMinutes(60), ct); } catch { return; }
      }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
      _cts?.Cancel();
      if (_loopTask != null)
      {
        try { await Task.WhenAny(_loopTask, Task.Delay(2000, cancellationToken)); } catch { }
      }
    }

    public void Dispose() { _cts?.Dispose(); }
  }
}
