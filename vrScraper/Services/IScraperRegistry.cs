namespace vrScraper.Services
{
  public interface IScraperRegistry
  {
    IVideoScraper? GetScraperForSite(string siteName);
    IEnumerable<IVideoScraper> GetAllScrapers();
    IEnumerable<string> GetAllSiteNames();
    IEnumerable<IVideoScraper> GetEnabledScrapers();
    IEnumerable<string> GetEnabledSiteNames();
  }
}
