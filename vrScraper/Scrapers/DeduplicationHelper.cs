namespace vrScraper.Scrapers
{
  public static class DeduplicationHelper
  {
    public static bool IsProbableDuplicate(string newTitle, TimeSpan newDuration,
        IEnumerable<(string Title, TimeSpan Duration)> existingVideos,
        double titleSimilarityThreshold = 0.85,
        int durationToleranceSeconds = 10)
    {
      var normalizedNew = NormalizeTitle(newTitle);
      foreach (var existing in existingVideos)
      {
        if (Math.Abs((newDuration - existing.Duration).TotalSeconds) > durationToleranceSeconds)
          continue;
        var similarity = CalculateJaccardSimilarity(normalizedNew, NormalizeTitle(existing.Title));
        if (similarity >= titleSimilarityThreshold)
          return true;
      }
      return false;
    }

    private static string NormalizeTitle(string title)
    {
      return title.ToLowerInvariant()
        .Replace("4k", "").Replace("5k", "").Replace("6k", "").Replace("8k", "")
        .Trim();
    }

    private static double CalculateJaccardSimilarity(string a, string b)
    {
      var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
      var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
      if (wordsA.Count == 0 && wordsB.Count == 0) return 1.0;
      if (wordsA.Count == 0 || wordsB.Count == 0) return 0.0;
      return (double)wordsA.Intersect(wordsB).Count() / wordsA.Union(wordsB).Count();
    }
  }
}
