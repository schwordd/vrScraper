using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class VideoChangedEventArgs : EventArgs
  {
    public DbVideoItem Video { get; private set; }
    public VideoChangedEventArgs(DbVideoItem video)
    {
      Video = video;
    }
  }

  public class VideoService(ILogger<VideoService> logger, IServiceProvider serviceProvider) : IVideoService, IDisposable
  {
    private List<DbVideoItem> videoItems = new List<DbVideoItem>();
    private readonly object _videoLock = new object();

    private object dblock = new object();
    private long? currentVideoId;
    private DbVideoItem? currentVideo;
    private DateTime? requestedCurrentVideoItem;

    private object listLock = new object();
    List<(long Id, DateTime Requested)> Requests = new List<(long Id, DateTime Requested)>();
    private bool nextRecordValid = false;
    private int _backgroundTaskStarted = 0;
    private CancellationTokenSource? _backgroundCts;

    public DbVideoItem? CurrentLiveVideo => this.currentVideo;

    public delegate void LiveVideoChanged(object sender, VideoChangedEventArgs e);
    public event LiveVideoChanged? OnLiveVideoChanged;

    public async Task Initialize()
    {
      await ReloadVideos();
    }

    public async Task ReloadVideos()
    {
      logger.LogInformation("reloading all video meta data");
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var items = await context.VideoItems
        .Include(a => a.Tags).Include(a => a.Stars)
        .Include(a => a.VideoStars).Include(a => a.VideoTags)
        .AsNoTracking().AsSplitQuery().ToListAsync();
      lock (_videoLock)
      {
        this.videoItems = items;
      }
      logger.LogInformation("all video meta data reloaded ({items} items)", items.Count);

      if (Interlocked.CompareExchange(ref _backgroundTaskStarted, 1, 0) == 0)
      {
        StartBackgroundTask();
      }
    }

    private void StartBackgroundTask()
    {
      _backgroundCts = new CancellationTokenSource();
      var token = _backgroundCts.Token;

      _ = Task.Run(async () =>
      {
        try
        {
          while (!token.IsCancellationRequested)
          {
            lock (listLock)
            {
              var count = this.Requests.RemoveAll(x => (DateTime.UtcNow - x.Requested > TimeSpan.FromSeconds(3)));
              if (count > 1)
                nextRecordValid = false;
              else if (count == 1)
                nextRecordValid = true;
              else if (count == 0)
              {
                nextRecordValid = true;
              }

              if (count > 0)
                logger.LogInformation("Removed {0} from list. Next record is {1}", count, nextRecordValid ? "valid" : "invalid");
            }
            await Task.Delay(2000, token);
          }
        }
        catch (OperationCanceledException)
        {
          // Expected on shutdown
        }
      }, token);
    }

    public void Dispose()
    {
      _backgroundCts?.Cancel();
      _backgroundCts?.Dispose();
    }

    public Task<List<DbVideoItem>> GetVideoItems()
    {
      lock (_videoLock)
      {
        return Task.FromResult(videoItems);
      }
    }

    public Task<DbVideoItem?> GetVideoById(long id)
    {
      lock (_videoLock)
      {
        return Task.FromResult(videoItems.Where(a => a.Id == id).FirstOrDefault());
      }
    }

    public async Task<List<(DbTag Tag, long Count)>> GetTagInfos()
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var tagCounts = await context.Tags
          .Select(tag => new
          {
            Tag = tag,
            VideoCount = (long)tag.Videos.Count
          })
          .OrderByDescending(t => t.VideoCount)
          .ToListAsync();

      return tagCounts.Select(a => (a.Tag, a.VideoCount)).ToList();
    }

    public async Task<List<(DbStar Star, long Count)>> GetActorInfos()
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var starCounts = await context.Stars
          .Select(star => new
          {
            Star = star,
            VideoCount = (long)star.Videos.Count
          })
          .OrderByDescending(t => t.VideoCount)
          .ToListAsync();

      return starCounts.Select(a => (a.Star, a.VideoCount)).ToList();
    }


    public void SetPlayedVideo(DbVideoItem vid)
    {
      lock (dblock)
      {
        lock (listLock)
        {
          this.Requests.Add((vid.Id, DateTime.UtcNow));

          logger.LogInformation("Items on List {0}", this.Requests.Count);

          if (this.Requests.Any(a => a.Id == currentVideoId))
          {
            logger.LogInformation("Skipping stats record for {0}. Item still in list", currentVideoId);
          }
          else if (nextRecordValid && this.currentVideoId != null && requestedCurrentVideoItem != null)
          {
            TimeSpan watchedTime = DateTime.UtcNow - (DateTime)requestedCurrentVideoItem;

            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
            var currentVideo = context.VideoItems.Find(currentVideoId);

            if (currentVideo != null)
            {
              currentVideo.PlayDurationEst += watchedTime;
              currentVideo.PlayCount += 1;
              context.SaveChanges();

              lock (_videoLock)
              {
                var memVid = this.videoItems.Where(a => a.Id == currentVideo.Id).FirstOrDefault();
                if (memVid != null)
                {
                  memVid.PlayCount = currentVideo.PlayCount;
                  memVid.PlayDurationEst = currentVideo.PlayDurationEst;
                }
              }

              logger.LogInformation("Added PlayCount {0} and PlayDuration {1} for video {2}", currentVideo.PlayCount, currentVideo.PlayDurationEst, currentVideo.Id);
            }
          }
        }

        this.currentVideoId = vid.Id;
        this.currentVideo = vid;

        // Update LastPlayedUtc
        {
          using var scope2 = serviceProvider.CreateScope();
          var ctx2 = scope2.ServiceProvider.GetRequiredService<VrScraperContext>();
          var dbVid = ctx2.VideoItems.Find(vid.Id);
          if (dbVid != null) { dbVid.LastPlayedUtc = DateTime.UtcNow; ctx2.SaveChanges(); }
          lock (_videoLock)
          {
            var memVid = this.videoItems.FirstOrDefault(v => v.Id == vid.Id);
            if (memVid != null) memVid.LastPlayedUtc = DateTime.UtcNow;
          }
        }

        if (OnLiveVideoChanged != null)
        {
          OnLiveVideoChanged(this, new VideoChangedEventArgs(vid));
        }

        this.requestedCurrentVideoItem = DateTime.UtcNow;
      }
    }

    public DbVideoItem LikeVideo(DbVideoItem vid)
    {
      vid.Liked = !vid.Liked;

      if (vid.Liked == false)
      {
        // Keine Aktion mehr für Favorite erforderlich
      }

      if (vid.Liked == true)
      {
        vid.Disliked = false;
      }

      return UpdateLikeDisklikeFav(vid);
    }

    public DbVideoItem DislikeVideo(DbVideoItem vid)
    {
      vid.Disliked = !vid.Disliked;

      if (vid.Disliked == true)
      {
        vid.Liked = false;
        // Keine Aktion mehr für Favorite erforderlich
      }

      return UpdateLikeDisklikeFav(vid);
    }

    public DbVideoItem FavVideo(DbVideoItem vid)
    {
      // Statt Favorite umzuschalten, setzen wir einfach Liked auf true
      vid.Liked = true;
      vid.Disliked = false;

      return UpdateLikeDisklikeFav(vid);
    }

    private DbVideoItem UpdateLikeDisklikeFav(DbVideoItem vid)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var dbVid = context.VideoItems.Where(a => a.Id == vid.Id).Single();

      dbVid.Liked = vid.Liked;
      dbVid.Disliked = vid.Disliked;

      context.SaveChanges();

      lock (_videoLock)
      {
        var memVid = this.videoItems.Where(a => a.Id == vid.Id).FirstOrDefault();
        if (memVid != null)
        {
          memVid.Liked = vid.Liked;
          memVid.Disliked = vid.Disliked;
        }
      }

      return vid;
    }

    public async Task<bool> UpdateVideoLikeStatus(long id, bool liked)
    {
      try
      {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
        var dbVid = await context.VideoItems.FirstOrDefaultAsync(a => a.Id == id);

        if (dbVid == null)
        {
          return false;
        }

        dbVid.Liked = liked;

        // Wenn ein Video geliked wird, kann es nicht gleichzeitig disliked sein
        if (liked)
        {
          dbVid.Disliked = false;
        }

        await context.SaveChangesAsync();

        // Aktualisieren des Videos im Speicher-Cache
        lock (_videoLock)
        {
          var memVid = this.videoItems.FirstOrDefault(a => a.Id == id);
          if (memVid != null)
          {
            memVid.Liked = liked;
            if (liked)
            {
              memVid.Disliked = false;
            }
          }
        }

        return true;
      }
      catch (Exception)
      {
        return false;
      }
    }

    public async Task<bool> UpdateVideoRating(long id, double rating)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var video = await context.VideoItems.FindAsync(id);

      if (video == null)
        return false;

      video.LocalRating = rating;
      await context.SaveChangesAsync();

      lock (_videoLock)
      {
        var memVideo = videoItems.FirstOrDefault(v => v.Id == id);
        if (memVideo != null)
        {
          memVideo.LocalRating = rating;
        }
      }

      logger.LogInformation("Rating for video {id} updated to {rating}", id, rating);
      return true;
    }

    public async Task<bool> UpdateVideoErrorCount(long id)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var video = await context.VideoItems.FindAsync(id);

      if (video == null)
        return false;

      if (video.ErrorCount == null)
        video.ErrorCount = 1;
      else
        video.ErrorCount++;

      await context.SaveChangesAsync();

      // Update video in memory
      lock (_videoLock)
      {
        var memVideo = videoItems.FirstOrDefault(v => v.Id == id);
        if (memVideo != null)
        {
          memVideo.ErrorCount = video.ErrorCount;
        }
      }

      logger.LogWarning("Fehler für Video-ID {0} erhöht auf {1}", id, video.ErrorCount);
      return true;
    }

    public async Task DeleteVideo(long id)
    {
      DbVideoItem? video;
      lock (_videoLock)
      {
        video = videoItems.Where(a => a.Id == id).FirstOrDefault();
      }
      if (video == null) return;

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      context.VideoItems.Remove(video);
      await context.SaveChangesAsync();

      // Entferne das Video auch aus dem Cache
      lock (_videoLock)
      {
        videoItems.RemoveAll(v => v.Id == id);
      }

      logger.LogInformation($"Video with ID {id} deleted");
    }

    public DbVideoItem? FinishCurrentPlayback()
    {
      lock (dblock)
      {
        if (currentVideoId == null || requestedCurrentVideoItem == null || currentVideo == null)
        {
          logger.LogInformation("No active playback to finish");
          return null;
        }

        // Berechne Wiedergabezeit
        TimeSpan watchedTime = DateTime.UtcNow - (DateTime)requestedCurrentVideoItem;

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
        var video = context.VideoItems.Find(currentVideoId);

        if (video != null)
        {
          video.PlayDurationEst += watchedTime;
          video.PlayCount += 1;
          context.SaveChanges();

          // Aktualisiere In-Memory-Cache
          lock (_videoLock)
          {
            var memVid = this.videoItems.FirstOrDefault(v => v.Id == currentVideoId);
            if (memVid != null)
            {
              memVid.PlayCount = video.PlayCount;
              memVid.PlayDurationEst = video.PlayDurationEst;
            }
          }

          logger.LogInformation("Finished playback for video {0}. PlayCount: {1}, Total PlayDuration: {2}",
              video.Id, video.PlayCount, video.PlayDurationEst);
        }

        // Speichere Referenz zum beendeten Video
        var finishedVideo = currentVideo;

        // Setze current video state zurück
        currentVideoId = null;
        currentVideo = null;
        requestedCurrentVideoItem = null;

        return finishedVideo;
      }
    }

  }
}
