using vrScraper.DB.Models;

namespace vrScraper.Services
{
  public class NormalizationProgress
  {
    public int Current;
    public int Total;
    public int StarsDetected;
    public int TagsDetected;
    public int TitlesNormalized;
    public string Phase = "Initializing";
  }

  public interface ITitleNormalizationService
  {
    string? NormalizeTitle(string title);
    string NormalizeTitleLegacy(string title);
    bool IsObfuscated(string title);
    List<(DbStar Star, double Confidence)> DetectStars(string normalizedTitle, List<DbStar> knownStars);
    List<DbTag> DetectTags(string normalizedTitle, List<DbTag> knownTags);
    Task<int> NormalizeAllTitles(bool forceReprocess = false, bool normalizeTitles = true, bool detectStars = true, bool detectTags = true, NormalizationProgress? progress = null, Action<string, string?, string>? onTitleProcessed = null, CancellationToken ct = default);
  }
}
