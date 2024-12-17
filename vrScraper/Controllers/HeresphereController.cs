using vrScraper.Controllers.Models.HereSphere;
using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using vrScraper.Controllers.@base;

namespace vrScraper.Controllers
{
  [Route("[controller]")]
  [ApiController]
  public class HeresphereController(ILogger<HeresphereController> logger, IEpornerScraper scraper, VrScraperContext context, IVideoService videoService, ISettingService settings) : VrScraperBaseController
  {
    // Post: <HeresphereController>
    [HttpPost]
    [Produces("application/json")]
    public async Task<dynamic> Post()
    {
      logger.LogInformation("HereSphere get Lists");
      var tabs = new List<(string Name, List<string> List)>();
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
              var list1 = allItems.OrderByDescending(v => Convert.ToInt32(v.SiteVideoId)).Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList<string>();
              tabs.Add((t.Name, list1));
            }
            else if (t.Name == "Rating")
            {
              var list2 = allItems.OrderByDescending(v => v.SiteRating).ThenByDescending(v => v.Views).Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList<string>();
              tabs.Add((t.Name, list2));
            }
            else if (t.Name == "Random")
            {
              var list3 = allItems.OrderBy(a => Guid.NewGuid()).Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList<string>();

              tabs.Add((t.Name, list3));
            }
            else if (t.Name == "Fav")
            {
              var list4 = allItems.Where(x => x.Favorite == true).OrderBy(a => Guid.NewGuid()).Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList<string>();

              tabs.Add((t.Name, list4));
            }
            else if (t.Name == "Liked")
            {
              var list5 = allItems.Where(x => x.Liked == true).OrderBy(a => Guid.NewGuid()).Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList<string>();

              tabs.Add((t.Name, list5));
            }
            else if (t.Name == "Playtime")
            {
              var list6 = allItems.OrderByDescending(a => a.PlayDurationEst).Where(a => a.PlayDurationEst > TimeSpan.FromSeconds(20)).Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList<string>();

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
                  ).Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList<string>();

              tabs.Add((t.Name, list7));
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

                .Select(item => $"{BaseUrl}/heresphere/{item.Id}").ToList<string>();

            tabs.Add((t.Name, list));

            break;
        }
      });

      Response.Headers.Append("HereSphere-JSON-Version", "1");

      return new
      {
        access = 1,
        banner = new
        {
          image = $"{BaseUrl}/logo.png",
          link = $"{BaseUrl}/heresphere"
        },
        library = tabs.Select(a => new { name = a.Name, list = a.List })
      };

    }

    [HttpPost]
    [Produces("application/json")]
    [Route("{videoId}")]
    public async Task<dynamic> Detail(int videoId, HereSphereGetDetailsModel model)
    {
      Response.Headers.Append("HereSphere-JSON-Version", "1");

      logger.LogInformation("Detail query for {videoId}", videoId);

      //logger.LogInformation(JsonConvert.SerializeObject(model));

      var foundVideo = await videoService.GetVideoById(videoId);
      if (foundVideo == null) return NotFound();

      VideoSource? source = null;

      if (model.NeedsMediaSource)
      {
        videoService.SetPlayedVideo(foundVideo);
        source = await scraper.GetSource(foundVideo, context);

        if (source == null)
          return NotFound();
      }

      if (model.IsFavorite.HasValue)
      {
        videoService.FavVideo(foundVideo);
      }

      var stars = foundVideo.Stars.Select(s => new { name = $"Talent:{s.Name}", start = 0.0d, end = 0.0d, track = 1 }).ToList();
      var tags = foundVideo.Tags.Select(t => new { name = t.Name, start = 0.0d, end = 0.0d, track = 0 }).ToList();

      return new
      {
        access = 1,
        title = foundVideo.Title,
        description = "dummy",
        thumbnailImage = $"{foundVideo.Thumbnail}",
        dateReleased = "2022-03-10",
        dateAdded = "2022-06-16",
        duration = foundVideo.Duration.TotalMilliseconds,
        rating = ScaleValue(foundVideo.SiteRating, 0, 1, 0, 5),
        favorites = foundVideo.Favorite ? 1 : 0,
        comments = 0,
        isFavorite = foundVideo.Favorite,
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
        writeRating = false, //TODO
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
