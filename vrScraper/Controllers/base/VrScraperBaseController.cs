using Microsoft.AspNetCore.Mvc;

namespace vrScraper.Controllers.@base
{
  public class VrScraperBaseController : ControllerBase
  {
    protected string BaseUrl => $"{Request.Scheme}://{Request.Host}";
  }
}
