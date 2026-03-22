namespace vrScraper.Services
{
  public class ScraperRegistry : IScraperRegistry
  {
    private readonly Dictionary<string, IVideoScraper> _scrapers;

    public ScraperRegistry(IEnumerable<IVideoScraper> scrapers)
    {
      _scrapers = scrapers.ToDictionary(s => s.SiteName, StringComparer.OrdinalIgnoreCase);
    }

    public IVideoScraper? GetScraperForSite(string siteName)
      => _scrapers.GetValueOrDefault(siteName);

    public IEnumerable<IVideoScraper> GetAllScrapers()
      => _scrapers.Values;

    public IEnumerable<string> GetAllSiteNames()
      => _scrapers.Keys;
  }
}
