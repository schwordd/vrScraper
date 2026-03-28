using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  [AllowAnonymous]
  public class AdminController(
    IScraperRegistry scraperRegistry,
    ITitleNormalizationService titleNormService,
    IServiceProvider serviceProvider,
    IVideoService videoService) : ControllerBase
  {
    [HttpGet("status")]
    public IActionResult Status()
    {
      var status = scraperRegistry.GetAllScrapers().Select(s => new
      {
        site = s.SiteName,
        displayName = s.DisplayName,
        inProgress = s.ScrapingInProgress,
        status = s.ScrapingStatus,
        currentTitle = s.CurrentVideoTitle
      });
      return Ok(status);
    }

    [HttpPost("scrape")]
    public IActionResult Scrape([FromQuery] string site, [FromQuery] int pages = 3)
    {
      var scraper = scraperRegistry.GetScraperForSite(site);
      if (scraper == null) return NotFound($"No scraper for site: {site}");
      if (scraper.ScrapingInProgress) return Conflict("Scraping already in progress");

      scraper.StartScraping(1, pages);
      return Ok($"Started scraping {site} for {pages} pages");
    }

    [HttpPost("stop")]
    public IActionResult Stop([FromQuery] string site)
    {
      var scraper = scraperRegistry.GetScraperForSite(site);
      if (scraper == null) return NotFound($"No scraper for site: {site}");
      scraper.StopScraping();
      return Ok($"Stopped scraping {site}");
    }

    /// <summary>Test title normalization without DB changes</summary>
    [HttpGet("test-normalize")]
    public IActionResult TestNormalize([FromQuery] string title)
    {
      if (string.IsNullOrEmpty(title)) return BadRequest("title parameter required");

      var legacy = titleNormService.NormalizeTitleLegacy(title);
      var isObfuscated = titleNormService.IsObfuscated(title);

      return Ok(new
      {
        original = title,
        isObfuscated,
        decoder = legacy,
        decoderChanged = legacy != title,
      });
    }

    /// <summary>Show a video's current state (stars, tags, normalized title)</summary>
    [HttpGet("video/{id}")]
    public async Task<IActionResult> GetVideo(long id)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var video = await context.VideoItems.FindAsync(id);
      if (video == null) return NotFound();

      var stars = await context.VideoStars.Where(vs => vs.VideoId == id)
        .Join(context.Stars, vs => vs.StarId, s => s.Id, (vs, s) => new { s.Name, vs.IsAutoDetected })
        .ToListAsync();

      var tags = await context.VideoTags.Where(vt => vt.VideoId == id)
        .Join(context.Tags, vt => vt.TagId, t => t.Id, (vt, t) => new { t.Name, vt.IsAutoDetected })
        .ToListAsync();

      return Ok(new
      {
        video.Id,
        video.Title,
        video.NormalizedTitle,
        video.Site,
        video.Link,
        starCount = stars.Count,
        stars,
        tagCount = tags.Count,
        tags
      });
    }

    /// <summary>Dry-run normalization on all obfuscated titles (read-only, no DB changes)</summary>
    [HttpGet("test-normalization")]
    public async Task<IActionResult> TestNormalization([FromQuery] int limit = 100)
    {
      var allVideos = await videoService.GetVideoItems();

      var obfuscated = allVideos
        .Where(v => v.LastScrapedUtc != null && titleNormService.IsObfuscated(v.Title))
        .OrderByDescending(v => v.Id)
        .Take(limit)
        .ToList();

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var allStars = await context.Stars.ToListAsync();
      var allTags = await context.Tags.ToListAsync();

      var results = new List<object>();
      foreach (var v in obfuscated)
      {
        var normalized = titleNormService.NormalizeTitle(v.Title);
        var titleForDetection = normalized ?? v.Title;
        var stars = titleNormService.DetectStars(titleForDetection, allStars)
          .Where(s => s.Confidence >= 0.7)
          .Select(s => new { s.Star.Name, s.Confidence })
          .ToList();
        var tags = titleNormService.DetectTags(titleForDetection, allTags)
          .Select(t => t.Name)
          .ToList();

        results.Add(new
        {
          v.Id,
          original = v.Title,
          normalized,
          stars,
          tags,
          scrapedStars = v.Stars?.Select(s => s.Name).ToList() ?? new List<string>()
        });
      }

      return Ok(new { count = obfuscated.Count, results });
    }

    /// <summary>Rescrape a single video by ID (clean slate + fresh from source)</summary>
    [HttpPost("rescrape/{id}")]
    public async Task<IActionResult> RescrapeVideo(long id)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var video = await context.VideoItems.FindAsync(id);
      if (video == null) return NotFound($"Video {id} not found");

      // Snapshot before
      var starsBefore = await context.VideoStars
        .Where(vs => vs.VideoId == id)
        .Join(context.Stars, vs => vs.StarId, s => s.Id, (vs, s) => new { s.Name, vs.IsAutoDetected })
        .ToListAsync();
      var tagsBefore = await context.VideoTags
        .Where(vt => vt.VideoId == id)
        .Join(context.Tags, vt => vt.TagId, t => t.Id, (vt, t) => new { t.Name, vt.IsAutoDetected })
        .ToListAsync();
      var titleBefore = video.Title;

      // Get the concrete scraper and call ParseDetails
      var scraper = scraperRegistry.GetScraperForSite(video.Site);
      if (scraper == null) return NotFound($"No scraper for site: {video.Site}");

      try
      {
        // Use reflection to call ParseDetails (it's public but not on the interface)
        var parseMethod = scraper.GetType().GetMethod("ParseDetails");
        if (parseMethod == null) return StatusCode(500, "ParseDetails not found on scraper");

        var task = (Task)parseMethod.Invoke(scraper, [video, context])!;
        await task;
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { error = ex.Message, videoId = id });
      }

      // Snapshot after
      var starsAfter = await context.VideoStars
        .Where(vs => vs.VideoId == id)
        .Join(context.Stars, vs => vs.StarId, s => s.Id, (vs, s) => new { s.Name, vs.IsAutoDetected })
        .ToListAsync();
      var tagsAfter = await context.VideoTags
        .Where(vt => vt.VideoId == id)
        .Join(context.Tags, vt => vt.TagId, t => t.Id, (vt, t) => new { t.Name, vt.IsAutoDetected })
        .ToListAsync();

      await videoService.ReloadVideos();

      return Ok(new
      {
        videoId = id,
        site = video.Site,
        titleBefore,
        titleAfter = video.Title,
        normalizedTitle = video.NormalizedTitle,
        starsBefore,
        starsAfter,
        tagsBefore = tagsBefore.Count,
        tagsAfter = tagsAfter.Count
      });
    }

    /// <summary>Find videos with suspicious star counts (likely broken scraping data)</summary>
    [HttpGet("broken-videos")]
    public async Task<IActionResult> FindBrokenVideos([FromQuery] int minStars = 10, [FromQuery] int limit = 20)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var broken = await context.VideoStars
        .Where(vs => !vs.IsAutoDetected)
        .GroupBy(vs => vs.VideoId)
        .Where(g => g.Count() >= minStars)
        .Select(g => new { VideoId = g.Key, StarCount = g.Count() })
        .OrderByDescending(x => x.StarCount)
        .Take(limit)
        .ToListAsync();

      var videoIds = broken.Select(b => b.VideoId).ToList();
      var videos = await context.VideoItems
        .Where(v => videoIds.Contains(v.Id))
        .Select(v => new { v.Id, v.Title, v.Site })
        .ToListAsync();

      var result = broken.Select(b =>
      {
        var v = videos.FirstOrDefault(v => v.Id == b.VideoId);
        return new { b.VideoId, v?.Title, v?.Site, b.StarCount };
      });

      return Ok(result);
    }
  }
}
