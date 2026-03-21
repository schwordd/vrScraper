using vrScraper.Controllers.Models.HereSphere;
using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using vrScraper.Controllers.@base;

namespace vrScraper.Controllers
{
  [Route("[controller]")]
  [ApiController]
  public class HeresphereController(ILogger<HeresphereController> logger, IEpornerScraper scraper, VrScraperContext context, IVideoService videoService, ISettingService settings, ITabFilteringService tabFilteringService) : VrScraperBaseController
  {
    // Post: <HeresphereController>
    [HttpPost]
    [Produces("application/json")]
    public async Task<dynamic> Post()
    {
      logger.LogInformation("HereSphere get Lists");
      var allItems = await videoService.GetVideoItems();

      //global tag blacklist
      var setting = await settings.GetSetting("TagBlacklist");
      var globalBlackList = JsonConvert.DeserializeObject<List<string>>(setting?.Value ?? "[]") ?? new List<string>();

      var filteredTabs = await tabFilteringService.GetFilteredTabVideos(allItems, globalBlackList);

      var tabs = filteredTabs.Select(tab => new
      {
        name = tab.Name,
        list = tab.Videos.Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList()
      }).ToList();

      Response.Headers.Append("HereSphere-JSON-Version", "1");

      return new
      {
        access = 1,
        banner = new
        {
          image = $"{BaseUrl}/logo1.png",
          link = $"{BaseUrl}/heresphere"
        },
        library = tabs
      };

    }

    [HttpPost]
    [Produces("application/json")]
    [Route("scan")]
    public async Task<dynamic> Scan()
    {
      logger.LogInformation("HereSphere scan request");
      Response.Headers.Append("HereSphere-JSON-Version", "1");

      var allItems = await videoService.GetVideoItems();

      var setting = await settings.GetSetting("TagBlacklist");
      var globalBlackList = JsonConvert.DeserializeObject<List<string>>(setting?.Value ?? "[]") ?? new List<string>();
      allItems = allItems.Where(item => !item.Tags.Exists(a => globalBlackList.Any(b => b == a.Name))).ToList();

      var scanData = allItems.Select(item => new
      {
        link = $"{BaseUrl}/heresphere/{item.Id}",
        title = item.Title,
        dateReleased = item.AddedUTC?.ToString("yyyy-MM-dd") ?? "",
        dateAdded = item.AddedUTC?.ToString("yyyy-MM-dd") ?? "",
        duration = item.Duration.TotalMilliseconds,
        rating = ScaleValue(item.SiteRating, 0, 1, 0, 5),
        favorites = item.Liked ? 1 : 0,
        comments = 0,
        isFavorite = item.Liked,
        tags = item.Tags.Select(t => new { name = t.Name, start = 0.0d, end = 0.0d, track = 0 })
            .Concat(item.Stars.Select(s => new { name = $"Talent:{s.Name}", start = 0.0d, end = 0.0d, track = 1 }))
      });

      return new { scanData };
    }

    [HttpPost]
    [Produces("application/json")]
    [Route("{videoId}")]
    public async Task<dynamic> Detail(int videoId, HereSphereGetDetailsModel model)
    {
      Response.Headers.Append("HereSphere-JSON-Version", "1");

      logger.LogInformation("Detail query for {videoId}", videoId);

      var foundVideo = await videoService.GetVideoById(videoId);
      if (foundVideo == null) return NotFound();

      // Handle deleteFile request
      if (model.DeleteFile == true)
      {
        logger.LogInformation("HereSphere delete request for video {videoId}", videoId);
        await videoService.DeleteVideo(foundVideo.Id);
        return Ok();
      }

      VideoSource? source = null;

      if (model.NeedsMediaSource)
      {
        source = await scraper.GetSource(foundVideo, context);

        if (source == null)
        {
          logger.LogInformation("Unable to get source for {videoId}", videoId);
          return NotFound();
        }

        videoService.SetPlayedVideo(foundVideo);
      }

      if (model.IsFavorite.HasValue)
      {
        videoService.FavVideo(foundVideo);
      }

      // Handle rating write-back
      if (model.Rating.HasValue)
      {
        foundVideo.LocalRating = model.Rating.Value / 5.0;
        await videoService.UpdateVideoRating(foundVideo.Id, foundVideo.LocalRating.Value);
        logger.LogInformation("HereSphere rating update for video {videoId}: {rating}", videoId, foundVideo.LocalRating);
      }

      var stars = foundVideo.Stars.Select(s => new { name = $"Talent:{s.Name}", start = 0.0d, end = 0.0d, track = 1 }).ToList();
      var tags = foundVideo.Tags.Select(t => new { name = t.Name, start = 0.0d, end = 0.0d, track = 0 }).ToList();

      var dateStr = foundVideo.AddedUTC?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");

      return new
      {
        access = 1,
        title = foundVideo.Title,
        description = "",
        thumbnailImage = $"{foundVideo.Thumbnail}",
        dateReleased = dateStr,
        dateAdded = dateStr,
        duration = foundVideo.Duration.TotalMilliseconds,
        rating = ScaleValue(foundVideo.LocalRating ?? foundVideo.SiteRating, 0, 1, 0, 5),
        favorites = foundVideo.Liked ? 1 : 0,
        comments = 0,
        isFavorite = foundVideo.Liked,
        projection = "equirectangular",
        stereo = "sbs",
        isEyeSwapped = false,
        fov = 180.0,
        lens = "Linear",
        cameraIPD = 6.5,
        scripts = Array.Empty<string>(),
        subttitles = Array.Empty<string>(),
        tags = tags.Concat(stars),
        media = source == null ? new List<dynamic>() :
        [
           new {
                  name = "h264",
                  sources = new List<dynamic>() {
                  new
                  {
                    width = source.Resolution / 9 * 16,
                    height = source.Resolution,
                    url = source.Src
                  }
                }
           }
        ],
        writeFavorite = true,
        writeRating = true,
        writeTags = false,
        writeHSP = false
      };
    }

    static double ScaleValue(double? value, double fromMin, double fromMax, double toMin, double toMax)
    {
      if (!value.HasValue) return 0.0d;

      // Skalierung
      return ((double)value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
    }
  }
}
