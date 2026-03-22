using vrScraper.Services;
using Microsoft.AspNetCore.Mvc;

namespace vrScraper.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class AdminController(IScraperRegistry scraperRegistry) : ControllerBase
  {
    [HttpGet("status")]
    public IActionResult Status()
    {
      var status = scraperRegistry.GetAllScrapers().Select(s => new
      {
        site = s.SiteName,
        displayName = s.DisplayName,
        inProgress = s.ScrapingInProgress,
        status = s.ScrapingStatus,
        currentTitle = s.CurrentVideoTitle
      });
      return Ok(status);
    }

    [HttpPost("scrape")]
    public IActionResult Scrape([FromQuery] string site, [FromQuery] int pages = 3)
    {
      var scraper = scraperRegistry.GetScraperForSite(site);
      if (scraper == null) return NotFound($"No scraper for site: {site}");
      if (scraper.ScrapingInProgress) return Conflict("Scraping already in progress");

      scraper.StartScraping(1, pages);
      return Ok($"Started scraping {site} for {pages} pages");
    }

    [HttpPost("stop")]
    public IActionResult Stop([FromQuery] string site)
    {
      var scraper = scraperRegistry.GetScraperForSite(site);
      if (scraper == null) return NotFound($"No scraper for site: {site}");
      scraper.StopScraping();
      return Ok($"Stopped scraping {site}");
    }
  }
}
