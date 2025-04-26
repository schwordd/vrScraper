using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using System.Text.RegularExpressions;

namespace vrScraper.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoProxyController : ControllerBase
    {
        private readonly ILogger<VideoProxyController> _logger;
        private readonly IEpornerScraper _scraper;
        private readonly VrScraperContext _context;
        private readonly IVideoService _videoService;
        private readonly IHttpClientFactory _httpClientFactory;

        public VideoProxyController(
            ILogger<VideoProxyController> logger,
            IEpornerScraper scraper,
            VrScraperContext context,
            IVideoService videoService,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _scraper = scraper;
            _context = context;
            _videoService = videoService;
            _httpClientFactory = httpClientFactory;
        }

        // GET: api/VideoProxy/{videoId}
        [HttpGet("{videoId}")]
        public async Task<IActionResult> Get(long videoId)
        {
            try
            {
                _logger.LogInformation("Video proxy request for videoId {VideoId}", videoId);

                // Video aus der Datenbank abrufen
                var video = await _videoService.GetVideoById(videoId);
                if (video == null)
                {
                    _logger.LogWarning("Video mit ID {VideoId} nicht gefunden", videoId);
                    return NotFound("Video nicht gefunden");
                }

                // Video-Quelle abrufen
                var source = await _scraper.GetSource(video, _context);
                if (source == null)
                {
                    _logger.LogWarning("Keine Video-Quelle für Video {VideoId} gefunden", videoId);
                    return NotFound("Video-Quelle nicht gefunden");
                }

                // Erhöhe den Play Count
                _videoService.SetPlayedVideo(video);

                // Extrahiere Range-Header, falls vorhanden
                string rangeHeader = Request.Headers["Range"].ToString();
                bool hasRangeHeader = !string.IsNullOrEmpty(rangeHeader);

                // Erstelle einen HttpClient mit geeigneten Headers
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();

                // Standard-Headers
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Referer", "https://www.eporner.com/");
                client.DefaultRequestHeaders.Add("Accept", "*/*");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                client.DefaultRequestHeaders.Add("Origin", "https://www.eporner.com");

                // Range-Header hinzufügen, falls vorhanden
                if (hasRangeHeader)
                {
                    client.DefaultRequestHeaders.Add("Range", rangeHeader);
                    _logger.LogInformation("Range-Request erkannt: {Range}", rangeHeader);
                }

                HttpResponseMessage response;
                try
                {
                    // Stream die Video-Datei durch
                    response = await client.GetAsync(source.Src, HttpCompletionOption.ResponseHeadersRead);

                    // Prüfe, ob der Request erfolgreich war
                    if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PartialContent)
                    {
                        _logger.LogWarning("Fehler beim Abrufen des Videos: StatusCode {StatusCode}", response.StatusCode);
                        return StatusCode((int)response.StatusCode, "Fehler beim Abrufen des Videos");
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Fehler beim Abrufen des Videos von der Quelle: {Url}", source.Src);
                    return StatusCode(500, "Fehler beim Abrufen des Videos von der Quelle");
                }

                // Status-Code setzen basierend auf der Antwort
                Response.StatusCode = (int)(hasRangeHeader && response.StatusCode == HttpStatusCode.PartialContent
                    ? HttpStatusCode.PartialContent
                    : HttpStatusCode.OK);

                // Kopiere alle relevanten Header aus der Antwort
                foreach (var header in response.Headers)
                {
                    // Einige Header müssen ausgelassen werden, um CORS oder andere Konflikte zu vermeiden
                    if (!new[] { "Connection", "Transfer-Encoding", "Keep-Alive" }.Contains(header.Key))
                    {
                        Response.Headers[header.Key] = header.Value.ToArray();
                    }
                }

                foreach (var header in response.Content.Headers)
                {
                    if (!new[] { "Content-Length", "Content-Type", "Content-Range" }.Contains(header.Key))
                    {
                        Response.Headers[header.Key] = header.Value.ToArray();
                    }
                }

                // Content-Type immer explizit setzen
                Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "video/mp4";

                // Content-Length und Content-Range werden speziell behandelt
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    Response.Headers["Content-Length"] = response.Content.Headers.ContentLength.Value.ToString();
                }

                // Content-Range für Partial-Content setzen, wenn vorhanden
                string contentRangeHeader = response.Content.Headers.GetValues("Content-Range").FirstOrDefault();
                if (!string.IsNullOrEmpty(contentRangeHeader))
                {
                    Response.Headers["Content-Range"] = contentRangeHeader;
                    _logger.LogInformation("Content-Range gesetzt: {ContentRange}", contentRangeHeader);
                }

                // Wichtige Streaming-Header hinzufügen
                Response.Headers["Accept-Ranges"] = "bytes";

                // CORS-Headers hinzufügen
                Response.Headers["Access-Control-Allow-Origin"] = "*";
                Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
                Response.Headers["Access-Control-Allow-Headers"] = "Range, Accept, Accept-Encoding, Content-Type";
                Response.Headers["Access-Control-Expose-Headers"] = "Content-Length, Content-Range, Accept-Ranges, Content-Type";
                Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";

                // Stream die Daten
                var videoStream = await response.Content.ReadAsStreamAsync();
                return new FileStreamResult(videoStream, Response.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ein Fehler ist beim Verarbeiten der Proxy-Anfrage aufgetreten");
                return StatusCode(500, "Ein interner Serverfehler ist aufgetreten");
            }
        }
    }
}
