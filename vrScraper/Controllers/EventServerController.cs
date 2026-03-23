using vrScraper.Controllers.Models.HereSphere;
using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace vrScraper.Controllers
{
  [Route("api/events")]
  [ApiController]
  [AllowAnonymous]
  public class EventServerController(ILogger<EventServerController> logger, IServiceProvider serviceProvider, IVideoService videoService) : ControllerBase
  {
    private static readonly ConcurrentDictionary<long, PlaybackSession> _sessions = new();

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] HereSphereEventModel model)
    {
      if (model == null)
        return BadRequest();

      // Parse videoId from model.Id URL (extract number after last /)
      long videoId = 0;
      if (!string.IsNullOrEmpty(model.Id))
      {
        var segments = model.Id.TrimEnd('/').Split('/');
        var lastSegment = segments.LastOrDefault();
        if (lastSegment != null)
          long.TryParse(lastSegment, out videoId);
      }

      if (videoId <= 0)
      {
        logger.LogWarning("EventServer: Could not parse videoId from '{Id}'", model.Id);
        return Ok();
      }

      var utcTimestamp = model.Utc > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds((long)model.Utc).UtcDateTime
        : DateTime.UtcNow;

      // Log event to DB
      try
      {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

        context.PlaybackEvents.Add(new DbPlaybackEvent
        {
          VideoId = videoId,
          EventType = model.Event,
          TimeMs = model.Time,
          Speed = model.Speed,
          UtcTimestamp = utcTimestamp
        });
        await context.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "EventServer: Error saving playback event");
      }

      // Track sessions
      switch (model.Event)
      {
        case 0: // Open
          var session = new PlaybackSession
          {
            VideoId = videoId,
            OpenedUtc = utcTimestamp,
            LastPlayStartUtc = null,
            AccumulatedPlayTime = TimeSpan.Zero
          };
          _sessions[videoId] = session;

          // Update LastPlayedUtc
          var video = await videoService.GetVideoById(videoId);
          if (video != null)
          {
            videoService.SetPlayedVideo(video);
          }
          logger.LogInformation("EventServer: Video {VideoId} opened", videoId);
          break;

        case 1: // Play
          if (_sessions.TryGetValue(videoId, out var playSession))
          {
            playSession.LastPlayStartUtc = utcTimestamp;
          }
          logger.LogDebug("EventServer: Video {VideoId} play at {Time}ms", videoId, model.Time);
          break;

        case 2: // Pause
          if (_sessions.TryGetValue(videoId, out var pauseSession) && pauseSession.LastPlayStartUtc.HasValue)
          {
            var playDuration = utcTimestamp - pauseSession.LastPlayStartUtc.Value;
            if (playDuration > TimeSpan.Zero && playDuration < TimeSpan.FromHours(4))
            {
              pauseSession.AccumulatedPlayTime += playDuration;
            }
            pauseSession.LastPlayStartUtc = null;
          }
          logger.LogDebug("EventServer: Video {VideoId} paused at {Time}ms", videoId, model.Time);
          break;

        case 3: // Close
          if (_sessions.TryRemove(videoId, out var closeSession))
          {
            // If still playing when closed, accumulate remaining
            if (closeSession.LastPlayStartUtc.HasValue)
            {
              var remaining = utcTimestamp - closeSession.LastPlayStartUtc.Value;
              if (remaining > TimeSpan.Zero && remaining < TimeSpan.FromHours(4))
              {
                closeSession.AccumulatedPlayTime += remaining;
              }
            }

            // Update PlayDurationEst in DB
            if (closeSession.AccumulatedPlayTime > TimeSpan.FromSeconds(5))
            {
              try
              {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
                var dbVideo = await context.VideoItems.FindAsync(videoId);
                if (dbVideo != null)
                {
                  dbVideo.PlayDurationEst += closeSession.AccumulatedPlayTime;
                  dbVideo.LastPlayedUtc = utcTimestamp;
                  await context.SaveChangesAsync();
                }
              }
              catch (Exception ex)
              {
                logger.LogError(ex, "EventServer: Error updating PlayDurationEst for {VideoId}", videoId);
              }
            }

            logger.LogInformation("EventServer: Video {VideoId} closed, total play time: {PlayTime}",
              videoId, closeSession.AccumulatedPlayTime);
          }
          break;
      }

      return Ok();
    }

    private class PlaybackSession
    {
      public long VideoId { get; set; }
      public DateTime OpenedUtc { get; set; }
      public DateTime? LastPlayStartUtc { get; set; }
      public TimeSpan AccumulatedPlayTime { get; set; }
    }
  }
}
