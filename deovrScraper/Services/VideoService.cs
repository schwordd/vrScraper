using deovrScraper.DB;
using deovrScraper.DB.Models;
using deovrScraper.Services.ParsingModels;
using Microsoft.EntityFrameworkCore;

namespace deovrScraper.Services
{
  public class VideoService(ILogger<VideoService> logger, IServiceProvider serviceProvider) : IVideoService
  {
    private List<DbVideoItem> videoItems = new List<DbVideoItem>();

    public async Task Initialize()
    {
      logger.LogInformation("loading all video meta data");
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<DeovrScraperContext>();
      this.videoItems = await context.VideoItems.Include(a => a.Tags).Include(a => a.Stars).AsNoTracking().AsSplitQuery().ToListAsync();
      logger.LogInformation("all video meta data loaded ({items} items)", this.videoItems.Count);
    }

    public Task<List<DbVideoItem>> GetVideoItems()
    {
      return Task.FromResult(videoItems);
    }

    public async Task<List<(DbTag Tag, long Count)>> GetTagInfos()
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<DeovrScraperContext>();

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
      var context = scope.ServiceProvider.GetRequiredService<DeovrScraperContext>();

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
  }
}
