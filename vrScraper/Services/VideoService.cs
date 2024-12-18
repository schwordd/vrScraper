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

  public class VideoService(ILogger<VideoService> logger, IServiceProvider serviceProvider) : IVideoService
  {
    private List<DbVideoItem> videoItems = new List<DbVideoItem>();

    private object dblock = new object();
    private long? currentVideoId;
    private DbVideoItem? currentVideo;
    private DateTime? requestedCurrentVideoItem;

    private object listLock = new object();
    List<(long Id, DateTime Requested)> Requests = new List<(long Id, DateTime Requested)>();
    private bool nextRecordValid = false;

    public DbVideoItem? CurrentLiveVideo => this.currentVideo;

    public delegate void LiveVideoChanged(object sender, VideoChangedEventArgs e);
    public event LiveVideoChanged? OnLiveVideoChanged;

    public async Task Initialize()
    {
      logger.LogInformation("loading all video meta data");
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      this.videoItems = await context.VideoItems.Include(a => a.Tags).Include(a => a.Stars).AsNoTracking().AsSplitQuery().ToListAsync();
      logger.LogInformation("all video meta data loaded ({items} items)", this.videoItems.Count);

      new Task(() =>
      {
        while (true)
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
              //unchanged
            }

            if (count > 0)
              logger.LogInformation("Removed {0} from list. Next record is {1}", count, nextRecordValid ? "valid" : "invalid");

          }
          Thread.Sleep(2000);
        }
      }).Start();
    }

    public Task<List<DbVideoItem>> GetVideoItems()
    {
      return Task.FromResult(videoItems);
    }

    public Task<DbVideoItem> GetVideoById(long id)
    {
      return Task.FromResult(videoItems.Where(a => a.Id == id).Single());
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

              var memVid = this.GetVideoById(currentVideo.Id).GetAwaiter().GetResult();
              memVid.PlayCount = currentVideo.PlayCount;
              memVid.PlayDurationEst = currentVideo.PlayDurationEst;

              logger.LogInformation("Added PlayCount {0} and PlayDuration {1} for video {2}", currentVideo.PlayCount, currentVideo.PlayDurationEst, currentVideo.Id);
            }
          }
        }

        this.currentVideoId = vid.Id;
        this.currentVideo = vid;

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
        vid.Favorite = false;
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
        vid.Favorite = false;
      }

      return UpdateLikeDisklikeFav(vid);

    }

    public DbVideoItem FavVideo(DbVideoItem vid)
    {
      vid.Favorite = !vid.Favorite;

      if (vid.Favorite == true)
      {
        vid.Disliked = false;
        vid.Liked = true;
      }

      return UpdateLikeDisklikeFav(vid);
    }

    private DbVideoItem UpdateLikeDisklikeFav(DbVideoItem vid)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var dbVid = context.VideoItems.Where(a => a.Id == vid.Id).Single();
      var memVid = this.videoItems.Where(a => a.Id == vid.Id).Single();

      dbVid.Liked = vid.Liked;
      dbVid.Disliked = vid.Disliked;
      dbVid.Favorite = vid.Favorite;

      context.SaveChanges();

      memVid.Liked = vid.Liked;
      memVid.Disliked = vid.Disliked;
      memVid.Favorite = vid.Favorite;

      return vid;
    }


  }
}
