using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public class RecommendationService : IRecommendationService
  {
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(ILogger<RecommendationService> logger)
    {
      _logger = logger;
    }

    public (Dictionary<string, double> TagAffinities, Dictionary<string, double> StarAffinities) GetAffinities(List<DbVideoItem> allItems)
    {
      // Step 1: Find signal videos
      var signalVideos = allItems
        .Where(v => v.Liked || v.PlayCount >= 2 || v.PlayDurationEst > TimeSpan.FromMinutes(2))
        .ToList();

      var tagAffinity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
      var starAffinity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

      if (signalVideos.Count == 0)
      {
        return (tagAffinity, starAffinity);
      }

      // Step 2: Build tag/star affinity
      foreach (var video in signalVideos)
      {
        double weight = video.Liked ? 2.0 : 1.0;

        if (video.Tags != null)
        {
          foreach (var tag in video.Tags)
          {
            if (!tagAffinity.ContainsKey(tag.Name))
              tagAffinity[tag.Name] = 0;
            tagAffinity[tag.Name] += weight;
          }
        }

        if (video.Stars != null)
        {
          foreach (var star in video.Stars)
          {
            if (!starAffinity.ContainsKey(star.Name))
              starAffinity[star.Name] = 0;
            starAffinity[star.Name] += weight;
          }
        }
      }

      // Normalize affinities
      double totalSignalVideos = signalVideos.Count;
      foreach (var key in tagAffinity.Keys.ToList())
        tagAffinity[key] /= totalSignalVideos;
      foreach (var key in starAffinity.Keys.ToList())
        starAffinity[key] /= totalSignalVideos;

      // Apply IDF weighting: common tags (on many videos) count less, rare tags count more
      double totalVideos = allItems.Count;
      var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var starCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      foreach (var video in allItems)
      {
        if (video.Tags != null)
          foreach (var tag in video.Tags)
          {
            tagCounts.TryGetValue(tag.Name, out int c);
            tagCounts[tag.Name] = c + 1;
          }
        if (video.Stars != null)
          foreach (var star in video.Stars)
          {
            starCounts.TryGetValue(star.Name, out int c);
            starCounts[star.Name] = c + 1;
          }
      }

      foreach (var key in tagAffinity.Keys.ToList())
      {
        if (tagCounts.TryGetValue(key, out int count) && count > 0)
          tagAffinity[key] *= Math.Log(totalVideos / count);
      }
      foreach (var key in starAffinity.Keys.ToList())
      {
        if (starCounts.TryGetValue(key, out int count) && count > 0)
          starAffinity[key] *= Math.Log(totalVideos / count);
      }

      return (tagAffinity, starAffinity);
    }

    public List<DbVideoItem> GetRecommendedVideos(List<DbVideoItem> allItems, int limit = 500)
    {
      var (tagAffinity, starAffinity) = GetAffinities(allItems);

      if (tagAffinity.Count == 0 && starAffinity.Count == 0)
      {
        _logger.LogDebug("No signal videos found, returning empty recommendations");
        return new List<DbVideoItem>();
      }

      _logger.LogDebug("Computing recommendations from {TagCount} tag affinities, {StarCount} star affinities",
        tagAffinity.Count, starAffinity.Count);

      // Step 3: Score each candidate video (exclude disliked)
      var scored = new List<(DbVideoItem Video, double Score)>();

      foreach (var video in allItems)
      {
        if (video.Disliked)
          continue;

        double tagScore = 0;
        if (video.Tags != null && video.Tags.Count > 0)
        {
          foreach (var tag in video.Tags)
          {
            if (tagAffinity.TryGetValue(tag.Name, out var affinity))
              tagScore += affinity;
          }
          tagScore /= video.Tags.Count;
        }

        double starScore = 0;
        if (video.Stars != null && video.Stars.Count > 0)
        {
          foreach (var star in video.Stars)
          {
            if (starAffinity.TryGetValue(star.Name, out var affinity))
              starScore += affinity;
          }
          starScore /= video.Stars.Count;
        }

        double combinedScore = tagScore * 0.6 + starScore * 0.4;

        // Quality boost
        double siteRating = video.SiteRating ?? 0.5;
        combinedScore *= (0.8 + 0.2 * siteRating);

        // Skip already watched videos
        if (video.PlayCount > 0)
          continue;

        if (combinedScore > 0)
          scored.Add((video, combinedScore));
      }

      var result = scored
        .OrderByDescending(s => s.Score)
        .Take(limit)
        .Select(s => s.Video)
        .ToList();

      _logger.LogDebug("Generated {Count} recommended videos", result.Count);
      return result;
    }

    public List<DbVideoItem> GetSimilarVideos(long videoId, List<DbVideoItem> allItems, int limit = 20)
    {
      var sourceVideo = allItems.FirstOrDefault(v => v.Id == videoId);
      if (sourceVideo == null)
      {
        _logger.LogWarning("Source video {VideoId} not found for similarity search", videoId);
        return new List<DbVideoItem>();
      }

      var sourceTags = new HashSet<string>(
        (sourceVideo.Tags ?? new List<DbTag>()).Select(t => t.Name),
        StringComparer.OrdinalIgnoreCase);

      var sourceStars = new HashSet<string>(
        (sourceVideo.Stars ?? new List<DbStar>()).Select(s => s.Name),
        StringComparer.OrdinalIgnoreCase);

      var scored = new List<(DbVideoItem Video, double Similarity)>();

      foreach (var video in allItems)
      {
        if (video.Id == videoId)
          continue;

        var videoTags = new HashSet<string>(
          (video.Tags ?? new List<DbTag>()).Select(t => t.Name),
          StringComparer.OrdinalIgnoreCase);

        var videoStars = new HashSet<string>(
          (video.Stars ?? new List<DbStar>()).Select(s => s.Name),
          StringComparer.OrdinalIgnoreCase);

        // Tag Jaccard
        double tagJaccard = 0;
        if (sourceTags.Count > 0 || videoTags.Count > 0)
        {
          int tagIntersection = sourceTags.Intersect(videoTags).Count();
          int tagUnion = sourceTags.Union(videoTags).Count();
          tagJaccard = (double)tagIntersection / tagUnion;
        }

        // Star Jaccard
        double starJaccard = 0;
        if (sourceStars.Count > 0 || videoStars.Count > 0)
        {
          int starIntersection = sourceStars.Intersect(videoStars).Count();
          int starUnion = sourceStars.Union(videoStars).Count();
          starJaccard = (double)starIntersection / starUnion;
        }

        double similarity = tagJaccard * 0.6 + starJaccard * 0.4;

        if (similarity > 0.1)
          scored.Add((video, similarity));
      }

      var result = scored
        .OrderByDescending(s => s.Similarity)
        .Take(limit)
        .Select(s => s.Video)
        .ToList();

      _logger.LogDebug("Found {Count} similar videos for video {VideoId}", result.Count, videoId);
      return result;
    }
  }
}
