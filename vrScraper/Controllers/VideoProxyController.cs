using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services;
using vrScraper.Services.Interfaces;
using vrScraper.Scrapers.Interfaces;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using System.Text.RegularExpressions;

namespace vrScraper.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class VideoProxyController : ControllerBase
    {
        private readonly ILogger<VideoProxyController> _logger;
        private readonly IScraperRegistry _scraperRegistry;
        private readonly VrScraperContext _context;
        private readonly IVideoService _videoService;
        private readonly IHttpClientFactory _httpClientFactory;

        public VideoProxyController(
            ILogger<VideoProxyController> logger,
            IScraperRegistry scraperRegistry,
            VrScraperContext context,
            IVideoService videoService,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _scraperRegistry = scraperRegistry;
            _context = context;
            _videoService = videoService;
            _httpClientFactory = httpClientFactory;
        }

        // GET: api/VideoProxy/{videoId}
        [HttpGet("{videoId}")]
        public async Task<IActionResult> Get(long videoId)
        {
            HttpResponseMessage? response = null;
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

                // Scraper für die Site ermitteln
                var scraper = _scraperRegistry.GetScraperForSite(video.Site);
                if (scraper == null)
                {
                    _logger.LogWarning("Kein Scraper für Site {Site} gefunden", video.Site);
                    return NotFound("Kein Scraper für diese Site");
                }

                // Extrahiere Range-Header, falls vorhanden
                string rangeHeader = Request.Headers["Range"].ToString();
                bool hasRangeHeader = !string.IsNullOrEmpty(rangeHeader);

                // Quellauflösung (Live-Scrape) und Upstream-Fetch sind beide externe
                // Netzwerkaufrufe, die gelegentlich transient fehlschlagen. Genau das
                // verursacht das sporadische 500 (~1/10), das nach Vor-/Zurückschalten
                // wieder verschwindet. Deshalb hier bis zu maxAttempts wiederholen und
                // die (kurzlebige) Quell-URL bei jedem Versuch neu auflösen.
                const int maxAttempts = 3;
                int lastUpstreamStatus = 0;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    // Video-Quelle abrufen (frische, ggf. signierte URL pro Versuch)
                    VideoSource? source;
                    try
                    {
                        source = await scraper.GetSource(video, _context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "GetSource fehlgeschlagen (Versuch {Attempt}/{Max}) für Video {VideoId}", attempt, maxAttempts, videoId);
                        source = null;
                    }

                    if (source == null)
                    {
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(400 * attempt);
                            continue;
                        }
                        _logger.LogWarning("Keine Video-Quelle für Video {VideoId} nach {Max} Versuchen gefunden", videoId, maxAttempts);
                        return NotFound("Video-Quelle nicht gefunden");
                    }

                    // Erstelle einen HttpClient mit geeigneten Headers
                    var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Clear();

                    // Standard-Headers
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var proxyHeaders = scraper.GetProxyHeaders();
                    foreach (var header in proxyHeaders)
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    client.DefaultRequestHeaders.Add("Accept", "*/*");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

                    // Range-Header hinzufügen, falls vorhanden
                    if (hasRangeHeader)
                    {
                        client.DefaultRequestHeaders.Add("Range", rangeHeader);
                        _logger.LogInformation("Range-Request erkannt: {Range}", rangeHeader);
                    }

                    try
                    {
                        // Stream die Video-Datei durch
                        response = await client.GetAsync(source.Src, HttpCompletionOption.ResponseHeadersRead);
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                    {
                        _logger.LogWarning(ex, "Fehler beim Abrufen des Videos von der Quelle (Versuch {Attempt}/{Max}): {Url}", attempt, maxAttempts, source.Src);
                        response?.Dispose();
                        response = null;
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(400 * attempt);
                            continue;
                        }
                        return StatusCode(502, "Fehler beim Abrufen des Videos von der Quelle");
                    }

                    // Prüfe, ob der Request erfolgreich war
                    if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PartialContent)
                    {
                        lastUpstreamStatus = (int)response.StatusCode;
                        bool transient = lastUpstreamStatus >= 500
                            || response.StatusCode == HttpStatusCode.TooManyRequests
                            || response.StatusCode == HttpStatusCode.Forbidden
                            || response.StatusCode == HttpStatusCode.RequestTimeout;
                        _logger.LogWarning("Fehler beim Abrufen des Videos: StatusCode {StatusCode} (Versuch {Attempt}/{Max})", response.StatusCode, attempt, maxAttempts);
                        response.Dispose();
                        response = null;
                        if (transient && attempt < maxAttempts)
                        {
                            await Task.Delay(400 * attempt);
                            continue;
                        }
                        return StatusCode(lastUpstreamStatus, "Fehler beim Abrufen des Videos");
                    }

                    // Erfolg: Antwort durchstreamen (verlässt die Retry-Schleife per return).
                    // Besitz des Streams geht an StreamResponse/FileStreamResult über,
                    // deshalb lokale Referenz nullen, damit der finally-Block sie nicht disposed.
                    var streamResult = await StreamResponse(response, hasRangeHeader);
                    response = null;
                    return streamResult;
                }

                // Sollte nicht erreicht werden (Schleife endet per return), aber zur Sicherheit:
                return StatusCode(lastUpstreamStatus == 0 ? 502 : lastUpstreamStatus, "Fehler beim Abrufen des Videos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ein Fehler ist beim Verarbeiten der Proxy-Anfrage aufgetreten");
                return StatusCode(500, "Ein interner Serverfehler ist aufgetreten");
            }
            finally
            {
                response?.Dispose();
            }
        }

        // Kopiert die relevanten Header der Upstream-Antwort in die Client-Antwort und
        // streamt den Inhalt durch. Der Stream-Besitz geht an das FileStreamResult über.
        private async Task<IActionResult> StreamResponse(HttpResponseMessage response, bool hasRangeHeader)
        {
            try
            {
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
                if (response.Content.Headers.TryGetValues("Content-Range", out var contentRangeValues))
                {
                    string? contentRangeHeader = contentRangeValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(contentRangeHeader))
                    {
                        Response.Headers["Content-Range"] = contentRangeHeader;
                        _logger.LogInformation("Content-Range gesetzt: {ContentRange}", contentRangeHeader);
                    }
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
                // Note: FileStreamResult will dispose the stream when done.
                // The response object is intentionally not disposed here because
                // the stream is owned by the response and must remain alive.
                var videoStream = await response.Content.ReadAsStreamAsync();
                response = null; // Prevent disposal in finally block; stream ownership transfers to FileStreamResult
                return new FileStreamResult(videoStream, Response.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ein Fehler ist beim Verarbeiten der Proxy-Anfrage aufgetreten");
                return StatusCode(500, "Ein interner Serverfehler ist aufgetreten");
            }
            finally
            {
                response?.Dispose();
            }
        }

        // GET: api/VideoProxy/download/{videoId}
        [HttpGet("download/{videoId}")]
        public async Task<IActionResult> Download(long videoId)
        {
            HttpResponseMessage? response = null;
            try
            {
                _logger.LogInformation("Video download request for videoId {VideoId}", videoId);

                // Video aus der Datenbank abrufen
                var video = await _videoService.GetVideoById(videoId);
                if (video == null)
                {
                    _logger.LogWarning("Video mit ID {VideoId} nicht gefunden", videoId);
                    return NotFound("Video nicht gefunden");
                }

                // Scraper für die Site ermitteln
                var scraper = _scraperRegistry.GetScraperForSite(video.Site);
                if (scraper == null)
                {
                    _logger.LogWarning("Kein Scraper für Site {Site} gefunden", video.Site);
                    return NotFound("Kein Scraper für diese Site");
                }

                // Video-Quelle abrufen
                var source = await scraper.GetSource(video, _context);
                if (source == null)
                {
                    _logger.LogWarning("Keine Video-Quelle für Video {VideoId} gefunden", videoId);
                    return NotFound("Video-Quelle nicht gefunden");
                }

                // Erstelle einen HttpClient mit geeigneten Headers
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.Timeout = TimeSpan.FromMinutes(30); // Erhöhe Timeout für große Downloads

                // Standard-Headers
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var proxyHeaders = scraper.GetProxyHeaders();
                foreach (var header in proxyHeaders)
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                client.DefaultRequestHeaders.Add("Accept", "*/*");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

                try
                {
                    // Jetzt hole das eigentliche Video
                    response = await client.GetAsync(source.Src, HttpCompletionOption.ResponseHeadersRead);

                    // Prüfe, ob der Request erfolgreich war
                    if (!response.IsSuccessStatusCode)
                    {
                        var statusCode = (int)response.StatusCode;
                        _logger.LogWarning("Fehler beim Herunterladen des Videos: StatusCode {StatusCode}", response.StatusCode);
                        response.Dispose();
                        response = null;
                        return StatusCode(statusCode, "Fehler beim Herunterladen des Videos");
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Fehler beim Herunterladen des Videos von der Quelle: {Url}", source.Src);
                    return StatusCode(500, "Fehler beim Herunterladen des Videos von der Quelle");
                }

                // Erstelle einen sicheren Dateinamen
                var safeTitle = Regex.Replace(video.Title ?? "video", @"[^a-zA-Z0-9_-]", "_");
                var fileName = $"{safeTitle}_{video.SiteVideoId}.mp4";

                // Setze Headers für Download
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "video/mp4";

                // Setze Content-Length wenn verfügbar
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    Response.Headers["Content-Length"] = response.Content.Headers.ContentLength.Value.ToString();
                    _logger.LogInformation("Content-Length für Download: {ContentLength} bytes", response.Content.Headers.ContentLength.Value);
                }

                // CORS Headers für bessere Kompatibilität
                Response.Headers["Access-Control-Allow-Origin"] = "*";
                Response.Headers["Access-Control-Expose-Headers"] = "Content-Length, Content-Disposition";

                // Stream die Daten mit größerem Buffer für bessere Performance
                var videoStream = await response.Content.ReadAsStreamAsync();

                // Verwende FileStreamResult mit enableRangeProcessing für bessere Download-Performance
                // Note: FileStreamResult will dispose the stream when done.
                // The response object is intentionally not disposed here because
                // the stream is owned by the response and must remain alive.
                response = null; // Prevent disposal in finally block; stream ownership transfers to FileStreamResult
                return new FileStreamResult(videoStream, Response.ContentType)
                {
                    EnableRangeProcessing = false,
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ein Fehler ist beim Verarbeiten der Download-Anfrage aufgetreten");
                return StatusCode(500, "Ein interner Serverfehler ist aufgetreten");
            }
            finally
            {
                response?.Dispose();
            }
        }
    }
}
