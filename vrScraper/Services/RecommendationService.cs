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

    public List<DbVideoItem> GetRecommendedVideos(List<DbVideoItem> allItems, int limit = 500)
    {
      // Step 1: Find signal videos
      var signalVideos = allItems
        .Where(v => v.Liked || v.PlayCount >= 2 || v.PlayDurationEst > TimeSpan.FromMinutes(2))
        .ToList();

      if (signalVideos.Count == 0)
      {
        _logger.LogDebug("No signal videos found, returning empty recommendations");
        return new List<DbVideoItem>();
      }

      _logger.LogDebug("Found {Count} signal videos for recommendation", signalVideos.Count);

      // Step 2: Build tag affinity
      var tagAffinity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
      var starAffinity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

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

        // Already watched penalty
        if (video.PlayCount > 0)
          combinedScore *= 0.3;

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
