namespace vrScraper.Scrapers.Interfaces
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
