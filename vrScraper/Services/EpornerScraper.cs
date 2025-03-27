using HtmlAgilityPack;
using Newtonsoft.Json;
using RestSharp;
using vrScraper.DB;
using vrScraper.DB.Models;
using Microsoft.EntityFrameworkCore;
using vrScraper.Services.ParsingModels;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Threading;

namespace vrScraper.Services
{
  public class EpornerScraper(ILogger<EpornerScraper> logger, IServiceProvider serviceProvider, IVideoService vs) : IEpornerScraper
  {
    public bool ScrapingInProgress => this._scrapingInprogress;

    public string ScrapingStatus => this._scrapingStatus;

    private bool _scrapingInprogress = false;
    private string _scrapingStatus = string.Empty;
    private static readonly string[] separator = ["\r\n", "\r", "\n"];
    private CancellationTokenSource? _cancellationTokenSource;

    public void Initialize()
    {
      logger.LogInformation("EpornerScraper initialized.");
    }

    public void StopScraping()
    {
      if (_cancellationTokenSource != null)
      {
        logger.LogInformation("Stopping scraping process...");
        _cancellationTokenSource.Cancel();
        _scrapingStatus = "Stopping...";
      }
    }

    private void InitializeNewScraping()
    {
      if (_cancellationTokenSource != null)
      {
        _cancellationTokenSource.Dispose();
      }
      _cancellationTokenSource = new CancellationTokenSource();
    }

    public void StartScraping(int start, int count)
    {
      logger.LogInformation("StartScraping called with start={Start}, count={Count}", start, count);

      if (this._scrapingInprogress)
      {
        logger.LogWarning("Scraping already in progress, ignoring request");
        return;
      }

      this._scrapingInprogress = true;
      InitializeNewScraping();
      logger.LogInformation("Starting scraping process");

      Task.Run(async () =>
      {
        try
        {
          logger.LogInformation("Starting ScrapeEporner task");
          await this.ScrapeEporner("https://www.eporner.com/cat/vr-porn", start, count, _cancellationTokenSource!.Token);
          logger.LogInformation("ScrapeEporner task completed");
        }
        catch (OperationCanceledException)
        {
          logger.LogInformation("Scraping process was cancelled");
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error during scraping process");
        }
        finally
        {
          this._scrapingInprogress = false;
          this._scrapingStatus = string.Empty;
          logger.LogInformation("Scraping process finished");
        }
      });
    }

    public void StartRemoveByDeadPicture()
    {
      if (this._scrapingInprogress) return;

      this._scrapingInprogress = true;
      InitializeNewScraping();
      logger.LogInformation("Starting dead thumbnail check");

      Task.Run(async () =>
      {
        try
        {
          logger.LogInformation("Starting RemoveDeadByPicture task");
          await this.RemoveDeadByPicture(_cancellationTokenSource!.Token);
          logger.LogInformation("RemoveDeadByPicture task completed");
        }
        catch (OperationCanceledException)
        {
          logger.LogInformation("Dead thumbnail check was cancelled");
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error during dead thumbnail check");
        }
        finally
        {
          this._scrapingInprogress = false;
          this._scrapingStatus = string.Empty;
          logger.LogInformation("Dead thumbnail check finished");
        }
      });
    }

    public void StartDeleteErrorItems()
    {
      if (this._scrapingInprogress) return;

      this._scrapingInprogress = true;
      InitializeNewScraping();
      logger.LogInformation("Starting error items deletion");

      Task.Run(async () =>
      {
        try
        {
          logger.LogInformation("Starting DeleteErrorItems task");
          await this.DeleteErrorItems(_cancellationTokenSource!.Token);
          logger.LogInformation("DeleteErrorItems task completed");
        }
        catch (OperationCanceledException)
        {
          logger.LogInformation("Error items deletion was cancelled");
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error during error items deletion");
        }
        finally
        {
          this._scrapingInprogress = false;
          this._scrapingStatus = string.Empty;
          logger.LogInformation("Error items deletion finished");
        }
      });
    }

    public void StartReparseInformations()
    {
      logger.LogInformation("StartReparseInformations called");

      if (this._scrapingInprogress)
      {
        logger.LogWarning("Reparse already in progress, ignoring request");
        return;
      }

      this._scrapingInprogress = true;
      InitializeNewScraping();
      logger.LogInformation("Starting reparse process");

      Task.Run(async () =>
      {
        try
        {
          logger.LogInformation("Starting ReparseInformations task");
          await this.ReparseInformations(_cancellationTokenSource!.Token);
          logger.LogInformation("ReparseInformations task completed");
        }
        catch (OperationCanceledException)
        {
          logger.LogInformation("Reparse process was cancelled");
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error during reparse process");
        }
        finally
        {
          this._scrapingInprogress = false;
          this._scrapingStatus = string.Empty;
          logger.LogInformation("Reparse process finished");
        }
      });
    }

    private async Task ScrapeEporner(string url, int startIndex, int pages = 10, CancellationToken cancellationToken = default)
    {
      logger.LogInformation("ScrapeEporner started with url={Url}, startIndex={StartIndex}, pages={Pages}", url, startIndex, pages);

      var totalList = new List<VideoItem>();
      var newInsertions = new List<DbVideoItem>();

      var rnd = new Random();
      var index = startIndex;

      while (!cancellationToken.IsCancellationRequested)
      {
        this._scrapingStatus = $"Total items found : {totalList.Count} -  Processing page {index} ...";
        logger.LogInformation(this._scrapingStatus);

        var foundPageItems = await ScrapeSinglePage($"{url}/{index}/");

        if (foundPageItems.Count > 0)
        {
          totalList.AddRange(foundPageItems);
          totalList = totalList.DistinctBy(a => a.VideoId).ToList();
          index++;
        }
        else
        {
          logger.LogInformation($"NO more results on page {index}. Giving up");
          break;
        }

        if (pages != -1 && index > pages)
        {
          logger.LogInformation($"Limit of pages ({pages}) reached. Stopping.");
          break;
        }

        await Task.Delay(rnd.Next(500, 1000), cancellationToken);
      }

      cancellationToken.ThrowIfCancellationRequested();

      totalList = totalList.Where(v => v.IsVr == true && v.Quality.Contains("4K")).ToList();

      try
      {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
        var site = "eporner.com";

        totalList = totalList.DistinctBy(a => a.VideoId).ToList();

        foreach (var item in totalList)
        {
          cancellationToken.ThrowIfCancellationRequested();

          if (context.VideoItems.Any(a => a.Site == site && a.SiteVideoId == item.VideoId))
          {
            continue;
          }

          var dbVideoItem = new DB.Models.DbVideoItem()
          {
            //ID --> GENERATED
            Site = "eporner.com",
            SiteVideoId = item.VideoId,
            DataVp = item.DataVp,
            Duration = item.Duration ?? TimeSpan.FromSeconds(0),
            IsVr = item.IsVr,
            Link = item.Link,
            Quality = item.Quality,
            SiteRating = item.Rating,
            Thumbnail = item.Thumbnail,
            Title = item.Title ?? string.Empty,
            Uploader = item.Uploader,
            Views = item.Views,
            ParsedDetails = false,
            AddedUTC = DateTime.UtcNow
          };

          context.VideoItems.Add(dbVideoItem);
          newInsertions.Add(dbVideoItem);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation($"{newInsertions.Count} new videos found and added to database");

        logger.LogInformation($"Parsing details (tags and artists) of {newInsertions.Count} new videos...this will take a while");

        for (var i = 0; i < newInsertions.Count; i++)
        {
          cancellationToken.ThrowIfCancellationRequested();
          this._scrapingStatus = $"Parse Details: {i + 1} / {newInsertions.Count}";
          logger.LogInformation(this._scrapingStatus);
          await ParseDetails(newInsertions[i], context);
          await Task.Delay(100, cancellationToken);
        }

        await vs.Initialize();

        logger.LogInformation($"Scraping complete! Hf");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while scraping.");
        throw;
      }
    }

    private async Task<List<VideoItem>> ScrapeSinglePage(string url)
    {
      var web = new HtmlWeb();
      var doc = await web.LoadFromWebAsync(url);

      var nodes = doc.DocumentNode.SelectNodes("//div[@class='mb hdy']");
      var videoItems = ParseVideoItems(nodes, "https://www.eporner.com");

      return videoItems;
    }

    public async Task<(VideoPlayerSettings PlayerSettings, List<string> Tags, List<string> Stars, AdditionalVideoDetails VideoDetails)> GetDetails(DbVideoItem item)
    {
      var web = new HtmlWeb();
      var doc = await web.LoadFromWebAsync(item.Link);

      var lines = doc.ParsedText.Split(separator, StringSplitOptions.None).ToList(); ;
      var videoInfos = lines.Where(a => a.StartsWith("EP.video.player.")).ToList();
      var settings = ParseVideoPlayerSettings(videoInfos);

      var pStars = doc.DocumentNode.SelectNodes("//li[contains(@class, 'vit-pornstar')]");
      var categories = doc.DocumentNode.SelectNodes("//li[contains(@class, 'vit-category')]");
      var vitTag = doc.DocumentNode.SelectNodes("//li[contains(@class, 'vit-tag')]");
      var jsonNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");

      var videoDetails = new AdditionalVideoDetails();

      if (jsonNodes != null)
      {
        foreach (var node in jsonNodes)
        {
          // JSON parsen
          var jsonObject = JsonDocument.Parse(node.InnerText);

          // Prüfen, ob @type = "VideoObject" existiert
          if (jsonObject.RootElement.TryGetProperty("@type", out var typeProperty) && typeProperty.GetString() == "VideoObject")
          {
            jsonObject.RootElement.TryGetProperty("name", out var nameCandidate);
            videoDetails.Name = nameCandidate.GetString();

            jsonObject.RootElement.TryGetProperty("bitrate", out var bitrateCandidate);
            videoDetails.Bitrate = bitrateCandidate.GetString();

            jsonObject.RootElement.TryGetProperty("width", out var widthCandidate);
            videoDetails.Width = Convert.ToUInt16(widthCandidate.GetString());

            jsonObject.RootElement.TryGetProperty("height", out var heightCandidate);
            videoDetails.Height = Convert.ToUInt16(heightCandidate.GetString());

            jsonObject.RootElement.TryGetProperty("description", out var descriptionCandidate);
            videoDetails.Description = descriptionCandidate.GetString();

            jsonObject.RootElement.TryGetProperty("uploadDate", out var uploadDateCandidate);

            if (uploadDateCandidate.GetString() != null)
              videoDetails.UploadDate = DateTime.ParseExact(uploadDateCandidate.GetString()!, "yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
          }
        }
      }

      var stars = new List<string>();
      var tags = new List<string>();

      if (pStars != null)
      {
        foreach (var t in pStars)
        {
          stars.Add(t.InnerText);
        }
      }

      if (categories != null)
      {
        foreach (var t in categories)
        {
          tags.Add(t.InnerText);
        }
      }

      return (settings, tags, stars, videoDetails);
    }

    public async Task<Quality?> GetBestVideoQuality(DbVideoItem item, VideoPlayerSettings settings)
    {
      if (settings == null || settings.Hash == null) return null;

      var b36Hash = ConvertToBase36(settings.Hash);
      var vidId = settings.Vid;

      var client = new RestClient($"https://www.eporner.com");
      var request = new RestRequest($"xhr/video/{vidId}", Method.Get);
      request.AddParameter("hash", b36Hash);
      request.AddParameter("domain", "www.eporner.com");

      var response = await client.ExecuteAsync(request);
      if (response.IsSuccessful && response.Content != null)
      {
        var videoResponse = JsonConvert.DeserializeObject<VideoItemDetails>(response.Content);

        if (videoResponse == null)
          return null;

        return videoResponse.Sources.Mp4.HighestQuality;
      }
      else
      {
        return null;
      }
    }

    public async Task<VideoSource?> GetSource(DbVideoItem video, VrScraperContext context)
    {
      var (PlayerSettings, Tags, Stars, VideoDetails) = await this.GetDetails(video);
      var quality = await this.GetBestVideoQuality(video, PlayerSettings);

      if (quality == null)
      {
        var dbItem = await context.VideoItems.Where(v => v.Id == video.Id).FirstAsync();

        if (dbItem.ErrorCount == null)
        {
          dbItem.ErrorCount = 1;
        }
        else
        {
          dbItem.ErrorCount++;
        }

        await context.SaveChangesAsync();
        video.ErrorCount = dbItem.ErrorCount;

        return null;
      }

      var source = new VideoSource
      {
        Default = quality.Default,
        Resolution = quality.Resolution,
        Src = quality.Src,
        LabelShort = quality.LabelShort,
        Type = quality.Type
      };

      await context.SaveChangesAsync();

      return source;
    }

    public async Task ParseDetails(DbVideoItem video, VrScraperContext context)
    {
      var (PlayerSettings, Tags, Stars, VideoDetails) = await this.GetDetails(video);

      foreach (var starParsed in Stars.Distinct().ToList())
      {
        var star = await context.Stars.Where(s => s.Name == starParsed).FirstOrDefaultAsync();
        if (star == null)
        {
          star = new DbStar() { Name = starParsed };
          context.Stars.Add(star);
          star.Videos = [];
        }

        star.Videos ??= [];

        if (star.Videos.Any(s => s.Id == video.Id))
        {
          //logger.LogInformation("Star {s} already exists for video {v}", star.Name, video.Id);
        }
        else
        {
          star.Videos.Add(video);
          logger.LogInformation("Star {s} added for video {v}", star.Name, video.Id);
        }
      }

      foreach (var tagParsed in Tags.Distinct().ToList())
      {
        var tag = await context.Tags.Where(s => s.Name == tagParsed).FirstOrDefaultAsync();
        if (tag == null)
        {
          tag = new DbTag() { Name = tagParsed };
          context.Tags.Add(tag);
        }

        tag.Videos ??= [];

        if (tag.Videos.Any(s => s.Id == video.Id))
        {
          //logger.LogInformation("Tag {t} already exists for video {v}", tag.Name, video.Id);
        }
        else
        {
          tag.Videos.Add(video);
          logger.LogInformation("Tag {s} added for video {v}", tag.Name, video.Id);
        }
      }

      if (string.IsNullOrWhiteSpace(VideoDetails.Name) == false && VideoDetails.Name != video.Title)
      {
        var old = video.Title;
        video.Title = VideoDetails.Name;

        logger.LogInformation("Title set to {v}. Previous title was {o}", video.Title, old);
      }

      video.ParsedDetails = true;
      await context.SaveChangesAsync();
    }

    public async Task ParseMissingInformations()
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var unscrapedItems = await context.VideoItems.Where(a => a.ParsedDetails == false).ToListAsync();
      for (var i = 0; i < unscrapedItems.Count; i++)
      {
        var videoItem = unscrapedItems[i];
        logger.LogInformation(message: $"Parse missing details: {i + 1} / {unscrapedItems.Count}");
        try
        {
          await ParseDetails(unscrapedItems[i], context);
        }
        catch (Exception ex)
        {
          logger.LogWarning($"Error scraping VideoItem {videoItem.Id}");
          logger.LogError(ex.ToString());
        }
      }
    }

    public async Task ReparseInformations(CancellationToken cancellationToken = default)
    {
      logger.LogInformation("ReparseInformations started");

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      logger.LogInformation("Loading video items from database");
      var items = await context.VideoItems
          .Include(a => a.Stars)
          .Include(a => a.Tags)
          .ToListAsync(cancellationToken);

      // Sortiere nach dem Laden in Memory
      items = items
          .OrderByDescending(a => Convert.ToInt32(a.SiteVideoId))
          .ToList();

      logger.LogInformation("Loaded {Count} items for reparse", items.Count);

      for (var i = 0; i < items.Count && !cancellationToken.IsCancellationRequested; i++)
      {
        var videoItem = items[i];
        this._scrapingStatus = $"Reparse details: {i + 1} / {items.Count}";
        logger.LogInformation(this._scrapingStatus);

        try
        {
          await ParseDetails(items[i], context);
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error reparsing VideoItem {Id}: {Title}", videoItem.Id, videoItem.Title);
        }

        await Task.Delay(100, cancellationToken);
      }

      cancellationToken.ThrowIfCancellationRequested();
      logger.LogInformation("ReparseInformations completed");
    }

    public async Task RemoveDeadByPicture(CancellationToken cancellationToken = default)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      // Nur Videos ohne Fehler und ohne Abspielzählung prüfen
      var items = await context.VideoItems
          .OrderByDescending(a => a.Id)
          .Where(x => x.PlayCount < 1)
          .ToListAsync(cancellationToken);

      var totalCount = items.Count;
      var processedCount = 0;
      var successCount = 0;
      var deadCount = 0;
      var errorCount = 0;

      // Optimierte HttpClient-Konfiguration
      using var httpClient = new HttpClient(new HttpClientHandler
      {
          AllowAutoRedirect = true,
          MaxAutomaticRedirections = 2,
          AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
          UseCookies = true
      })
      {
          Timeout = TimeSpan.FromSeconds(3)
      };

      // Browser-ähnliche HTTP-Header
      httpClient.DefaultRequestHeaders.Clear();
      httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
      httpClient.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,*/*;q=0.8");
      httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
      httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");

      // Verarbeite jeweils 5 Videos parallel, aber nicht mehr
      var semaphore = new SemaphoreSlim(5);

      foreach (var item in items)
      {
        try
        {
          await semaphore.WaitAsync(cancellationToken);

          // Starte die Prüfung als Task
          _ = Task.Run(async () =>
          {
            try
            {
              var status = await CheckThumbnailAsync(httpClient, item, cancellationToken);

              if (status == ThumbnailStatus.Success)
                Interlocked.Increment(ref successCount);
              else if (status == ThumbnailStatus.DeadLink)
                Interlocked.Increment(ref deadCount);
              else
                Interlocked.Increment(ref errorCount);

              var count = Interlocked.Increment(ref processedCount);

              // Aktualisiere den Status alle 5 Elemente
              if (count % 5 == 0 || count == totalCount)
              {
                this._scrapingStatus = $"Checking thumbnails: {count}/{totalCount} ({(count * 100.0 / totalCount):F1}%) - Ok: {successCount}, Dead: {deadCount}, Errors: {errorCount}";
              }

              // Speichere die Änderungen alle 20 Elemente
              if (count % 20 == 0 || count == totalCount)
              {
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation($"Progress: {count}/{totalCount} items checked - Ok: {successCount}, Dead: {deadCount}, Errors: {errorCount}");
              }
            }
            catch (Exception ex)
            {
              logger.LogError(ex, $"Error checking thumbnail for video {item.Id}");
              Interlocked.Increment(ref errorCount);
            }
            finally
            {
              semaphore.Release();
            }
          }, cancellationToken);

          // Kurze Verzögerung zwischen Task-Starts, um Last zu verteilen
          await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
          semaphore.Release();
          logger.LogError(ex, "Error starting thumbnail check task");
        }
      }

      // Warte, bis alle Anfragen abgeschlossen sind (maximal 5 Minuten)
      for (int i = 0; i < 300 && processedCount < totalCount; i++)
      {
        if (cancellationToken.IsCancellationRequested) break;

        this._scrapingStatus = $"Checking thumbnails: {processedCount}/{totalCount} ({(processedCount * 100.0 / totalCount):F1}%) - Ok: {successCount}, Dead: {deadCount}, Errors: {errorCount}";
        await Task.Delay(1000, cancellationToken);
      }

      // Abschließende Speicherung
      await context.SaveChangesAsync(cancellationToken);
      logger.LogInformation($"Finished checking thumbnails. Summary: Total: {totalCount}, Ok: {successCount}, Dead: {deadCount}, Errors: {errorCount}");

      this._scrapingStatus = $"✓ Finished checking {totalCount} thumbnails. Ok: {successCount}, Dead: {deadCount}, Errors: {errorCount}";
      await Task.Delay(2000, cancellationToken);
    }

    private enum ThumbnailStatus
    {
      Success,
      DeadLink,
      Error
    }

    private async Task<ThumbnailStatus> CheckThumbnailAsync(HttpClient httpClient, DbVideoItem item, CancellationToken cancellationToken)
    {
      try
      {
        // Verwende HEAD-Anfrage statt GET, um Bandbreite zu sparen
        var request = new HttpRequestMessage(HttpMethod.Head, item.Thumbnail);
        var res = await httpClient.SendAsync(request, cancellationToken);

        if (!res.IsSuccessStatusCode)
        {
          if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
          {
            item.ErrorCount = (item.ErrorCount ?? 0) + 1;
            logger.LogInformation($"Dead thumbnail found for video {item.Id}: {item.Title}");
            return ThumbnailStatus.DeadLink;
          }
          else
          {
            logger.LogWarning($"HTTP {(int)res.StatusCode} for video {item.Id}: {res.StatusCode}");
            return ThumbnailStatus.Error;
          }
        }
        else
        {
          return ThumbnailStatus.Success;
        }
      }
      catch (Exception ex)
      {
        if (ex is TaskCanceledException || ex is HttpRequestException)
        {
          logger.LogWarning($"Network error for video {item.Id}: {ex.Message}");
        }
        else
        {
          logger.LogError(ex, $"Unexpected error checking video {item.Id}");
        }
        return ThumbnailStatus.Error;
      }
    }

    public async Task DeleteErrorItems(CancellationToken cancellationToken = default)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var errorItems = await context.VideoItems
          .Where(v => v.ErrorCount > 0)
          .ToListAsync(cancellationToken);

      var totalCount = errorItems.Count;
      var currentCount = 0;

      foreach (var item in errorItems)
      {
        if (cancellationToken.IsCancellationRequested)
          break;

        currentCount++;
        this._scrapingStatus = $"Deleting items with errors: {currentCount}/{totalCount}";
        context.VideoItems.Remove(item);

        if (currentCount % 100 == 0)
        {
          await context.SaveChangesAsync(cancellationToken);
        }
      }

      cancellationToken.ThrowIfCancellationRequested();
      await context.SaveChangesAsync(cancellationToken);
      logger.LogInformation($"Deleted {totalCount} items with errors");

      this._scrapingStatus = $"✓ Successfully deleted {totalCount} items with errors";
      await Task.Delay(5000, cancellationToken);
    }

    private List<VideoItem> ParseVideoItems(HtmlNodeCollection? nodes, string baseUrl)
    {
      var items = new List<VideoItem>();

      if (nodes == null)
        return items;

      foreach (var node in nodes)
      {
        var titleNode = node.SelectSingleNode(".//p[@class='mbtit']/a");
        var durationNode = node.SelectSingleNode(".//span[@class='mbtim']");
        var ratingNode = node.SelectSingleNode(".//span[@class='mbrate']");
        var viewsNode = node.SelectSingleNode(".//span[@class='mbvie']");
        var uploaderNode = node.SelectSingleNode(".//span[@class='mb-uploader']/a");
        var linkNode = node.SelectSingleNode(".//div[@class='mbimg']/div[@class='mbcontent']/a");
        var thumbnailNode = node.SelectSingleNode(".//div[@class='mbimg']//img");
        var qualityNode = node.SelectSingleNode(".//div[@class='mvhdico']/span[2]");
        var videoIdNode = node.GetAttributeValue("data-id", string.Empty);
        var vrLogoNode = node.SelectSingleNode(".//span[@class='vrico']");
        var dataVp = node.GetAttributeValue("data-vp", string.Empty);

        var duration = ParseDuration(durationNode?.InnerText.Trim());
        var rating = ParseRating(ratingNode?.InnerText.Trim()) ?? 0;
        var views = ParseViews(viewsNode?.InnerText.Trim()) ?? 0;

        var videoItem = new VideoItem
        {
          Title = titleNode?.InnerText.Trim(),
          Duration = duration,
          Rating = rating,
          Views = views,
          Uploader = uploaderNode?.InnerText.Trim(),
          Link = linkNode != null ? new Uri(new Uri(baseUrl), linkNode.GetAttributeValue("href", string.Empty)).ToString() : string.Empty,
          Thumbnail = thumbnailNode.GetAttributeValue("src", string.Empty),
          Quality = qualityNode == null ? string.Empty : qualityNode.InnerText.Trim(),
          VideoId = !string.IsNullOrEmpty(videoIdNode) ? videoIdNode : string.Empty,
          IsVr = vrLogoNode != null,
          DataVp = !string.IsNullOrEmpty(dataVp) ? dataVp : null
        };

        var thumbSrc = thumbnailNode.GetAttributeValue("src", string.Empty);
        var thumbDataSrc = thumbnailNode.GetAttributeValue("data-src", string.Empty);

        videoItem.Thumbnail = thumbSrc == "data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==" ? thumbDataSrc : thumbSrc;

        items.Add(videoItem);
      }

      return items;
    }

    private static VideoPlayerSettings ParseVideoPlayerSettings(List<string> lines)
    {
      var settings = new VideoPlayerSettings();
      foreach (var line in lines)
      {
        var parts = line.Split(new[] { "=", ";" }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
        if (parts.Length < 2) continue;

        var propertyName = parts[0].Replace("EP.video.player.", "");
        var value = parts[1];

        switch (propertyName)
        {
          case "autoplay":
            settings.Autoplay = bool.Parse(value);
            break;
          case "disable":
            settings.Disable = bool.Parse(value);
            break;
          case "responsive":
            settings.Responsive = bool.Parse(value);
            break;
          case "enableCover":
            settings.EnableCover = bool.Parse(value);
            break;
          case "embed":
            settings.Embed = bool.Parse(value);
            break;
          case "muted":
            settings.Muted = bool.Parse(value);
            break;
          case "ar169":
            settings.Ar169 = bool.Parse(value);
            break;
          case "playbackRates":
            settings.PlaybackRates = value.Trim('[', ']').Split(',').Select(double.Parse).ToList();
            break;
          case "poster":
            settings.Poster = value.Trim('\'');
            break;
          case "vid":
            settings.Vid = value.Trim('\'');
            break;
          case "hash":
            settings.Hash = value.Trim('\'');
            break;
          case "url":
            settings.Url = value.Trim('\'');
            break;
          case "VR":
            settings.VR = bool.Parse(value);
            break;
          case "VRplugin":
            settings.VRplugin = value.Trim('\'');
            break;
          case "VRtype":
            settings.VRtype = value.Trim('\'');
            break;
          case "vjs":
            settings.Vjs = bool.Parse(value);
            break;
          case "initExtraFunc":
            settings.InitExtraFunc = bool.Parse(value);
            break;
        }
      }

      return settings;
    }

    private static TimeSpan? ParseDuration(string? durationText)
    {
      if (string.IsNullOrEmpty(durationText))
      {
        return null;
      }

      var parts = durationText.Split(':');
      if (parts.Length == 2)
      {
        // Format: mm:ss
        if (int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
        {
          return TimeSpan.FromSeconds(minutes * 60 + seconds);
        }
      }
      else if (parts.Length == 3)
      {
        // Format: hh:mm:ss
        if (int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes) && int.TryParse(parts[2], out int seconds))
        {
          return TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
        }
      }

      return null;
    }

    private static double? ParseRating(string? ratingText)
    {
      if (double.TryParse(ratingText?.TrimEnd('%'), out var rating))
      {
        return rating / 100;
      }
      return null;
    }

    private static long? ParseViews(string? viewsText)
    {
      if (viewsText == null)
      {
        return null;
      }

      viewsText = viewsText.Replace(",", "").Replace(" views", "");
      if (long.TryParse(viewsText, out var views))
      {
        return views;
      }
      return null;
    }

    private static string Base36Encode(long value)
    {
      const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
      string result = string.Empty;
      while (value > 0)
      {
        result = chars[(int)(value % 36)] + result;
        value /= 36;
      }
      return string.IsNullOrEmpty(result) ? "0" : result;
    }

    private static string ConvertToBase36(string a)
    {
      if (a.Length != 32)
      {
        throw new ArgumentException("Input string must be 32 characters long.");
      }

      string part1 = Base36Encode(Convert.ToInt64(a.Substring(0, 8), 16));
      string part2 = Base36Encode(Convert.ToInt64(a.Substring(8, 8), 16));
      string part3 = Base36Encode(Convert.ToInt64(a.Substring(16, 8), 16));
      string part4 = Base36Encode(Convert.ToInt64(a.Substring(24, 8), 16));

      return part1 + part2 + part3 + part4;
    }
  }
}
