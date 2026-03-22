namespace vrScraper.Services
{
  public interface IScraperRegistry
  {
    IVideoScraper? GetScraperForSite(string siteName);
    IEnumerable<IVideoScraper> GetAllScrapers();
    IEnumerable<string> GetAllSiteNames();
  }
}
