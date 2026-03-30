using vrScraper.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace vrScraper.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  [AllowAnonymous]
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

      var result = similar.Select(s => new
      {
        id = s.Video.Id,
        title = s.Video.Title,
        thumbnail = s.Video.Thumbnail,
        duration = (int)s.Video.Duration.TotalSeconds,
        rating = s.Video.SiteRating,
        score = Math.Round(s.Score * 100, 0),
        tags = s.Video.Tags.Select(t => t.Name),
        stars = s.Video.Stars.Select(st => st.Name)
      });

      return Ok(result);
    }
  }
}
