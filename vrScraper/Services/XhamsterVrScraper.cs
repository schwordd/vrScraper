using HtmlAgilityPack;
using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace vrScraper.Services
{
  public class XhamsterVrScraper(ILogger<XhamsterVrScraper> logger, IServiceProvider serviceProvider, IVideoService vs, ITagNormalizationService tagNorm) : IXhamsterVrScraper
  {
    public string SiteName => "xhamster.com";
    public string DisplayName => "xHamster VR";
    public bool IsExperimental => true;

    public Dictionary<string, string> GetProxyHeaders() => new()
    {
      { "Referer", "https://xhamster.desi/" },
      { "Origin", "https://xhamster.desi" }
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

    private const string BaseUrl = "https://xhamster.com/vr/newest";
    private const string Site = "xhamster.com";

    public void Initialize()
    {
      logger.LogInformation("XhamsterVrScraper initialized.");
    }

    public void StopScraping()
    {
      if (_cancellationTokenSource != null)
      {
        logger.LogInformation("Stopping xHamster scraping process...");
        _cancellationTokenSource.Cancel();
        _scrapingStatus = "Stopping...";
      }
    }

    private void InitializeNewScraping()
    {
      if (_cancellationTokenSource != null)
      {
        _cancellationTokenSource.Dispose();
      }
      _cancellationTokenSource = new CancellationTokenSource();
    }

    public void StartScraping(int start, int count)
    {
      logger.LogInformation("StartScraping called with start={Start}, count={Count}", start, count);

      if (_scrapingInProgress)
      {
        logger.LogWarning("xHamster scraping already in progress, ignoring request");
        return;
      }

      _scrapingInProgress = true;
      InitializeNewScraping();
      logger.LogInformation("Starting xHamster scraping process");

      Task.Run(async () =>
      {
        try
        {
          logger.LogInformation("Starting ScrapeXhamster task");
          await ScrapeXhamster(start, count, _cancellationTokenSource!.Token);
          logger.LogInformation("ScrapeXhamster task completed");
        }
        catch (OperationCanceledException)
        {
          logger.LogInformation("xHamster scraping process was cancelled");
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error during xHamster scraping process");
        }
        finally
        {
          _scrapingInProgress = false;
          _scrapingStatus = string.Empty;
          _currentVideoThumbnail = null;
          _currentVideoTitle = null;

          // Reload VideoService cache after scraping
          await vs.ReloadVideos();

          logger.LogInformation("xHamster scraping process finished");
        }
      });
    }

    private async Task ScrapeXhamster(int startPage, int pageCount, CancellationToken cancellationToken)
    {
      logger.LogInformation("ScrapeXhamster started with startPage={StartPage}, pageCount={PageCount}", startPage, pageCount);

      var allItems = new List<ScrapedItem>();
      var rnd = new Random();
      var currentPage = startPage;

      while (!cancellationToken.IsCancellationRequested)
      {
        _scrapingStatus = $"Total items found: {allItems.Count} - Processing page {currentPage}...";
        logger.LogInformation(_scrapingStatus);

        var pageUrl = currentPage <= 1 ? BaseUrl : $"{BaseUrl}/{currentPage}";
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
          logger.LogInformation("Limit of pages ({PageCount}) reached. Stopping.", pageCount);
          break;
        }

        // Rate limiting: 3-5 second delay between requests
        await Task.Delay(rnd.Next(3000, 5000), cancellationToken);
      }

      cancellationToken.ThrowIfCancellationRequested();

      // Filter by per-site minimum duration setting
      var settingService = serviceProvider.GetRequiredService<ISettingService>();
      var minDurationSetting = await settingService.GetSetting($"Site:{Site}:MinDuration");
      var minDurationSeconds = 660; // Default 11 min for xHamster (filter trailers)
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

        // Build existing video list for cross-site deduplication
        var existingVideos = await context.VideoItems
          .Select(v => new { v.Title, v.Duration })
          .ToListAsync(cancellationToken);
        var existingForDedup = existingVideos
          .Select(v => (v.Title, v.Duration))
          .ToList();

        var newInsertions = 0;
        var duplicateCount = 0;

        foreach (var item in allItems)
        {
          cancellationToken.ThrowIfCancellationRequested();

          _currentVideoThumbnail = item.Thumbnail;
          _currentVideoTitle = item.Title;
          _scrapingStatus = $"Processing: {item.Title}";

          // Check if already exists for this site
          var existingVideo = context.VideoItems.FirstOrDefault(a => a.Site == Site && a.SiteVideoId == item.SiteVideoId);
          if (existingVideo != null)
          {
            continue;
          }

          // Cross-site deduplication
          if (DeduplicationHelper.IsProbableDuplicate(item.Title, item.Duration, existingForDedup))
          {
            duplicateCount++;
            logger.LogInformation("Skipping probable duplicate: {Title}", item.Title);
            continue;
          }

          var dbVideoItem = new DbVideoItem()
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
          };

          context.VideoItems.Add(dbVideoItem);
          newInsertions++;

          // Add to dedup list so later items in this batch are also checked
          existingForDedup.Add((item.Title, item.Duration));
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("{NewCount} new videos added, {DuplicateCount} duplicates skipped.", newInsertions, duplicateCount);

        // Parse tags from scraped data
        _scrapingStatus = "Parsing tags for new videos...";
        var newVideos = await context.VideoItems
          .Where(v => v.Site == Site && !v.ParsedDetails)
          .ToListAsync(cancellationToken);

        for (var i = 0; i < newVideos.Count; i++)
        {
          cancellationToken.ThrowIfCancellationRequested();
          _scrapingStatus = $"Parsing details: {i + 1} / {newVideos.Count}";

          try
          {
            await ParseVideoDetails(newVideos[i], context, cancellationToken);
          }
          catch (Exception ex)
          {
            logger.LogError(ex, "Error parsing details for video {Id}: {Title}. Skipping.", newVideos[i].Id, newVideos[i].Title);
            await vs.UpdateVideoErrorCount(newVideos[i].Id);
          }

          // Rate limiting between detail page fetches
          await Task.Delay(rnd.Next(3000, 5000), cancellationToken);
        }

        await vs.Initialize();
        logger.LogInformation("xHamster scraping complete!");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while saving xHamster scraped data.");
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

        // xHamster uses thumb-list items for video listings
        var videoNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'thumb-list')]//div[contains(@class, 'thumb-list__item')]")
          ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'video-thumb')]")
          ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'thumb-list')]/article");

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
            if (item != null)
            {
              items.Add(item);
            }
          }
          catch (Exception ex)
          {
            logger.LogWarning(ex, "Failed to parse a video node on page: {Url}", url);
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
      // Find the link element
      var linkNode = node.SelectSingleNode(".//a[contains(@class, 'video-thumb-info__name')]")
        ?? node.SelectSingleNode(".//a[contains(@href, '/videos/')]");

      if (linkNode == null) return null;

      var link = linkNode.GetAttributeValue("href", "");
      if (string.IsNullOrEmpty(link)) return null;

      // Ensure absolute URL
      if (link.StartsWith("/"))
        link = "https://xhamster.com" + link;

      var title = linkNode.InnerText?.Trim()
        ?? linkNode.GetAttributeValue("title", "")?.Trim()
        ?? "";

      if (string.IsNullOrEmpty(title)) return null;

      // Extract video ID from URL (e.g., /videos/some-title-12345 -> 12345)
      var siteVideoId = ExtractVideoId(link);
      if (string.IsNullOrEmpty(siteVideoId)) return null;

      // Find thumbnail
      var imgNode = node.SelectSingleNode(".//img");
      var thumbnail = imgNode?.GetAttributeValue("src", "")
        ?? imgNode?.GetAttributeValue("data-src", "")
        ?? "";

      // Find duration
      var durationNode = node.SelectSingleNode(".//*[contains(@class, 'duration')]")
        ?? node.SelectSingleNode(".//*[contains(@class, 'thumb-image-container__duration')]");
      var duration = ParseDuration(durationNode?.InnerText?.Trim());

      // Find view count
      var viewsNode = node.SelectSingleNode(".//*[contains(@class, 'views')]");
      var views = ParseViews(viewsNode?.InnerText?.Trim());

      return new ScrapedItem
      {
        SiteVideoId = siteVideoId,
        Title = HtmlEntity.DeEntitize(title),
        Link = link,
        Thumbnail = thumbnail,
        Duration = duration,
        Views = views,
        Quality = "VR" // All items from VR category
      };
    }

    private static string? ExtractVideoId(string url)
    {
      // xHamster URLs: https://xhamster.com/videos/some-title-12345
      // The numeric ID is typically at the end of the URL path
      var match = Regex.Match(url, @"/videos/.*?-(\d+)$");
      if (match.Success)
        return match.Groups[1].Value;

      // Alternative: just grab the last numeric segment
      match = Regex.Match(url, @"(\d+)(?:\?|$)");
      if (match.Success)
        return match.Groups[1].Value;

      // Fallback: use the full path segment as ID
      var segments = url.Split('/');
      var lastSegment = segments.LastOrDefault(s => !string.IsNullOrEmpty(s));
      return lastSegment;
    }

    private static TimeSpan ParseDuration(string? durationText)
    {
      if (string.IsNullOrWhiteSpace(durationText))
        return TimeSpan.Zero;

      // Clean up the text
      durationText = durationText.Trim();

      // Format: "12:34" or "1:23:45"
      var parts = durationText.Split(':');
      try
      {
        if (parts.Length == 2)
        {
          return new TimeSpan(0, int.Parse(parts[0]), int.Parse(parts[1]));
        }
        else if (parts.Length == 3)
        {
          return new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
        }
      }
      catch
      {
        // If parsing fails, return zero
      }

      return TimeSpan.Zero;
    }

    private static long? ParseViews(string? viewsText)
    {
      if (string.IsNullOrWhiteSpace(viewsText))
        return null;

      // Remove non-numeric characters except K, M
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

    private async Task ParseVideoDetails(DbVideoItem video, VrScraperContext context, CancellationToken cancellationToken)
    {
      if (string.IsNullOrEmpty(video.Link)) return;

      var web = new HtmlWeb();
      web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
      var doc = await web.LoadFromWebAsync(video.Link);

      if (doc?.DocumentNode == null)
      {
        logger.LogWarning("Failed to load detail page for video {Id}", video.Id);
        video.ParsedDetails = true;
      video.LastScrapedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return;
      }

      var html = doc.DocumentNode.InnerHtml;

      // Clean slate: remove all existing star/tag links for this video
      var existingStarLinks = await context.VideoStars.Where(vs => vs.VideoId == video.Id).ToListAsync(cancellationToken);
      context.VideoStars.RemoveRange(existingStarLinks);
      var existingTagLinks = await context.VideoTags.Where(vt => vt.VideoId == video.Id).ToListAsync(cancellationToken);
      context.VideoTags.RemoveRange(existingTagLinks);
      video.NormalizedTitle = null;
      await context.SaveChangesAsync(cancellationToken);

      // Parse categories from URL params embedded in GTM/analytics data
      // Pattern: videoCategory=BBW%2CBabe%2CBig+Natural+Tits%2C...
      var categoryMatch = Regex.Match(html, @"videoCategory=([^&""]+)");
      if (categoryMatch.Success)
      {
        var categoriesRaw = Uri.UnescapeDataString(categoryMatch.Groups[1].Value);
        var categories = categoriesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var catName in categories)
        {
          var tagName = tagNorm.NormalizeTag(catName.Trim());
          if (string.IsNullOrEmpty(tagName)) continue;

          var tag = await context.Tags.Where(t => t.Name == tagName).FirstOrDefaultAsync(cancellationToken)
            ?? context.Tags.Local.FirstOrDefault(t => t.Name == tagName);
          if (tag == null)
          {
            tag = new DbTag() { Name = tagName };
            context.Tags.Add(tag);
            tag.Videos = new List<DbVideoItem>();
          }

          tag.Videos ??= new List<DbVideoItem>();
          if (!tag.Videos.Any(v => v.Id == video.Id))
          {
            tag.Videos.Add(video);
          }
        }
        logger.LogInformation("Parsed {Count} categories for video {Id}", categories.Length, video.Id);
      }

      // Parse pornstars from xprf parameter
      // Pattern: xprf=Alice+Peachy%2CVirtual+Taboo
      var starMatch = Regex.Match(html, @"xprf=([^&""]+)");
      if (starMatch.Success)
      {
        var starsRaw = Uri.UnescapeDataString(starMatch.Groups[1].Value);
        var stars = starsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var sName in stars)
        {
          var starName = sName.Trim();
          if (string.IsNullOrEmpty(starName)) continue;

          // Skip channel/studio names (they appear in xprf too)
          if (starName.Contains("Studio") || starName.Contains("Taboo") || starName.Contains("VR "))
            continue;

          var star = await context.Stars.Where(s => s.Name == starName).FirstOrDefaultAsync(cancellationToken)
            ?? context.Stars.Local.FirstOrDefault(s => s.Name == starName);
          if (star == null)
          {
            star = new DbStar() { Name = starName };
            context.Stars.Add(star);
            star.Videos = new List<DbVideoItem>();
          }

          star.Videos ??= new List<DbVideoItem>();
          if (!star.Videos.Any(v => v.Id == video.Id))
          {
            star.Videos.Add(video);
          }
        }
        logger.LogInformation("Parsed {Count} stars for video {Id}", stars.Length, video.Id);
      }

      video.ParsedDetails = true;
      video.LastScrapedUtc = DateTime.UtcNow;
      await context.SaveChangesAsync(cancellationToken);
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
        {
          logger.LogWarning("Failed to load video page for source extraction: {Link}", video.Link);
          return null;
        }

        // xHamster typically embeds video URLs in a script containing window.initials or similar JSON
        var scriptNodes = doc.DocumentNode.SelectNodes("//script");
        string? videoUrl = null;
        int resolution = 0;

        if (scriptNodes != null)
        {
          foreach (var script in scriptNodes)
          {
            var scriptContent = script.InnerText;

            // Look for direct mp4 URLs in window.initials or similar data structures
            // Pattern: "videoModel":{"sources":{"mp4":{...}}}
            // or "url":"https://...mp4..."
            if (scriptContent.Contains("\"mp4\"") || scriptContent.Contains(".mp4"))
            {
              // Try to find the highest quality mp4 URL
              var mp4Matches = Regex.Matches(scriptContent, @"""(\d{3,4})p?""\s*:\s*\{[^}]*""url""\s*:\s*""([^""]+\.mp4[^""]*)""");

              foreach (Match m in mp4Matches)
              {
                if (int.TryParse(m.Groups[1].Value, out var res) && res > resolution)
                {
                  resolution = res;
                  videoUrl = m.Groups[2].Value.Replace("\\/", "/");
                }
              }

              // Alternative pattern: direct URL extraction
              if (videoUrl == null)
              {
                var urlMatch = Regex.Match(scriptContent, @"""(https?://[^""]+\.mp4[^""]*)""");
                if (urlMatch.Success)
                {
                  videoUrl = urlMatch.Groups[1].Value.Replace("\\/", "/");
                  resolution = 720; // Default assumption
                }
              }
            }

            // Also try HLS/m3u8 as fallback
            if (videoUrl == null && scriptContent.Contains(".m3u8"))
            {
              var m3u8Match = Regex.Match(scriptContent, @"""(https?://[^""]+\.m3u8[^""]*)""");
              if (m3u8Match.Success)
              {
                videoUrl = m3u8Match.Groups[1].Value.Replace("\\/", "/");
                resolution = 720;
              }
            }

            if (videoUrl != null) break;
          }
        }

        if (videoUrl == null)
        {
          logger.LogWarning("Could not find video source URL for: {Link}", video.Link);

          // Track error count
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
        logger.LogError(ex, "Error getting source for video {Id}: {Link}", video.Id, video.Link);
        return null;
      }
    }

    /// <summary>
    /// Internal model for scraped video data before DB insertion
    /// </summary>
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
