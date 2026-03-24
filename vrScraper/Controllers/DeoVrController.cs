using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using vrScraper.Controllers.@base;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace vrScraper.Controllers
{
  [Route("[controller]")]
  [ApiController]
  [AllowAnonymous]
  public class DeoVrController(ILogger<DeoVrController> logger, IScraperRegistry scraperRegistry, VrScraperContext context, IVideoService videoService, ISettingService settings, ITabFilteringService tabFilteringService) : VrScraperBaseController
  {
    // GET: <DeoVrController>
    [HttpGet]
    [Produces("application/json")]
    public async Task<dynamic> Get()
    {
      var allItems = await videoService.GetVideoItems();

      //global tag blacklist
      var setting = await settings.GetSetting("TagBlacklist");
      var globalBlackList = JsonConvert.DeserializeObject<List<string>>(setting?.Value ?? "[]") ?? new List<string>();

      var filteredTabs = await tabFilteringService.GetFilteredTabVideos(allItems, globalBlackList);

      var tabs = filteredTabs.Select(tab => new
      {
        tab.Name,
        list = (tab.Name == "Latest Unwatched"
            ? tab.Videos
            : tab.Videos.Take(500))
          .Select(item => new
          {
            title = item.Title,
            videoLength = (int)(item.Duration.TotalSeconds),
            thumbnailUrl = $"{item.Thumbnail}",
            video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
          }).ToList<dynamic>()
      }).ToList();

      return new
      {
        Scenes = tabs.Select(a => new { a.Name, list = a.list })
      };
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("detail/{videoId}")]
    public async Task<dynamic> Detail(int videoId)
    {
      logger.LogInformation("Detail query for {videoId}", videoId);

      var foundVideo = await videoService.GetVideoById(videoId);
      if (foundVideo == null) return NotFound();

      videoService.SetPlayedVideo(foundVideo);

      var scraper = scraperRegistry.GetScraperForSite(foundVideo.Site);
      if (scraper == null) return NotFound();

      VideoSource? source;
      source = await scraper.GetSource(foundVideo, context);

      if (source == null)
        return NotFound();

      return new
      {
        id = foundVideo.Id,
        is3d = true,
        title = foundVideo.NormalizedTitle ?? foundVideo.Title,
        thumbnailUrl = $"{foundVideo.Thumbnail}",
        authorized = 1,
        videoLength = (int)foundVideo.Duration.TotalSeconds,
        encodings = new List<dynamic>()
        {
          new {
            name = "h264",
            videoSources = new List<dynamic>() {
              new
              {
                source.Resolution,
                Url = source.Src
              }

            }
          }
        },
        screenType = "dome",
        stereoMode = "sbs",
        fullVideoReady = true,
        fullAccess = true,
        videoThumbnail = "",
        isFavorite = foundVideo.Liked,
        isScripted = false,
        isWatchlist = false,
        date = 1715731200
      };
    }
  }
}
