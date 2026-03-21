using vrScraper.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace vrScraper.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class RecommendationController(IVideoService videoService, IRecommendationService recommendationService, ISettingService settingService) : ControllerBase
  {
    [HttpGet("similar/{videoId}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetSimilar(long videoId, [FromQuery] int limit = 10)
    {
      var allItems = await videoService.GetVideoItems();

      // Apply global tag blacklist
      var setting = await settingService.GetSetting("TagBlacklist");
      var globalBlackList = JsonConvert.DeserializeObject<List<string>>(setting?.Value ?? "[]") ?? new List<string>();
      var filtered = allItems.Where(item => !item.Tags.Exists(a => globalBlackList.Any(b => b == a.Name))).ToList();

      var similar = recommendationService.GetSimilarVideos(videoId, filtered, limit);

      var result = similar.Select(v => new
      {
        id = v.Id,
        title = v.Title,
        thumbnail = v.Thumbnail,
        duration = (int)v.Duration.TotalSeconds,
        rating = v.SiteRating,
        tags = v.Tags.Select(t => t.Name),
        stars = v.Stars.Select(s => s.Name)
      });

      return Ok(result);
    }
  }
}
