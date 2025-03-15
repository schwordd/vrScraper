using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;
using vrScraper.Controllers.@base;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace vrScraper.Controllers
{
  [Route("[controller]")]
  [ApiController]
  public class DeoVrController(ILogger<DeoVrController> logger, IEpornerScraper scraper, VrScraperContext context, IVideoService videoService, ISettingService settings) : VrScraperBaseController
  {
    // GET: <DeoVrController>
    [HttpGet]
    [Produces("application/json")]
    public async Task<dynamic> Get()
    {
      var tabs = new List<(string Name, List<dynamic> List)>();
      var allItems = await videoService.GetVideoItems();

      //global tag blacklist
      var setting = await settings.GetSetting("TagBlacklist");
      var globalBlackList = JsonConvert.DeserializeObject<List<string>>(setting.Value);
      allItems = allItems.Where(item => !item.Tags.Exists(a => globalBlackList!.Any(b => b == a.Name))).ToList();

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
                video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list1));
            }
            else if (t.Name == "Rating")
            {
              var list2 = allItems.OrderByDescending(v => v.SiteRating).ThenByDescending(v => v.Views).Take(500).Select(item => new
              {
                title = item.Title,
                videoLength = (int)(item.Duration.TotalSeconds),
                thumbnailUrl = $"{item.Thumbnail}",
                video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list2));
            }
            else if (t.Name == "Random")
            {
              var list3 = allItems.OrderBy(a => Guid.NewGuid()).Take(500).Select(item => new
              {
                title = item.Title,
                videoLength = (int)(item.Duration.TotalSeconds),
                thumbnailUrl = $"{item.Thumbnail}",
                video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list3));
            }
            else if (t.Name == "Fav")
            {
              var list4 = allItems.Where(x => x.Favorite == true).OrderBy(a => Guid.NewGuid()).Take(500).Select(item => new
              {
                title = item.Title,
                videoLength = (int)(item.Duration.TotalSeconds),
                thumbnailUrl = $"{item.Thumbnail}",
                video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list4));
            }
            else if (t.Name == "Liked")
            {
              var list5 = allItems.Where(x => x.Liked == true).OrderBy(a => Guid.NewGuid()).Take(500).Select(item => new
              {
                title = item.Title,
                videoLength = (int)(item.Duration.TotalSeconds),
                thumbnailUrl = $"{item.Thumbnail}",
                video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list5));
            }
            else if (t.Name == "Playtime")
            {
              var list6 = allItems.OrderByDescending(a => a.PlayDurationEst).Where(a => a.PlayDurationEst > TimeSpan.FromSeconds(20)).Take(500).Select(item => new
              {
                title = item.Title,
                videoLength = (int)(item.Duration.TotalSeconds),
                thumbnailUrl = $"{item.Thumbnail}",
                video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list6));
            }
            else if (t.Name == "Unwatched")
            {
              var allUnwatched = allItems.Where(x => x.PlayCount == 0);
              var k1 = 16000; // Tuning-Parameter, der angepasst werden kann
              var averageRating1 = allItems.Average(a => a.SiteRating); // Berechnung des durchschnittlichen Ratings
              var averageViews1 = allItems.Average(a => a.Views); // Berechnung des durchschnittlichen Views

              var list7 = allUnwatched.OrderByDescending(a =>
                      ((a.Views!.Value / (double)(a.Views.Value + k1)) * a.SiteRating!.Value) +
                      ((k1 / (double)(a.Views.Value + k1)) * averageRating1)
                  ).Take(500).Select(item => new
                  {
                    title = item.Title,
                    videoLength = (int)(item.Duration.TotalSeconds),
                    thumbnailUrl = $"{item.Thumbnail}",
                    video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
                  }).ToList<dynamic>();

              tabs.Add((t.Name, list7));
            }
            else if (t.Name == "Latest Unwatched")
            {
              var allUnwatched = allItems.Where(x => x.PlayCount == 0);
              var list8 = allUnwatched.OrderByDescending(v => Convert.ToInt32(v.SiteVideoId)).Select(item => new
              {
                title = item.Title,
                videoLength = (int)(item.Duration.TotalSeconds),
                thumbnailUrl = $"{item.Thumbnail}",
                video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
              }).ToList<dynamic>();

              tabs.Add((t.Name, list8));
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



            var k = 16000; // Tuning-Parameter, der angepasst werden kann
            var averageRating = matchingItems.Average(a => a.SiteRating); // Berechnung des durchschnittlichen Ratings
            var averageViews = matchingItems.Average(a => a.Views); // Berechnung des durchschnittlichen Views

            var list = matchingItems
                .OrderByDescending(a =>
                    ((a.Views!.Value / (double)(a.Views.Value + k)) * a.SiteRating!.Value) +
                    ((k / (double)(a.Views.Value + k)) * averageRating)
                )
                .Take(500)
                .Select(item => new
                {
                  title = item.Title,
                  videoLength = (int)(item.Duration.TotalSeconds),
                  thumbnailUrl = $"{item.Thumbnail}",
                  video_url = $"{BaseUrl}/deovr/detail/{item.Id}"
                })
                .ToList<dynamic>();

            tabs.Add((t.Name, list));

            break;
        }
      });

      return new
      {
        Scenes = tabs.Select(a => new { a.Name, list = a.List })
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

      VideoSource? source;
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
