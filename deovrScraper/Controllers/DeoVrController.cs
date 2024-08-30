using deovrScraper.DB;
using deovrScraper.DB.Models;
using deovrScraper.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace deovrScraper.Controllers
{
  [Route("[controller]")]
  [ApiController]
  public class DeoVrController(ILogger<DeoVrController> logger, IEpornerScraper scraper, DeovrScraperContext context, IVideoService videoService, IConfiguration config) : ControllerBase
  {
    // GET: <DeoVrController>
    [HttpGet]
    [Produces("application/json")]
    public async Task<dynamic> Get()
    {
      var tabs = new List<(string Name, List<dynamic> List)>();
      var allItems = await videoService.GetVideoItems();

      var tabConfigs = await context.Tabs.Where(t => t.Active).OrderBy(t => t.Order).ToListAsync();
      tabConfigs.ForEach(t =>
      {
        switch (t.Type)
        {
          case "DEFAULT":
            if (t.Name == "Latest")
            {
              var list1 = allItems.OrderByDescending(v => Convert.ToInt32(v.SiteVideoId)).Take(500).Select(item => new
              {
                title = item.Title,
                videoLength = (int)(item.Duration.TotalSeconds),
                thumbnailUrl = $"{item.Thumbnail}",
                video_url = $"http://{config["Ip"]}:{config["Port"]}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list1));
            }
            else if (t.Name == "Rating")
            {
              var list2 = allItems.OrderByDescending(v => v.Rating).ThenByDescending(v => v.Views).Take(500).Select(item => new
              {
                title = item.Title,
                videoLength = (int)(item.Duration.TotalSeconds),
                thumbnailUrl = $"{item.Thumbnail}",
                video_url = $"http://{config["Ip"]}:{config["Port"]}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list2));
            }

            break;

          case "CUSTOM":

            var matchingItems = allItems.AsQueryable();

            var tagsWL = JsonConvert.DeserializeObject<List<string>>(t.TagWhitelist);
            var tagsBL = JsonConvert.DeserializeObject<List<string>>(t.TagBlacklist);
            var acctressWL = JsonConvert.DeserializeObject<List<string>>(t.ActressWhitelist);
            var acctressBL = JsonConvert.DeserializeObject<List<string>>(t.ActressBlacklist);
            var videoWl = JsonConvert.DeserializeObject<List<string>>(t.VideoWhitelist);
            var videoBl = JsonConvert.DeserializeObject<List<string>>(t.VideoBlacklist);

            foreach (var item in tagsWL!)
              matchingItems = matchingItems.Where(a => a.Tags.Any(t => t.Name == item));

            foreach (var item in tagsBL!)
              matchingItems = matchingItems.Where(a => a.Tags.Any(t => t.Name == item) == false);

            foreach (var item in acctressWL!)
              matchingItems = matchingItems.Where(a => a.Stars.Any(t => t.Name == item));

            foreach (var item in acctressBL!)
              matchingItems = matchingItems.Where(a => a.Stars.Any(t => t.Name == item) == false);

            foreach (var item in videoWl!)
              matchingItems = matchingItems.Where(a => a.Id == Convert.ToInt64(item));

            foreach (var item in videoWl!)
              matchingItems = matchingItems.Where(a => a.Id != Convert.ToInt64(item));

            var list = matchingItems.OrderByDescending(a => Convert.ToInt32(a.SiteVideoId)).Take(500).Select(item => new
            {
              title = item.Title,
              videoLength = (int)(item.Duration.TotalSeconds),
              thumbnailUrl = $"{item.Thumbnail}",
              video_url = $"http://{config["Ip"]}:{config["Port"]}/deovr/detail/{item.Id}"
            }).ToList<dynamic>();

            tabs.Add((t.Name, list));

            break;
        }
      });

      return new
      {
        Scenes = tabs.Select(a => new { Name = a.Name, list = a.List })
      };
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("detail/{videoId}")]
    public async Task<dynamic> Detail(int videoId)
    {
      logger.LogInformation("Detail query for {videoId}", videoId);

      var foundVideo = await context.VideoItems.Where(v => v.Id == videoId).FirstOrDefaultAsync();
      if (foundVideo == null) return NotFound();

      VideoSource? source = null;
      source = await scraper.GetSource(foundVideo, context);

      if (source == null)
        return NotFound();

      return new
      {
        id = foundVideo.Id,
        is3d = true,
        title = foundVideo.Title,
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
        isFavorite = false,
        isScripted = false,
        isWatchlist = false,
        date = 1715731200
      };
    }
  }
}
