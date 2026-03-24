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
    string NormalizeTitle(string title);
    bool IsObfuscated(string title);
    List<(DbStar Star, double Confidence)> DetectStars(string normalizedTitle, List<DbStar> knownStars);
    List<DbTag> DetectTags(string normalizedTitle, List<DbTag> knownTags);
    Task<int> NormalizeAllTitles(bool forceReprocess = false, NormalizationProgress? progress = null);
  }
}
