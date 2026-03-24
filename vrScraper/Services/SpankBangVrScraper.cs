using HtmlAgilityPack;
using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace vrScraper.Services
{
  public class SpankBangVrScraper(ILogger<SpankBangVrScraper> logger, IServiceProvider serviceProvider, IVideoService vs, ITagNormalizationService tagNorm) : ISpankBangVrScraper
  {
    public string SiteName => "spankbang.com";
    public string DisplayName => "SpankBang VR";

    public Dictionary<string, string> GetProxyHeaders() => new()
    {
      { "Referer", "https://spankbang.com/" },
      { "Origin", "https://spankbang.com" }
    };

    public bool ScrapingInProgress => _scrapingInProgress;
    public string ScrapingStatus => _scrapingStatus;
    public string? CurrentVideoThumbnail => _currentVideoThumbnail;
    public string? CurrentVideoTitle => _currentVideoTitle;
    public bool IsScheduledScraping { get; set; } = false;

    public bool SupportsRescrape => false;
    public bool SupportsDeadThumbnailCheck => false;
    public bool SupportsDeleteErrors => false;
    public Task StartRescrape() => throw new NotSupportedException();
    public Task StartDeadThumbnailCheck() => throw new NotSupportedException();
    public Task StartDeleteErrors() => throw new NotSupportedException();

    private bool _scrapingInProgress = false;
    private string _scrapingStatus = string.Empty;
    private string? _currentVideoThumbnail = null;
    private string? _currentVideoTitle = null;
    private CancellationTokenSource? _cancellationTokenSource;

    private const string BaseUrl = "https://spankbang.com/s/vr/";
    private const string Site = "spankbang.com";

    public void Initialize()
    {
      logger.LogInformation("SpankBangVrScraper initialized.");
    }

    public void StopScraping()
    {
      if (_cancellationTokenSource != null)
      {
        logger.LogInformation("Stopping SpankBang scraping...");
        _cancellationTokenSource.Cancel();
        _scrapingStatus = "Stopping...";
      }
    }

    private void InitializeNewScraping()
    {
      _cancellationTokenSource?.Dispose();
      _cancellationTokenSource = new CancellationTokenSource();
    }

    public void StartScraping(int start, int count)
    {
      logger.LogInformation("StartScraping called with start={Start}, count={Count}", start, count);

      if (_scrapingInProgress)
      {
        logger.LogWarning("SpankBang scraping already in progress");
        return;
      }

      _scrapingInProgress = true;
      InitializeNewScraping();

      Task.Run(async () =>
      {
        try
        {
          await ScrapeSpankBang(start, count, _cancellationTokenSource!.Token);
        }
        catch (OperationCanceledException)
        {
          logger.LogInformation("SpankBang scraping cancelled");
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error during SpankBang scraping");
        }
        finally
        {
          _scrapingInProgress = false;
          _scrapingStatus = string.Empty;
          _currentVideoThumbnail = null;
          _currentVideoTitle = null;
          await vs.ReloadVideos();
          logger.LogInformation("SpankBang scraping finished");
        }
      });
    }

    private async Task ScrapeSpankBang(int startPage, int pageCount, CancellationToken ct)
    {
      var allItems = new List<ScrapedItem>();
      var rnd = new Random();
      var currentPage = startPage;

      while (!ct.IsCancellationRequested)
      {
        _scrapingStatus = $"Total items found: {allItems.Count} - Processing page {currentPage}...";
        logger.LogInformation(_scrapingStatus);

        // SpankBang pagination: ?o=new&p=1, ?o=new&p=2, etc.
        var pageUrl = $"{BaseUrl}?o=new&p={currentPage}";
        var pageItems = await ScrapeSinglePage(pageUrl);

        if (pageItems.Count > 0)
        {
          allItems.AddRange(pageItems);
          allItems = allItems.DistinctBy(a => a.SiteVideoId).ToList();
          currentPage++;
        }
        else
        {
          logger.LogInformation("No more results on page {Page}. Stopping.", currentPage);
          break;
        }

        if (pageCount != -1 && currentPage > (startPage + pageCount - 1))
        {
          logger.LogInformation("Page limit ({PageCount}) reached. Stopping.", pageCount);
          break;
        }

        await Task.Delay(rnd.Next(3000, 5000), ct);
      }

      ct.ThrowIfCancellationRequested();

      // Per-site min duration filter
      var settingService = serviceProvider.GetRequiredService<ISettingService>();
      var minDurationSetting = await settingService.GetSetting($"Site:{Site}:MinDuration");
      var minDurationSeconds = 660; // Default 11 min
      if (minDurationSetting != null && int.TryParse(minDurationSetting.Value, out int parsedMin))
      {
        minDurationSeconds = parsedMin;
      }
      if (minDurationSeconds > 0)
      {
        allItems = allItems.Where(v => v.Duration == TimeSpan.Zero || v.Duration.TotalSeconds >= minDurationSeconds).ToList();
        logger.LogInformation("After min-duration filter ({MinDuration}s): {Count} videos remain.", minDurationSeconds, allItems.Count);
      }

      // Insert into database
      try
      {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

        allItems = allItems.DistinctBy(a => a.SiteVideoId).ToList();

        var existingVideos = await context.VideoItems
          .Select(v => new { v.Title, v.Duration })
          .ToListAsync(ct);
        var existingForDedup = existingVideos.Select(v => (v.Title, v.Duration)).ToList();

        var newInsertions = 0;
        var duplicateCount = 0;

        foreach (var item in allItems)
        {
          ct.ThrowIfCancellationRequested();

          _currentVideoThumbnail = item.Thumbnail;
          _currentVideoTitle = item.Title;
          _scrapingStatus = $"Processing: {item.Title}";

          var existingVideo = context.VideoItems.FirstOrDefault(a => a.Site == Site && a.SiteVideoId == item.SiteVideoId);
          if (existingVideo != null) continue;

          if (DeduplicationHelper.IsProbableDuplicate(item.Title, item.Duration, existingForDedup))
          {
            duplicateCount++;
            logger.LogInformation("Skipping duplicate: {Title}", item.Title);
            continue;
          }

          context.VideoItems.Add(new DbVideoItem()
          {
            Site = Site,
            SiteVideoId = item.SiteVideoId,
            Duration = item.Duration,
            IsVr = true,
            Link = item.Link,
            Quality = item.Quality,
            Thumbnail = item.Thumbnail,
            Title = item.Title,
            Views = item.Views,
            ParsedDetails = false,
            AddedUTC = DateTime.UtcNow
          });

          newInsertions++;
          existingForDedup.Add((item.Title, item.Duration));
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("{NewCount} new videos added, {DuplicateCount} duplicates skipped.", newInsertions, duplicateCount);

        // Parse details for new videos
        _scrapingStatus = "Parsing details for new videos...";
        var newVideos = await context.VideoItems
          .Where(v => v.Site == Site && !v.ParsedDetails)
          .ToListAsync(ct);

        for (var i = 0; i < newVideos.Count; i++)
        {
          ct.ThrowIfCancellationRequested();
          _scrapingStatus = $"Parsing details: {i + 1} / {newVideos.Count}";

          try
          {
            await ParseVideoDetails(newVideos[i], context, ct);
          }
          catch (Exception ex)
          {
            logger.LogError(ex, "Error parsing details for video {Id}. Skipping.", newVideos[i].Id);
            await vs.UpdateVideoErrorCount(newVideos[i].Id);
          }

          await Task.Delay(rnd.Next(3000, 5000), ct);
        }

        await vs.Initialize();
        logger.LogInformation("SpankBang scraping complete!");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error saving SpankBang data.");
        throw;
      }
    }

    private async Task<List<ScrapedItem>> ScrapeSinglePage(string url)
    {
      var items = new List<ScrapedItem>();

      try
      {
        var web = new HtmlWeb();
        web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var doc = await web.LoadFromWebAsync(url);

        if (doc?.DocumentNode == null)
        {
          logger.LogWarning("Failed to load page: {Url}", url);
          return items;
        }

        var videoNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'js-video-item')]")
          ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'video-item')]");

        if (videoNodes == null)
        {
          logger.LogWarning("No video nodes found on page: {Url}", url);
          return items;
        }

        foreach (var node in videoNodes)
        {
          try
          {
            var item = ParseVideoNode(node);
            if (item != null) items.Add(item);
          }
          catch (Exception ex)
          {
            logger.LogWarning(ex, "Failed to parse video node");
          }
        }

        logger.LogInformation("Found {Count} videos on page: {Url}", items.Count, url);
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error loading page: {Url}", url);
      }

      return items;
    }

    private ScrapedItem? ParseVideoNode(HtmlNode node)
    {
      // Find link to video page
      var linkNode = node.SelectSingleNode(".//a[contains(@href, '/video/')]");
      if (linkNode == null) return null;

      var link = linkNode.GetAttributeValue("href", "");
      if (string.IsNullOrEmpty(link)) return null;

      if (link.StartsWith("/"))
        link = "https://spankbang.com" + link;

      var siteVideoId = ExtractVideoId(link);
      if (string.IsNullOrEmpty(siteVideoId)) return null;

      // Title from img alt attribute (SpankBang puts titles there)
      var imgNode = node.SelectSingleNode(".//img[@alt]");
      var title = imgNode?.GetAttributeValue("alt", "")?.Trim() ?? "";
      if (string.IsNullOrEmpty(title)) return null;

      // Thumbnail from img src
      var thumbnail = imgNode?.GetAttributeValue("src", "")
        ?? imgNode?.GetAttributeValue("data-src", "")
        ?? "";

      // Duration from data-testid="video-item-length" (format: "16m", "1h 20m")
      var durationNode = node.SelectSingleNode(".//*[@data-testid='video-item-length']");
      var duration = ParseSpankBangDuration(durationNode?.InnerText?.Trim());

      return new ScrapedItem
      {
        SiteVideoId = siteVideoId,
        Title = HtmlEntity.DeEntitize(title),
        Link = link,
        Thumbnail = thumbnail,
        Duration = duration,
        Views = null,
        Quality = "VR"
      };
    }

    private static string? ExtractVideoId(string url)
    {
      // SpankBang URLs: https://spankbang.com/abc123/video/title-here
      var match = Regex.Match(url, @"spankbang\.com/([a-z0-9]+)/video/");
      if (match.Success)
        return match.Groups[1].Value;

      // Fallback: first path segment after domain
      match = Regex.Match(url, @"spankbang\.com/([a-z0-9]+)");
      if (match.Success)
        return match.Groups[1].Value;

      return null;
    }

    private static TimeSpan ParseSpankBangDuration(string? durationText)
    {
      if (string.IsNullOrWhiteSpace(durationText))
        return TimeSpan.Zero;

      durationText = durationText.Trim().ToLowerInvariant();

      // SpankBang format: "16m", "1h 20m", "45m", "1h"
      int hours = 0, minutes = 0;

      var hourMatch = Regex.Match(durationText, @"(\d+)\s*h");
      if (hourMatch.Success)
        hours = int.Parse(hourMatch.Groups[1].Value);

      var minMatch = Regex.Match(durationText, @"(\d+)\s*m");
      if (minMatch.Success)
        minutes = int.Parse(minMatch.Groups[1].Value);

      if (hours == 0 && minutes == 0)
      {
        // Fallback: try "12:34" format
        var parts = durationText.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var s))
          return new TimeSpan(0, m, s);
      }

      return new TimeSpan(hours, minutes, 0);
    }

    private static long? ParseViews(string? viewsText)
    {
      if (string.IsNullOrWhiteSpace(viewsText))
        return null;

      viewsText = viewsText.Trim().ToUpperInvariant();

      if (viewsText.EndsWith("K"))
      {
        if (double.TryParse(viewsText.TrimEnd('K'), out var k))
          return (long)(k * 1000);
      }
      else if (viewsText.EndsWith("M"))
      {
        if (double.TryParse(viewsText.TrimEnd('M'), out var m))
          return (long)(m * 1_000_000);
      }
      else
      {
        var cleaned = Regex.Replace(viewsText, @"[^\d]", "");
        if (long.TryParse(cleaned, out var v))
          return v;
      }

      return null;
    }

    private async Task ParseVideoDetails(DbVideoItem video, VrScraperContext context, CancellationToken ct)
    {
      if (string.IsNullOrEmpty(video.Link)) return;

      var web = new HtmlWeb();
      web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
      var doc = await web.LoadFromWebAsync(video.Link);

      if (doc?.DocumentNode == null)
      {
        video.ParsedDetails = true;
      video.LastScrapedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return;
      }

      // Clean slate: remove all existing star/tag links for this video
      var existingStarLinks = await context.VideoStars.Where(vs => vs.VideoId == video.Id).ToListAsync(ct);
      context.VideoStars.RemoveRange(existingStarLinks);
      var existingTagLinks = await context.VideoTags.Where(vt => vt.VideoId == video.Id).ToListAsync(ct);
      context.VideoTags.RemoveRange(existingTagLinks);
      video.NormalizedTitle = null;
      await context.SaveChangesAsync(ct);

      // SpankBang tags have data-testid="tag" or are links to /s/tagname/
      var tagNodes = doc.DocumentNode.SelectNodes("//*[@data-testid='tag']")
        ?? doc.DocumentNode.SelectNodes("//a[starts-with(@href, '/s/') and not(contains(@href, '/pornstar'))]");

      if (tagNodes != null)
      {
        foreach (var tagNode in tagNodes)
        {
          var tagNameRaw = HtmlEntity.DeEntitize(tagNode.InnerText?.Trim() ?? "");
          if (string.IsNullOrEmpty(tagNameRaw) || tagNameRaw.Length > 50 || tagNameRaw == "Tags") continue;
          var tagName = tagNorm.NormalizeTag(tagNameRaw);

          var tag = await context.Tags.Where(t => t.Name == tagName).FirstOrDefaultAsync(ct)
            ?? context.Tags.Local.FirstOrDefault(t => t.Name == tagName);
          if (tag == null)
          {
            tag = new DbTag() { Name = tagName };
            context.Tags.Add(tag);
            tag.Videos = new List<DbVideoItem>();
          }
          tag.Videos ??= new List<DbVideoItem>();
          if (!tag.Videos.Any(v => v.Id == video.Id))
            tag.Videos.Add(video);
        }
        logger.LogInformation("Parsed {Count} tags for video {Id}", tagNodes.Count, video.Id);
      }

      video.ParsedDetails = true;
      video.LastScrapedUtc = DateTime.UtcNow;
      await context.SaveChangesAsync(ct);
    }

    public async Task<VideoSource?> GetSource(DbVideoItem video, VrScraperContext context)
    {
      if (string.IsNullOrEmpty(video.Link))
        return null;

      try
      {
        var web = new HtmlWeb();
        web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var doc = await web.LoadFromWebAsync(video.Link);

        if (doc?.DocumentNode == null)
          return null;

        string? videoUrl = null;
        int resolution = 0;

        // Try <source> tags first
        var sourceNodes = doc.DocumentNode.SelectNodes("//source[@src]");
        if (sourceNodes != null)
        {
          foreach (var src in sourceNodes)
          {
            var srcUrl = src.GetAttributeValue("src", "");
            var quality = src.GetAttributeValue("data-quality", "")
              ?? src.GetAttributeValue("label", "");

            if (!string.IsNullOrEmpty(srcUrl) && (srcUrl.Contains(".mp4") || srcUrl.Contains(".m3u8")))
            {
              var res = ParseResolution(quality);
              if (res > resolution)
              {
                resolution = res;
                videoUrl = srcUrl;
              }
            }
          }
        }

        // Try JavaScript stream_data
        if (videoUrl == null)
        {
          var scriptNodes = doc.DocumentNode.SelectNodes("//script");
          if (scriptNodes != null)
          {
            foreach (var script in scriptNodes)
            {
              var content = script.InnerText;
              if (content.Contains("stream_data") || content.Contains(".mp4") || content.Contains("m3u8"))
              {
                // Pattern: '720p':'https://...' or "720p":"https://..."
                var matches = Regex.Matches(content, @"['""](\d{3,4})p?['""]\s*:\s*['""]([^'""]+\.(mp4|m3u8)[^'""]*)['""]");
                foreach (Match m in matches)
                {
                  if (int.TryParse(m.Groups[1].Value, out var res) && res > resolution)
                  {
                    resolution = res;
                    videoUrl = m.Groups[2].Value.Replace("\\/", "/");
                  }
                }

                // Fallback: any mp4 URL
                if (videoUrl == null)
                {
                  var mp4Match = Regex.Match(content, @"['""]?(https?://[^'""]+\.mp4[^'""]*)['""]?");
                  if (mp4Match.Success)
                  {
                    videoUrl = mp4Match.Groups[1].Value.Replace("\\/", "/");
                    resolution = 720;
                  }
                }

                if (videoUrl != null) break;
              }
            }
          }
        }

        if (videoUrl == null)
        {
          logger.LogWarning("No video source found for: {Link}", video.Link);
          var dbItem = await context.VideoItems.Where(v => v.Id == video.Id).FirstAsync();
          dbItem.ErrorCount = (dbItem.ErrorCount ?? 0) + 1;
          await context.SaveChangesAsync();
          video.ErrorCount = dbItem.ErrorCount;
          return null;
        }

        return new VideoSource
        {
          Default = true,
          Resolution = resolution,
          Src = videoUrl,
          LabelShort = $"{resolution}p",
          Type = videoUrl.Contains(".m3u8") ? "application/x-mpegURL" : "video/mp4"
        };
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error getting source for video {Id}", video.Id);
        return null;
      }
    }

    private static int ParseResolution(string? quality)
    {
      if (string.IsNullOrEmpty(quality)) return 0;
      var match = Regex.Match(quality, @"(\d{3,4})");
      return match.Success && int.TryParse(match.Groups[1].Value, out var res) ? res : 0;
    }

    private class ScrapedItem
    {
      public string SiteVideoId { get; set; } = string.Empty;
      public string Title { get; set; } = string.Empty;
      public string? Link { get; set; }
      public string? Thumbnail { get; set; }
      public TimeSpan Duration { get; set; }
      public long? Views { get; set; }
      public string? Quality { get; set; }
    }
  }
}
