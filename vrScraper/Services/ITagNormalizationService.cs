namespace vrScraper.Services
{
  public interface ITagNormalizationService
  {
    string NormalizeTag(string tagName);
    Dictionary<string, List<string>> GetSynonymMap();
    Task SaveSynonymMap(Dictionary<string, List<string>> map);
    Task NormalizeAllExistingTags();
  }
}
