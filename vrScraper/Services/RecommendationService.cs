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
      // Step 1: Find signal videos (any video with engagement)
      var signalVideos = allItems
        .Where(v => v.Liked || v.PlayCount >= 1 || v.PlayDurationEst > TimeSpan.FromMinutes(1))
        .ToList();

      var tagAffinity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
      var starAffinity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

      if (signalVideos.Count == 0)
      {
        return (tagAffinity, starAffinity);
      }

      // Step 2: Build tag/star affinity with graduated weights
      // Also apply negative signals from disliked videos (if enough data)
      bool useDislikeSignals = allItems.Count(v => v.Disliked) >= 10;

      foreach (var video in signalVideos)
      {
        double weight = ComputeSignalWeight(video);

        // Skip near-zero weights
        if (Math.Abs(weight) < 0.01)
          continue;

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

      // Apply negative signals from disliked videos
      if (useDislikeSignals)
      {
        var dislikedVideos = allItems.Where(v => v.Disliked).ToList();
        foreach (var video in dislikedVideos)
        {
          if (video.Tags != null)
            foreach (var tag in video.Tags)
            {
              if (!tagAffinity.ContainsKey(tag.Name))
                tagAffinity[tag.Name] = 0;
              tagAffinity[tag.Name] -= 0.5;
            }
          if (video.Stars != null)
            foreach (var star in video.Stars)
            {
              if (!starAffinity.ContainsKey(star.Name))
                starAffinity[star.Name] = 0;
              starAffinity[star.Name] -= 0.5;
            }
        }
      }

      // Count videos per tag/star (used for normalization and IDF)
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

      // Normalize per-tag/star by video count, remove ubiquitous tags (>30%)
      // Inverse dampened IDF: rare tags slightly penalized (more noise), common tags trusted more
      double prevalenceThreshold = totalVideos * 0.3;
      foreach (var key in tagAffinity.Keys.ToList())
      {
        if (tagCounts.TryGetValue(key, out int count) && count > 0)
        {
          if (count > prevalenceThreshold)
            tagAffinity.Remove(key);
          else
            tagAffinity[key] = tagAffinity[key] / count / (1.0 + 0.15 * Math.Log(Math.Max(1.0, totalVideos / (double)count)));
        }
      }
      foreach (var key in starAffinity.Keys.ToList())
      {
        if (starCounts.TryGetValue(key, out int count) && count > 0)
          starAffinity[key] = starAffinity[key] / count / (1.0 + 0.15 * Math.Log(Math.Max(1.0, totalVideos / (double)count)));
      }

      return (tagAffinity, starAffinity);
    }

    private static double ComputeSignalWeight(DbVideoItem video)
    {
      // Likes dominate massively; watches are weak background signal
      double weight;
      if (video.Liked)
        weight = 20.0;
      else if (video.PlayCount >= 5)
        weight = 1.5;
      else if (video.PlayCount >= 2)
        weight = 0.6;
      else if (video.PlayCount == 1)
        weight = 0.1;
      else
        weight = 0.05;

      // Engagement boost from VideoEngagement (scrub data)
      var engagement = video.Engagement;
      if (engagement != null)
      {
        if (engagement.BackwardScrubCount >= 3)
          weight += 0.3;
        if (engagement.ScrubCoveragePercent >= 0.7)
          weight += 0.2;
        if (engagement.OpenCount >= 3)
          weight += 0.2;
        if (engagement.ScrubEventCount <= 3 && engagement.OpenCount == 1 && !video.Liked)
          weight -= 0.2; // Quick-skip signal
      }

      return weight;
    }

    public List<ScoredVideo> GetRecommendedVideos(List<DbVideoItem> allItems, int limit = 500)
    {
      var (tagAffinity, starAffinity) = GetAffinities(allItems);

      if (tagAffinity.Count == 0 && starAffinity.Count == 0)
      {
        _logger.LogDebug("No signal videos found, returning top-rated unwatched videos as fallback");
        return allItems
          .Where(v => !v.Disliked && v.PlayCount == 0)
          .OrderByDescending(v => v.SiteRating ?? 0)
          .Take(limit)
          .Select(v => new ScoredVideo(v, v.SiteRating ?? 0, "top-rated"))
          .ToList();
      }

      _logger.LogDebug("Computing recommendations from {TagCount} tag affinities, {StarCount} star affinities",
        tagAffinity.Count, starAffinity.Count);

      // Compute duration preference from signal videos
      var signalVideos = allItems
        .Where(v => v.Liked || v.PlayCount >= 2)
        .ToList();
      var (preferredDurationMin, durationStdDev) = ComputeDurationPreference(signalVideos);

      var scored = new List<ScoredVideo>();

      foreach (var video in allItems)
      {
        if (video.Disliked)
          continue;

        // Skip already watched videos (recommended = unwatched only)
        if (video.PlayCount > 0)
          continue;

        double tagScore = 0;
        int tagMatches = 0;
        string? topTag = null;
        double topTagScore = 0;
        if (video.Tags != null && video.Tags.Count > 0)
        {
          foreach (var tag in video.Tags)
          {
            if (tagAffinity.TryGetValue(tag.Name, out var affinity))
            {
              tagScore += affinity;
              tagMatches++;
              if (affinity > topTagScore) { topTagScore = affinity; topTag = tag.Name; }
            }
          }
          if (tagMatches > 0)
            tagScore /= tagMatches; // average only matching tags, ignore neutral ones
        }

        double starScore = 0;
        int starMatches = 0;
        string? topStar = null;
        double topStarScore = 0;
        if (video.Stars != null && video.Stars.Count > 0)
        {
          foreach (var star in video.Stars)
          {
            if (starAffinity.TryGetValue(star.Name, out var affinity))
            {
              starScore += affinity;
              starMatches++;
              if (affinity > topStarScore) { topStarScore = affinity; topStar = star.Name; }
            }
          }
          if (starMatches > 0)
            starScore /= starMatches; // average only matching stars
        }

        double combinedScore = tagScore * 0.6 + starScore * 0.4;

        // Quality boost
        double siteRating = video.SiteRating ?? 0.5;
        combinedScore *= (0.8 + 0.2 * siteRating);

        // Duration-preference matching
        combinedScore *= ComputeDurationPreference(video, preferredDurationMin, durationStdDev);

        // Freshness boost: videos added within last 14 days get up to +15%
        if (video.AddedUTC.HasValue)
        {
          double daysSinceAdded = (DateTime.UtcNow - video.AddedUTC.Value).TotalDays;
          double freshness = Math.Clamp(1.0 - daysSinceAdded / 14.0, 0.0, 1.0);
          combinedScore *= (1.0 + 0.15 * freshness);
        }

        if (combinedScore > 0)
        {
          // Build reason string from top contributors
          var reasons = new List<string>();
          if (topStar != null) reasons.Add(topStar);
          if (topTag != null) reasons.Add(topTag);
          var reason = reasons.Count > 0 ? string.Join(", ", reasons) : null;

          scored.Add(new ScoredVideo(video, combinedScore, reason));
        }
      }

      // Assemble results with diversity injection and performer rotation
      var result = AssembleDiverseResults(scored, allItems, limit);

      _logger.LogDebug("Generated {Count} recommended videos", result.Count);
      return result;
    }

    public List<ScoredVideo> GetSimilarVideos(long videoId, List<DbVideoItem> allItems, int limit = 20)
    {
      var sourceVideo = allItems.FirstOrDefault(v => v.Id == videoId);
      if (sourceVideo == null)
      {
        _logger.LogWarning("Source video {VideoId} not found for similarity search", videoId);
        return new List<ScoredVideo>();
      }

      var sourceTags = new HashSet<string>(
        (sourceVideo.Tags ?? new List<DbTag>()).Select(t => t.Name),
        StringComparer.OrdinalIgnoreCase);

      var sourceStars = new HashSet<string>(
        (sourceVideo.Stars ?? new List<DbStar>()).Select(s => s.Name),
        StringComparer.OrdinalIgnoreCase);

      var scored = new List<ScoredVideo>();

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

        // 1. Tag Jaccard (weight: 0.40)
        double tagJaccard = 0;
        if (sourceTags.Count > 0 || videoTags.Count > 0)
        {
          int tagIntersection = sourceTags.Intersect(videoTags).Count();
          int tagUnion = sourceTags.Union(videoTags).Count();
          tagJaccard = (double)tagIntersection / tagUnion;
        }

        // 2. Star Jaccard (weight: 0.25) — both starless = perfect match
        double starJaccard;
        if (sourceStars.Count == 0 && videoStars.Count == 0)
        {
          starJaccard = 1.0;
        }
        else if (sourceStars.Count > 0 || videoStars.Count > 0)
        {
          int starIntersection = sourceStars.Intersect(videoStars).Count();
          int starUnion = sourceStars.Union(videoStars).Count();
          starJaccard = (double)starIntersection / starUnion;
        }
        else
        {
          starJaccard = 0;
        }

        // 3. Duration similarity (weight: 0.15) — Gaussian, σ=10min
        double durationDiffMin = Math.Abs(sourceVideo.Duration.TotalMinutes - video.Duration.TotalMinutes);
        double durationSim = Math.Exp(-(durationDiffMin * durationDiffMin) / (2 * 10 * 10));

        // 4. Quality match (weight: 0.10)
        double qualitySim = string.Equals(sourceVideo.Quality, video.Quality, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.5;

        // 5. Rating similarity (weight: 0.10)
        double srcRating = sourceVideo.LocalRating ?? sourceVideo.SiteRating ?? 0.5;
        double cndRating = video.LocalRating ?? video.SiteRating ?? 0.5;
        double ratingSim = 1.0 - Math.Abs(srcRating - cndRating);

        double similarity = tagJaccard * 0.40 + starJaccard * 0.25 + durationSim * 0.15 + qualitySim * 0.10 + ratingSim * 0.10;

        if (similarity > 0.1)
          scored.Add(new ScoredVideo(video, similarity, null));
      }

      // Performer-Fatigue: max limit/5 videos per star
      int maxPerStar = Math.Max(3, limit / 5);
      var starCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var result = new List<ScoredVideo>();

      foreach (var item in scored.OrderByDescending(s => s.Score))
      {
        if (result.Count >= limit)
          break;

        bool overRepresented = item.Video.Stars?.Any(s =>
        {
          starCounts.TryGetValue(s.Name, out int count);
          return count >= maxPerStar;
        }) ?? false;

        if (overRepresented)
          continue;

        result.Add(item);
        if (item.Video.Stars != null)
        {
          foreach (var star in item.Video.Stars)
          {
            starCounts.TryGetValue(star.Name, out int c);
            starCounts[star.Name] = c + 1;
          }
        }
      }

      _logger.LogDebug("Found {Count} similar videos for video {VideoId}", result.Count, videoId);
      return result;
    }

    /// <summary>
    /// Compute preferred duration from signal videos (weighted average + stddev).
    /// </summary>
    private static (double PreferredMinutes, double StdDevMinutes) ComputeDurationPreference(List<DbVideoItem> signalVideos)
    {
      var durations = signalVideos
        .Where(v => v.Duration.TotalMinutes > 1)
        .Select(v => v.Duration.TotalMinutes)
        .ToList();

      if (durations.Count < 3)
        return (0, 0); // Not enough data

      double mean = durations.Average();
      double variance = durations.Sum(d => (d - mean) * (d - mean)) / durations.Count;
      double stdDev = Math.Sqrt(variance);

      // Minimum stddev of 5 minutes to avoid over-penalizing
      return (mean, Math.Max(stdDev, 5));
    }

    /// <summary>
    /// Gaussian fit: videos near the preferred duration get a score multiplier.
    /// Returns 0.85-1.0 (never a strong penalty, only a mild boost for matching duration).
    /// </summary>
    private static double ComputeDurationPreference(DbVideoItem video, double preferredMin, double stdDev)
    {
      if (preferredMin <= 0 || stdDev <= 0 || video.Duration.TotalMinutes <= 0)
        return 1.0; // No preference data, neutral

      double diff = Math.Abs(video.Duration.TotalMinutes - preferredMin);
      double gaussian = Math.Exp(-(diff * diff) / (2 * stdDev * stdDev));

      // Map from [0,1] to [0.97, 1.0] — very minimal influence
      return 0.97 + 0.03 * gaussian;
    }

    /// <summary>
    /// Assemble diverse result list with tag-cluster suppression and performer rotation.
    /// Uses a single-pass approach for performance (no O(n²) MMR).
    /// </summary>
    private static List<ScoredVideo> AssembleDiverseResults(List<ScoredVideo> scored, List<DbVideoItem> allItems, int limit)
    {
      if (scored.Count <= limit)
        return scored.OrderByDescending(s => s.Score).ToList();

      var sorted = scored.OrderByDescending(s => s.Score).ToList();

      var result = new List<ScoredVideo>();
      var selectedIds = new HashSet<long>();
      var starCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

      foreach (var item in sorted)
      {
        if (result.Count >= limit)
          break;

        // Performer rotation: max limit/10 per star
        if (!ApplyPerformerRotation(item, starCounts, limit))
          continue;

        // Tag-cluster suppression: skip if any tag is already in >70% of results (after first 20 items)
        if (result.Count > 20 && IsTagOverRepresented(item, tagCounts, result.Count))
          continue;

        AddToResult(result, selectedIds, starCounts, tagCounts, item);
      }

      return result;
    }

    private static bool ApplyPerformerRotation(ScoredVideo item, Dictionary<string, int> starCounts, int limit)
    {
      int maxPerStar = Math.Max(10, limit / 5);
      return !(item.Video.Stars?.Any(s =>
      {
        starCounts.TryGetValue(s.Name, out int count);
        return count >= maxPerStar;
      }) ?? false);
    }

    private static void AddToResult(List<ScoredVideo> result, HashSet<long> selectedIds,
      Dictionary<string, int> starCounts, Dictionary<string, int> tagCounts, ScoredVideo item)
    {
      result.Add(item);
      selectedIds.Add(item.Video.Id);

      if (item.Video.Stars != null)
        foreach (var star in item.Video.Stars)
        {
          starCounts.TryGetValue(star.Name, out int c);
          starCounts[star.Name] = c + 1;
        }
      if (item.Video.Tags != null)
        foreach (var tag in item.Video.Tags)
        {
          tagCounts.TryGetValue(tag.Name, out int c);
          tagCounts[tag.Name] = c + 1;
        }
    }

    private static bool IsTagOverRepresented(ScoredVideo candidate, Dictionary<string, int> tagCounts, int resultCount)
    {
      double threshold = resultCount * 0.7;
      return candidate.Video.Tags?.Any(t =>
      {
        tagCounts.TryGetValue(t.Name, out int count);
        return count > threshold;
      }) ?? false;
    }

  }
}
