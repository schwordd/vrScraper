using HtmlAgilityPack;
using Newtonsoft.Json;
using RestSharp;
using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;
using vrScraper.Services.ParsingModels;

namespace vrScraper.Services
{
  public class EpornerScraper(ILogger<EpornerScraper> logger, IServiceProvider serviceProvider, IVideoService vs) : IEpornerScraper
  {
    public bool ScrapingInProgress => this._scrapingInprogress;

    public string ScrapingStatus => this._scrapingStatus;

    private bool _scrapingInprogress = false;
    private string _scrapingStatus = string.Empty;

    public void Initialize()
    {
      logger.LogInformation("EpornerScraper initialized.");
    }

    public void StartScraping(int start, int count)
    {
      if (this._scrapingInprogress) return;

      this._scrapingInprogress = true;

      Task.Run(async () =>
      {
        await this.ScrapeEporner("https://www.eporner.com/cat/vr-porn", start, count);
        this._scrapingInprogress = false;
        this._scrapingStatus = string.Empty;
      });
    }

    private async Task ScrapeEporner(string url, int startIndex, int pages = 10)
    {
      var totalList = new List<VideoItem>();
      var newInsertions = new List<DbVideoItem>();

      var rnd = new Random();
      var index = startIndex;

      while (true)
      {
        this._scrapingStatus = $"Total items found : {totalList.Count} -  Processing page {index} ...";
        logger.LogInformation(this._scrapingStatus);

        var foundPageItems = await ScrapeSinglePage($"{url}/{index}/");

        if (foundPageItems.Count > 0)
        {
          totalList.AddRange(foundPageItems);
          totalList = totalList.DistinctBy(a => a.VideoId).ToList();
          index++;
        }
        else
        {
          logger.LogInformation($"NO more results on page {index}. Giving up");
          break;
        }

        if (pages != -1 && index > pages)
        {
          logger.LogInformation($"Limit of pages ({pages}) reached. Stopping.");
          break;
        }

        Thread.Sleep(rnd.Next(500, 1000));
      }

      totalList = totalList.Where(v => v.IsVr == true && v.Quality.Contains("4K")).ToList();

      try
      {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
        var site = "eporner.com";

        totalList = totalList.DistinctBy(a => a.VideoId).ToList();

        foreach (var item in totalList)
        {
          if (context.VideoItems.Any(a => a.Site == site && a.SiteVideoId == item.VideoId))
          {
            continue;
          }

          var dbVideoItem = new DB.Models.DbVideoItem()
          {
            //ID --> GENERATED
            Site = "eporner.com",
            SiteVideoId = item.VideoId,
            DataVp = item.DataVp,
            Duration = item.Duration ?? TimeSpan.FromSeconds(0),
            IsVr = item.IsVr,
            Link = item.Link,
            Quality = item.Quality,
            SiteRating = item.Rating,
            Thumbnail = item.Thumbnail,
            Title = item.Title ?? string.Empty,
            Uploader = item.Uploader,
            Views = item.Views,
            ParsedDetails = false,
            AddedUTC = DateTime.UtcNow
          };

          context.VideoItems.Add(dbVideoItem);
          newInsertions.Add(dbVideoItem);
        }

        await context.SaveChangesAsync();

        logger.LogInformation($"{newInsertions.Count} new videos found and added to database");

        logger.LogInformation($"Parsing details (tags and artists) of {newInsertions.Count} new videos...this will take a while");

        for (var i = 0; i < newInsertions.Count; i++)
        {
          this._scrapingStatus = $"Parse Details: {i + 1} / {newInsertions.Count}";
          logger.LogInformation(this._scrapingStatus);
          await ParseDetails(newInsertions[i], context);
          Thread.Sleep(100);
        }

        await vs.Initialize();

        logger.LogInformation($"Scraping complete! Hf");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while scraping.");
      }
    }

    private async Task<List<VideoItem>> ScrapeSinglePage(string url)
    {
      var web = new HtmlWeb();
      var doc = await web.LoadFromWebAsync(url);

      var nodes = doc.DocumentNode.SelectNodes("//div[@class='mb hdy']");
      var videoItems = ParseVideoItems(nodes, "https://www.eporner.com");

      return videoItems;
    }

    public async Task<(VideoPlayerSettings PlayerSettings, List<string> Tags, List<string> Stars)> GetDetails(DbVideoItem item)
    {
      var web = new HtmlWeb();
      var doc = await web.LoadFromWebAsync(item.Link);

      var lines = doc.ParsedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList(); ;
      var videoInfos = lines.Where(a => a.StartsWith("EP.video.player.")).ToList();
      var settings = ParseVideoPlayerSettings(videoInfos);

      var pStars = doc.DocumentNode.SelectNodes("//li[contains(@class, 'vit-pornstar')]");
      var categories = doc.DocumentNode.SelectNodes("//li[contains(@class, 'vit-category')]");

      var stars = new List<string>();
      var tags = new List<string>();

      if (pStars != null)
      {
        foreach (var t in pStars)
        {
          stars.Add(t.InnerText);
        }
      }

      if (categories != null)
      {
        foreach (var t in categories)
        {
          tags.Add(t.InnerText);
        }
      }

      return (settings, tags, stars);
    }

    public async Task<Quality?> GetBestVideoQuality(DbVideoItem item, VideoPlayerSettings settings)
    {
      if (settings == null || settings.Hash == null) return null;

      var b36Hash = ConvertToBase36(settings.Hash);
      var vidId = settings.Vid;

      var client = new RestClient($"https://www.eporner.com");
      var request = new RestRequest($"xhr/video/{vidId}", Method.Get);
      request.AddParameter("hash", b36Hash);
      request.AddParameter("domain", "www.eporner.com");

      var response = await client.ExecuteAsync(request);
      if (response.IsSuccessful && response.Content != null)
      {
        var videoResponse = JsonConvert.DeserializeObject<VideoItemDetails>(response.Content);

        if (videoResponse == null)
          return null;

        return videoResponse.Sources.Mp4.HighestQuality;
      }
      else
      {
        return null;
      }
    }

    public async Task<VideoSource?> GetSource(DbVideoItem video, VrScraperContext context)
    {
      var details = await this.GetDetails(video);
      var quality = await this.GetBestVideoQuality(video, details.PlayerSettings);

      if (quality == null) return null;

      var source = new VideoSource
      {
        Default = quality.Default,
        Resolution = quality.Resolution,
        Src = quality.Src,
        LabelShort = quality.LabelShort,
        Type = quality.Type
      };

      await context.SaveChangesAsync();

      return source;
    }

    public async Task ParseDetails(DbVideoItem video, VrScraperContext context)
    {
      var details = await this.GetDetails(video);

      foreach (var starParsed in details.Stars.Distinct().ToList())
      {
        var star = await context.Stars.Where(s => s.Name == starParsed).FirstOrDefaultAsync();
        if (star == null)
        {
          star = new DbStar() { Name = starParsed };
          context.Stars.Add(star);
          star.Videos = [];
        }

        if (star.Videos == null)
          star.Videos = [];

        star.Videos.Add(video);
      }

      foreach (var tagParsed in details.Tags.Distinct().ToList())
      {
        var tag = await context.Tags.Where(s => s.Name == tagParsed).FirstOrDefaultAsync();
        if (tag == null)
        {
          tag = new DbTag() { Name = tagParsed };
          context.Tags.Add(tag);
        }

        if (tag.Videos == null)
          tag.Videos = [];

        tag.Videos.Add(video);
      }

      video.ParsedDetails = true;
      await context.SaveChangesAsync();
    }

    public async Task ParseMissingInformations()
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var unscrapedItems = await context.VideoItems.Where(a => a.ParsedDetails == false).ToListAsync();
      for (var i = 0; i < unscrapedItems.Count; i++)
      {
        var videoItem = unscrapedItems[i];
        logger.LogInformation(message: $"Parse missing details: {i + 1} / {unscrapedItems.Count}");
        try
        {
          await ParseDetails(unscrapedItems[i], context);
        }
        catch (Exception ex)
        {
          logger.LogWarning($"Error scraping VideoItem {videoItem.Id}");
          logger.LogError(ex.ToString());
        }
      }
    }

    private List<VideoItem> ParseVideoItems(HtmlNodeCollection? nodes, string baseUrl)
    {
      var items = new List<VideoItem>();

      if (nodes == null)
        return items;

      foreach (var node in nodes)
      {
        var titleNode = node.SelectSingleNode(".//p[@class='mbtit']/a");
        var durationNode = node.SelectSingleNode(".//span[@class='mbtim']");
        var ratingNode = node.SelectSingleNode(".//span[@class='mbrate']");
        var viewsNode = node.SelectSingleNode(".//span[@class='mbvie']");
        var uploaderNode = node.SelectSingleNode(".//span[@class='mb-uploader']/a");
        var linkNode = node.SelectSingleNode(".//div[@class='mbimg']/div[@class='mbcontent']/a");
        var thumbnailNode = node.SelectSingleNode(".//div[@class='mbimg']//img");
        var qualityNode = node.SelectSingleNode(".//div[@class='mvhdico']/span[2]");
        var videoIdNode = node.GetAttributeValue("data-id", string.Empty);
        var vrLogoNode = node.SelectSingleNode(".//span[@class='vrico']");
        var dataVp = node.GetAttributeValue("data-vp", string.Empty);

        var duration = ParseDuration(durationNode?.InnerText.Trim());
        var rating = ParseRating(ratingNode?.InnerText.Trim()) ?? 0;
        var views = ParseViews(viewsNode?.InnerText.Trim()) ?? 0;

        var videoItem = new VideoItem
        {
          Title = titleNode?.InnerText.Trim(),
          Duration = duration,
          Rating = rating,
          Views = views,
          Uploader = uploaderNode?.InnerText.Trim(),
          Link = linkNode != null ? new Uri(new Uri(baseUrl), linkNode.GetAttributeValue("href", string.Empty)).ToString() : string.Empty,
          Thumbnail = thumbnailNode.GetAttributeValue("src", string.Empty),
          Quality = qualityNode == null ? string.Empty : qualityNode.InnerText.Trim(),
          VideoId = !string.IsNullOrEmpty(videoIdNode) ? videoIdNode : string.Empty,
          IsVr = vrLogoNode != null,
          DataVp = !string.IsNullOrEmpty(dataVp) ? dataVp : null
        };

        var thumbSrc = thumbnailNode.GetAttributeValue("src", string.Empty);
        var thumbDataSrc = thumbnailNode.GetAttributeValue("data-src", string.Empty);

        videoItem.Thumbnail = thumbSrc == "data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==" ? thumbDataSrc : thumbSrc;

        items.Add(videoItem);
      }

      return items;
    }

    private static VideoPlayerSettings ParseVideoPlayerSettings(List<string> lines)
    {
      var settings = new VideoPlayerSettings();
      foreach (var line in lines)
      {
        var parts = line.Split(new[] { "=", ";" }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
        if (parts.Length < 2) continue;

        var propertyName = parts[0].Replace("EP.video.player.", "");
        var value = parts[1];

        switch (propertyName)
        {
          case "autoplay":
            settings.Autoplay = bool.Parse(value);
            break;
          case "disable":
            settings.Disable = bool.Parse(value);
            break;
          case "responsive":
            settings.Responsive = bool.Parse(value);
            break;
          case "enableCover":
            settings.EnableCover = bool.Parse(value);
            break;
          case "embed":
            settings.Embed = bool.Parse(value);
            break;
          case "muted":
            settings.Muted = bool.Parse(value);
            break;
          case "ar169":
            settings.Ar169 = bool.Parse(value);
            break;
          case "playbackRates":
            settings.PlaybackRates = value.Trim('[', ']').Split(',').Select(double.Parse).ToList();
            break;
          case "poster":
            settings.Poster = value.Trim('\'');
            break;
          case "vid":
            settings.Vid = value.Trim('\'');
            break;
          case "hash":
            settings.Hash = value.Trim('\'');
            break;
          case "url":
            settings.Url = value.Trim('\'');
            break;
          case "VR":
            settings.VR = bool.Parse(value);
            break;
          case "VRplugin":
            settings.VRplugin = value.Trim('\'');
            break;
          case "VRtype":
            settings.VRtype = value.Trim('\'');
            break;
          case "vjs":
            settings.Vjs = bool.Parse(value);
            break;
          case "initExtraFunc":
            settings.InitExtraFunc = bool.Parse(value);
            break;
        }
      }

      return settings;
    }

    private static TimeSpan? ParseDuration(string? durationText)
    {
      if (string.IsNullOrEmpty(durationText))
      {
        return null;
      }

      var parts = durationText.Split(':');
      if (parts.Length == 2)
      {
        // Format: mm:ss
        if (int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
        {
          return TimeSpan.FromSeconds(minutes * 60 + seconds);
        }
      }
      else if (parts.Length == 3)
      {
        // Format: hh:mm:ss
        if (int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes) && int.TryParse(parts[2], out int seconds))
        {
          return TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
        }
      }

      return null;
    }

    private static double? ParseRating(string? ratingText)
    {
      if (double.TryParse(ratingText?.TrimEnd('%'), out var rating))
      {
        return rating / 100;
      }
      return null;
    }

    private static long? ParseViews(string? viewsText)
    {
      if (viewsText == null)
      {
        return null;
      }

      viewsText = viewsText.Replace(",", "").Replace(" views", "");
      if (long.TryParse(viewsText, out var views))
      {
        return views;
      }
      return null;
    }

    private static string Base36Encode(long value)
    {
      const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
      string result = string.Empty;
      while (value > 0)
      {
        result = chars[(int)(value % 36)] + result;
        value /= 36;
      }
      return string.IsNullOrEmpty(result) ? "0" : result;
    }

    private static string ConvertToBase36(string a)
    {
      if (a.Length != 32)
      {
        throw new ArgumentException("Input string must be 32 characters long.");
      }

      string part1 = Base36Encode(Convert.ToInt64(a.Substring(0, 8), 16));
      string part2 = Base36Encode(Convert.ToInt64(a.Substring(8, 8), 16));
      string part3 = Base36Encode(Convert.ToInt64(a.Substring(16, 8), 16));
      string part4 = Base36Encode(Convert.ToInt64(a.Substring(24, 8), 16));

      return part1 + part2 + part3 + part4;
    }
  }
}
