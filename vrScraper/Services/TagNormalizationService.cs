using Newtonsoft.Json;
using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Services
{
  public class TagNormalizationService(ILogger<TagNormalizationService> logger, ISettingService settingService, IServiceProvider serviceProvider) : ITagNormalizationService
  {
    private Dictionary<string, string>? _reverseMap;

    public string NormalizeTag(string tagName)
    {
      if (string.IsNullOrWhiteSpace(tagName))
        return tagName;

      var reverse = GetReverseMap();
      if (reverse.TryGetValue(tagName.Trim(), out var canonical))
        return canonical;

      return tagName.Trim();
    }

    public Dictionary<string, List<string>> GetSynonymMap()
    {
      var json = settingService.GetSettingValue("TagSynonymMap") ?? "{}";
      try
      {
        return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
          ?? new Dictionary<string, List<string>>();
      }
      catch
      {
        return new Dictionary<string, List<string>>();
      }
    }

    public async Task SaveSynonymMap(Dictionary<string, List<string>> map)
    {
      var json = JsonConvert.SerializeObject(map);
      await settingService.SaveSetting("TagSynonymMap", json);
      _reverseMap = null; // invalidate cache
      logger.LogInformation("Tag synonym map saved with {Count} canonical entries", map.Count);
    }

    public async Task NormalizeAllExistingTags()
    {
      var synonymMap = GetSynonymMap();
      if (synonymMap.Count == 0)
      {
        logger.LogInformation("No synonyms defined, nothing to normalize");
        return;
      }

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var reverse = BuildReverseMap(synonymMap);
      var mergedCount = 0;

      foreach (var (synonym, canonical) in reverse)
      {
        // Find the synonym tag
        var synonymTag = await context.Tags
          .Include(t => t.Videos)
          .FirstOrDefaultAsync(t => t.Name == synonym);

        if (synonymTag == null) continue;

        // Find or create the canonical tag
        var canonicalTag = await context.Tags
          .Include(t => t.Videos)
          .FirstOrDefaultAsync(t => t.Name == canonical);

        if (canonicalTag == null)
        {
          canonicalTag = new DbTag { Name = canonical, Videos = new List<DbVideoItem>() };
          context.Tags.Add(canonicalTag);
        }

        canonicalTag.Videos ??= new List<DbVideoItem>();

        // Move videos from synonym to canonical
        if (synonymTag.Videos != null)
        {
          foreach (var video in synonymTag.Videos.ToList())
          {
            if (!canonicalTag.Videos.Any(v => v.Id == video.Id))
            {
              canonicalTag.Videos.Add(video);
            }
          }
          synonymTag.Videos.Clear();
        }

        context.Tags.Remove(synonymTag);
        mergedCount++;
      }

      await context.SaveChangesAsync();
      logger.LogInformation("Normalized {Count} synonym tags into canonical tags", mergedCount);
    }

    private Dictionary<string, string> GetReverseMap()
    {
      if (_reverseMap != null) return _reverseMap;

      var synonymMap = GetSynonymMap();
      _reverseMap = BuildReverseMap(synonymMap);
      return _reverseMap;
    }

    private static Dictionary<string, string> BuildReverseMap(Dictionary<string, List<string>> synonymMap)
    {
      var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var (canonical, synonyms) in synonymMap)
      {
        foreach (var synonym in synonyms)
        {
          reverse[synonym] = canonical;
        }
      }
      return reverse;
    }
  }
}
