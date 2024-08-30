using deovrScraper.DB;
using deovrScraper.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

/*

namespace deovrScraper.Controllers
{
  [Route("[controller]")]
  [ApiController]
  public class ThumbController(ILogger<DeoVrController> logger, IEpornerScraper scraper, DeovrScraperContext context, IVideoServicecs videoService, IConfiguration config) : ControllerBase
  {
    [HttpGet]
    [Route("{*id}")]
    public async Task Thumb(string id)
    {
      logger.LogInformation("Thumb GET query for {id}", id);

      var filePath = $"{config["DataDir"]}\\eporner.com_{id}";

      if (!System.IO.File.Exists(filePath))
      {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return;
      }

      Response.Headers["Content-Type"] = "image/jpeg";
      Response.Headers["Cache-Control"] = "public,max-age=31536000";
      Response.Headers["Server"] = "Apache";
      Response.Headers["Keep-Alive"] = "timeout=5, max=500";
      Response.Headers["Connection"] = "Keep-Alive";

      try
      {
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
        {
          await fileStream.CopyToAsync(Response.Body);
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while streaming file {filePath}", filePath);
        Response.StatusCode = StatusCodes.Status500InternalServerError;
      }
    }

    [HttpOptions]
    [Route("{*id}")]
    public ActionResult ThumbOptions(string id)
    {
      logger.LogInformation("Thumb OPTIONS query for {id}", id);

      Response.Headers["Connection"] = "Keep-Alive";
      Response.Headers["Keep-Alive"] = "timeout=10";
      Response.Headers["Transfer-Encoding"] = "chunked";

      return Ok();
    }
  }
}

*/