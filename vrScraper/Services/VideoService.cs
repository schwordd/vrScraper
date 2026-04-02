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
    private readonly ReaderWriterLockSlim _videoLock = new ReaderWriterLockSlim();

    private readonly object dblock = new object();
    private long? currentVideoId;
    private DbVideoItem? currentVideo;
    private DateTime? requestedCurrentVideoItem;


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
        .Include(a => a.Engagement)
        .AsNoTracking().AsSplitQuery().ToListAsync();
      _videoLock.EnterWriteLock();
      try { this.videoItems = items; }
      finally { _videoLock.ExitWriteLock(); }
      logger.LogInformation("all video meta data reloaded ({items} items)", items.Count);

    }

    public void Dispose()
    {
      _videoLock.Dispose();
    }

    public Task<List<DbVideoItem>> GetVideoItems()
    {
      _videoLock.EnterReadLock();
      try { return Task.FromResult(videoItems); }
      finally { _videoLock.ExitReadLock(); }
    }

    public Task<DbVideoItem?> GetVideoById(long id)
    {
      _videoLock.EnterReadLock();
      try { return Task.FromResult(videoItems.FirstOrDefault(a => a.Id == id)); }
      finally { _videoLock.ExitReadLock(); }
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


    public void SetPlayedVideo(DbVideoItem vid, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null, string? callerSource = null)
    {
      var source = callerSource ?? caller ?? "unknown";
      logger.LogDebug("[TRACK] SetPlayedVideo(vid={VidId}) from {Source}", vid.Id, source);

      lock (dblock)
      {
        // Record previous video if switching to a different one
        if (this.currentVideoId != null && this.currentVideoId != vid.Id && requestedCurrentVideoItem != null)
        {
          TimeSpan watchedTime = DateTime.UtcNow - (DateTime)requestedCurrentVideoItem;

          using var scope = serviceProvider.CreateScope();
          var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
          var prevVideo = context.VideoItems.Find(currentVideoId);

          if (prevVideo != null)
          {
            // Cap PlayDurationEst at video duration (if known)
            if (prevVideo.Duration > TimeSpan.Zero)
              watchedTime = TimeSpan.FromSeconds(Math.Min(watchedTime.TotalSeconds, prevVideo.Duration.TotalSeconds));

            prevVideo.PlayDurationEst += watchedTime;
            prevVideo.PlayCount += 1;
            context.SaveChanges();

            var savedPlayCount = prevVideo.PlayCount;
            var savedPlayDuration = prevVideo.PlayDurationEst;
            var savedId = prevVideo.Id;

            _videoLock.EnterWriteLock();
            try
            {
              var memVid = this.videoItems.FirstOrDefault(a => a.Id == savedId);
              if (memVid != null)
              {
                memVid.PlayCount = savedPlayCount;
                memVid.PlayDurationEst = savedPlayDuration;
              }
            }
            finally { _videoLock.ExitWriteLock(); }

            logger.LogDebug("[TRACK] RECORD: video {VidId} PlayCount={Count}, PlayDurationEst={Est}",
              savedId, savedPlayCount, savedPlayDuration);
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
          _videoLock.EnterWriteLock();
          try
          {
            var memVid = this.videoItems.FirstOrDefault(v => v.Id == vid.Id);
            if (memVid != null) memVid.LastPlayedUtc = DateTime.UtcNow;
          }
          finally { _videoLock.ExitWriteLock(); }
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

      _videoLock.EnterWriteLock();
      try
      {
        var memVid = this.videoItems.FirstOrDefault(a => a.Id == vid.Id);
        if (memVid != null)
        {
          memVid.Liked = vid.Liked;
          memVid.Disliked = vid.Disliked;
        }
      }
      finally { _videoLock.ExitWriteLock(); }

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
        _videoLock.EnterWriteLock();
        try
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
        finally { _videoLock.ExitWriteLock(); }

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

      _videoLock.EnterWriteLock();
      try
      {
        var memVideo = videoItems.FirstOrDefault(v => v.Id == id);
        if (memVideo != null)
        {
          memVideo.LocalRating = rating;
        }
      }
      finally { _videoLock.ExitWriteLock(); }

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
      _videoLock.EnterWriteLock();
      try
      {
        var memVideo = videoItems.FirstOrDefault(v => v.Id == id);
        if (memVideo != null)
        {
          memVideo.ErrorCount = video.ErrorCount;
        }
      }
      finally { _videoLock.ExitWriteLock(); }

      logger.LogWarning("Fehler für Video-ID {0} erhöht auf {1}", id, video.ErrorCount);
      return true;
    }

    public async Task DeleteVideo(long id)
    {
      DbVideoItem? video;
      _videoLock.EnterReadLock();
      try { video = videoItems.FirstOrDefault(a => a.Id == id); }
      finally { _videoLock.ExitReadLock(); }
      if (video == null) return;

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      context.VideoItems.Remove(video);
      await context.SaveChangesAsync();

      _videoLock.EnterWriteLock();
      try { videoItems.RemoveAll(v => v.Id == id); }
      finally { _videoLock.ExitWriteLock(); }

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
          // Cap PlayDurationEst at video duration (if known)
          if (video.Duration > TimeSpan.Zero)
            watchedTime = TimeSpan.FromSeconds(Math.Min(watchedTime.TotalSeconds, video.Duration.TotalSeconds));

          video.PlayDurationEst += watchedTime;
          video.PlayCount += 1;
          context.SaveChanges();

          // Aktualisiere In-Memory-Cache
          _videoLock.EnterWriteLock();
          try
          {
            var memVid = this.videoItems.FirstOrDefault(v => v.Id == currentVideoId);
            if (memVid != null)
            {
              memVid.PlayCount = video.PlayCount;
              memVid.PlayDurationEst = video.PlayDurationEst;
            }
          }
          finally { _videoLock.ExitWriteLock(); }

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
