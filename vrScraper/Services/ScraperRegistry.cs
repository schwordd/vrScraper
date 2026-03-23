namespace vrScraper.Services
{
  public class ScraperRegistry : IScraperRegistry
  {
    private readonly Dictionary<string, IVideoScraper> _scrapers;
    private readonly ISettingService _settingService;

    public ScraperRegistry(IEnumerable<IVideoScraper> scrapers, ISettingService settingService)
    {
      _scrapers = scrapers.ToDictionary(s => s.SiteName, StringComparer.OrdinalIgnoreCase);
      _settingService = settingService;
    }

    public IVideoScraper? GetScraperForSite(string siteName)
      => _scrapers.GetValueOrDefault(siteName);

    public IEnumerable<IVideoScraper> GetAllScrapers()
      => _scrapers.Values;

    public IEnumerable<string> GetAllSiteNames()
      => _scrapers.Keys;

    public IEnumerable<IVideoScraper> GetEnabledScrapers()
      => _scrapers.Values.Where(s => IsSiteEnabled(s.SiteName));

    public IEnumerable<string> GetEnabledSiteNames()
      => _scrapers.Keys.Where(IsSiteEnabled);

    private bool IsSiteEnabled(string siteName)
    {
      var defaultValue = siteName.Equals("eporner.com", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
      var value = _settingService.GetSettingValue($"Site:{siteName}:Enabled") ?? defaultValue;
      return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
  }
}
