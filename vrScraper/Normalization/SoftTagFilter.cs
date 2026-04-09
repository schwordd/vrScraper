using vrScraper.DB.Models;

namespace vrScraper.Normalization
{
  public static class SoftTagFilter
  {
    public static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
      // Pronouns & articles
      "you", "your", "she", "her", "he", "him", "his", "has", "had", "have",
      "than", "does", "did", "the", "this", "that", "and", "but", "for", "not",
      "with", "are", "was", "were", "been", "its", "who", "how", "from",
      // Common verbs & adjectives
      "get", "gets", "got", "just", "like", "more", "most", "only",
      "out", "over", "some", "very", "what", "when", "all", "new", "best",
      "fuck", "step", "part", "time", "need", "take", "read", "enjoy", "ever",
      "will", "getting", "wants", "show", "turn", "back", "come", "know",
      "real", "three", "first", "hard", "sweet", "ready", "play", "lucky",
      "fucked", "loves", "head", "perfect", "nice", "special", "great",
      "super", "huge", "another", "gives", "late", "watch", "fire", "hell",
      "fun", "body", "happy", "needs", "wild", "after",
      // Explicit generic
      "dick", "cock", "pussy", "tits", "cum", "little", "deep", "blow",
      "hole", "cream", "big", "horny", "sexy", "slut", "fucking",
      // Noise
      "full", "video", "hot", "completely", "soft", "vr", "hd", "xxx",
      "porn", "sex", "king", "star", "amp"
    };

    /// <summary>
    /// Validates a single soft tag. Returns false if the tag should be filtered out.
    /// </summary>
    public static bool IsValid(string tag, List<DbStar> knownStars)
    {
      if (string.IsNullOrWhiteSpace(tag) || tag.Length < 3)
        return false;

      // Single-word stopword check
      if (Stopwords.Contains(tag))
        return false;

      // Exact star name match (case-insensitive)
      if (knownStars.Any(s => s.Name.Equals(tag, StringComparison.OrdinalIgnoreCase)))
        return false;

      // Multi-word tag checks
      if (tag.Contains(' '))
      {
        // Star phrase filter: tag contains a full star name as substring
        var tagLower = tag.ToLowerInvariant();
        if (knownStars.Where(s => s.Name.Contains(' '))
            .Any(s => tagLower.Contains(s.Name.ToLowerInvariant())))
          return false;

        // All words are stopwords: "fuck her hard", "up her ass"
        var words = tag.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.All(w => Stopwords.Contains(w)))
          return false;
      }

      return true;
    }

    /// <summary>
    /// Finds star-name pairs among a list of single-word tags.
    /// Returns the set of tag names that form a known star name when combined.
    /// </summary>
    public static HashSet<string> FindStarNamePairs(List<string> singleWordTags, List<DbStar> knownStars)
    {
      var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var twoWordStars = knownStars
          .Where(s => s.Name.Contains(' ') && !s.Name.Contains("  "))
          .Select(s => (Full: s.Name, Parts: s.Name.Split(' ')))
          .Where(s => s.Parts.Length == 2)
          .ToList();

      var tagSet = new HashSet<string>(singleWordTags, StringComparer.OrdinalIgnoreCase);

      foreach (var star in twoWordStars)
      {
        if (tagSet.Contains(star.Parts[0]) && tagSet.Contains(star.Parts[1]))
        {
          result.Add(star.Parts[0]);
          result.Add(star.Parts[1]);
        }
      }

      return result;
    }

    /// <summary>
    /// Categorizes pending tags into suggestion groups for the UI.
    /// </summary>
    public static SuggestedDenials GetSuggestions(List<(DbTag Tag, long VideoCount)> pendingTags, List<DbStar> knownStars)
    {
      var suggestions = new SuggestedDenials();
      var classified = new HashSet<long>(); // tag IDs already classified

      // 1. Stopwords (single-word tags in stopword list)
      foreach (var item in pendingTags)
      {
        if (Stopwords.Contains(item.Tag.Name))
        {
          suggestions.StopwordTags.Add(item);
          classified.Add(item.Tag.Id);
        }
      }

      // 2. Multi-word tags where all words are stopwords
      foreach (var item in pendingTags.Where(t => !classified.Contains(t.Tag.Id)))
      {
        if (item.Tag.Name.Contains(' '))
        {
          var words = item.Tag.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
          if (words.All(w => Stopwords.Contains(w)))
          {
            suggestions.AllWordsStopwordTags.Add(item);
            classified.Add(item.Tag.Id);
          }
        }
      }

      // 3. Star phrase tags (contain a full star name)
      var twoWordStars = knownStars.Where(s => s.Name.Contains(' ')).ToList();
      foreach (var item in pendingTags.Where(t => !classified.Contains(t.Tag.Id)))
      {
        if (item.Tag.Name.Contains(' '))
        {
          var tagLower = item.Tag.Name.ToLowerInvariant();
          var matchedStar = twoWordStars.FirstOrDefault(s => tagLower.Contains(s.Name.ToLowerInvariant()));
          if (matchedStar != null)
          {
            suggestions.StarPhraseTags.Add((item.Tag, item.VideoCount, matchedStar.Name));
            classified.Add(item.Tag.Id);
          }
        }
      }

      // 4. Star name fragment pairs
      var singleWordPending = pendingTags
          .Where(t => !classified.Contains(t.Tag.Id) && !t.Tag.Name.Contains(' '))
          .Select(t => t.Tag.Name)
          .ToList();
      var starPairs = FindStarNamePairs(singleWordPending, knownStars);

      foreach (var item in pendingTags.Where(t => !classified.Contains(t.Tag.Id)))
      {
        if (starPairs.Contains(item.Tag.Name))
        {
          suggestions.StarFragmentTags.Add(item);
          classified.Add(item.Tag.Id);
        }
      }

      // 5. Low confidence (only 1 video)
      foreach (var item in pendingTags.Where(t => !classified.Contains(t.Tag.Id)))
      {
        if (item.VideoCount <= 1)
        {
          suggestions.LowConfidenceTags.Add(item);
          classified.Add(item.Tag.Id);
        }
      }

      // Remaining: not classified
      suggestions.RemainingTags = pendingTags.Where(t => !classified.Contains(t.Tag.Id)).ToList();

      return suggestions;
    }
  }

  public class SuggestedDenials
  {
    public List<(DbTag Tag, long VideoCount)> StopwordTags { get; set; } = new();
    public List<(DbTag Tag, long VideoCount)> AllWordsStopwordTags { get; set; } = new();
    public List<(DbTag Tag, long VideoCount, string MatchedStar)> StarPhraseTags { get; set; } = new();
    public List<(DbTag Tag, long VideoCount)> StarFragmentTags { get; set; } = new();
    public List<(DbTag Tag, long VideoCount)> LowConfidenceTags { get; set; } = new();
    public List<(DbTag Tag, long VideoCount)> RemainingTags { get; set; } = new();

    public int TotalSuggestions => StopwordTags.Count + AllWordsStopwordTags.Count
        + StarPhraseTags.Count + StarFragmentTags.Count + LowConfidenceTags.Count;
  }
}
