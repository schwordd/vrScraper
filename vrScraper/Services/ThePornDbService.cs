using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services.Interfaces;
using vrScraper.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class ThePornDbService(
    ILogger<ThePornDbService> logger,
    ISettingService settingService,
    IVideoService videoService,
    IServiceProvider serviceProvider,
    IHttpClientFactory httpClientFactory) : IThePornDbService
  {
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastRequest = DateTime.MinValue;
    private const int RateDelayMs = 500; // 120 req/min

    public async Task<PerformerInfo?> SearchPerformer(string name)
    {
      var token = settingService.GetSettingValue("ThePornDbApiToken");
      if (string.IsNullOrWhiteSpace(token))
      {
        logger.LogWarning("ThePornDB API token not configured");
        return null;
      }

      await ThrottleAsync();

      try
      {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var encodedName = Uri.EscapeDataString(name);
        var response = await client.GetAsync($"https://api.theporndb.net/performers?q={encodedName}");

        if (!response.IsSuccessStatusCode)
        {
          logger.LogWarning("ThePornDB API returned {Status} for performer {Name}", response.StatusCode, name);
          return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var result = JObject.Parse(json);
        var data = result["data"] as JArray;

        if (data == null || data.Count == 0)
          return null;

        var first = data[0];
        var performer = new PerformerInfo
        {
          Name = first["name"]?.ToString() ?? name,
          Aliases = first["aliases"]?.ToObject<List<string>>() ?? [],
          Tags = first["tags"]?.Select(t => t["tag"]?.ToString() ?? t.ToString()).Where(t => !string.IsNullOrEmpty(t)).ToList() ?? []
        };

        return performer;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error searching ThePornDB for performer {Name}", name);
        return null;
      }
    }

    public async Task<int> EnrichAllStars(Action<int, int>? progressCallback = null)
    {
      var token = settingService.GetSettingValue("ThePornDbApiToken");
      if (string.IsNullOrWhiteSpace(token))
      {
        logger.LogWarning("ThePornDB API token not configured, skipping enrichment");
        return 0;
      }

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var allStars = await context.Stars.Include(s => s.Videos).ToListAsync();
      var allTags = await context.Tags.Include(t => t.Videos).ToListAsync();

      int enriched = 0;
      int tagsAdded = 0;

      for (int i = 0; i < allStars.Count; i++)
      {
        var star = allStars[i];
        var performer = await SearchPerformer(star.Name);
        if (performer == null) continue;

        // Add performer's genre tags to all videos of this star
        foreach (var tagName in performer.Tags)
        {
          var tag = allTags.FirstOrDefault(t =>
            string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));

          if (tag == null)
          {
            tag = new DbTag { Name = tagName, Videos = [] };
            context.Tags.Add(tag);
            allTags.Add(tag);
          }

          tag.Videos ??= [];

          foreach (var video in star.Videos ?? [])
          {
            if (!tag.Videos.Any(v => v.Id == video.Id))
            {
              tag.Videos.Add(video);

              // Mark as auto-detected via junction entity
              if (!context.VideoTags.Local.Any(vt => vt.VideoId == video.Id && vt.TagId == tag.Id)
                && !await context.VideoTags.AnyAsync(vt => vt.VideoId == video.Id && vt.TagId == tag.Id))
              {
                context.VideoTags.Add(new DbVideoTag { VideoId = video.Id, TagId = tag.Id, IsAutoDetected = true });
              }

              tagsAdded++;
            }
          }
        }

        enriched++;
        if (enriched % 10 == 0)
        {
          await context.SaveChangesAsync();
          progressCallback?.Invoke(i + 1, allStars.Count);
        }
      }

      await context.SaveChangesAsync();
      progressCallback?.Invoke(allStars.Count, allStars.Count);

      logger.LogInformation("Enriched {Stars} stars, added {Tags} tag links via ThePornDB", enriched, tagsAdded);

      await videoService.ReloadVideos();

      return enriched;
    }

    private async Task ThrottleAsync()
    {
      await _rateLimiter.WaitAsync();
      try
      {
        var elapsed = DateTime.UtcNow - _lastRequest;
        if (elapsed < TimeSpan.FromMilliseconds(RateDelayMs))
          await Task.Delay(TimeSpan.FromMilliseconds(RateDelayMs) - elapsed);
        _lastRequest = DateTime.UtcNow;
      }
      finally
      {
        _rateLimiter.Release();
      }
    }
  }
}
