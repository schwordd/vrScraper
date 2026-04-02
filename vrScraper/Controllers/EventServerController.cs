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

      // Clean up stale sessions (since CLOSE never comes from HereSphere)
      CleanupStaleSessions();

      // For OPEN events, create session first so we can save the SessionId with the event
      string? sessionId = null;
      if (model.Event == 0)
      {
        var newSessionId = Guid.NewGuid().ToString();
        var session = new PlaybackSession
        {
          VideoId = videoId,
          SessionId = newSessionId,
          OpenedUtc = utcTimestamp,
          LastPlayStartUtc = null,
          AccumulatedPlayTime = TimeSpan.Zero,
          LastTimeMs = 0
        };
        _sessions[videoId] = session;
        sessionId = newSessionId;
      }
      else if (_sessions.TryGetValue(videoId, out var existingSession))
      {
        sessionId = existingSession.SessionId;
      }

      // Log event to DB (SessionId is always set correctly now)
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
          UtcTimestamp = utcTimestamp,
          SessionId = sessionId
        });
        await context.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "EventServer: Error saving playback event");
      }

      // Track sessions
      var eventNames = new[] { "OPEN", "PLAY", "PAUSE", "CLOSE" };
      var eventName = model.Event >= 0 && model.Event < eventNames.Length ? eventNames[model.Event] : $"UNKNOWN({model.Event})";
      logger.LogDebug("[EVENT] {EventName} videoId={VideoId} timeMs={TimeMs} speed={Speed}",
        eventName, videoId, model.Time, model.Speed);

      switch (model.Event)
      {
        case 0: // Open
          // Update VideoEngagement.OpenCount (NO SetPlayedVideo — that comes from HereSphere.Detail)
          await UpdateEngagement(videoId, engagement =>
          {
            engagement.OpenCount += 1;
            engagement.LastSessionUtc = utcTimestamp;
          });

          logger.LogDebug("[EVENT] Session created for video {VideoId} (session {SessionId})", videoId, sessionId);
          break;

        case 1: // Play (= Scrub position update)
          if (_sessions.TryGetValue(videoId, out var playSession))
          {
            playSession.LastPlayStartUtc = utcTimestamp;

            var currentTimeMs = model.Time;
            var previousTimeMs = playSession.LastTimeMs;

            // Pre-compute coverage outside the sync delegate
            var video = await videoService.GetVideoById(videoId);
            double? coverageAtPosition = null;
            if (video != null && video.Duration.TotalMilliseconds > 0)
              coverageAtPosition = Math.Min(1.0, currentTimeMs / video.Duration.TotalMilliseconds);

            await UpdateEngagement(videoId, engagement =>
            {
              engagement.ScrubEventCount += 1;

              if (previousTimeMs - currentTimeMs > 5000)
                engagement.BackwardScrubCount += 1;

              if (coverageAtPosition.HasValue && coverageAtPosition.Value > engagement.ScrubCoveragePercent)
                engagement.ScrubCoveragePercent = coverageAtPosition.Value;
            });

            playSession.LastTimeMs = currentTimeMs;
          }
          break;

        case 2: // Pause (= user pressed stop or headset standby)
          if (_sessions.TryGetValue(videoId, out var pauseSession) && pauseSession.LastPlayStartUtc.HasValue)
          {
            var playDuration = utcTimestamp - pauseSession.LastPlayStartUtc.Value;
            if (playDuration > TimeSpan.Zero && playDuration < TimeSpan.FromHours(4))
            {
              pauseSession.AccumulatedPlayTime += playDuration;
            }
            pauseSession.LastPlayStartUtc = null;
          }

          // PAUSE = session end (HereSphere sends this on stop/standby)
          videoService.FinishCurrentPlayback();
          _sessions.TryRemove(videoId, out _);
          logger.LogDebug("[EVENT] PAUSE video {VideoId} — session finished", videoId);
          break;

        case 3: // Close (never sent by HereSphere, kept for protocol completeness)
          if (_sessions.TryRemove(videoId, out var closeSession))
          {
            if (closeSession.LastPlayStartUtc.HasValue)
            {
              var remaining = utcTimestamp - closeSession.LastPlayStartUtc.Value;
              if (remaining > TimeSpan.Zero && remaining < TimeSpan.FromHours(4))
              {
                closeSession.AccumulatedPlayTime += remaining;
              }
            }

            videoService.FinishCurrentPlayback();
            logger.LogDebug("[EVENT] CLOSE video {VideoId}", videoId);
          }
          break;
      }

      return Ok();
    }

    private async Task UpdateEngagement(long videoId, Action<DbVideoEngagement> update)
    {
      try
      {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

        var engagement = await context.VideoEngagements.FindAsync(videoId);
        if (engagement == null)
        {
          engagement = new DbVideoEngagement { VideoId = videoId };
          context.VideoEngagements.Add(engagement);
        }

        update(engagement);
        await context.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "EventServer: Error updating VideoEngagement for {VideoId}", videoId);
      }
    }

    private void CleanupStaleSessions()
    {
      var staleThreshold = DateTime.UtcNow.AddHours(-8);
      var staleKeys = _sessions.Where(kv => kv.Value.OpenedUtc < staleThreshold).Select(kv => kv.Key).ToList();
      foreach (var key in staleKeys)
        _sessions.TryRemove(key, out _);
    }

    private class PlaybackSession
    {
      public long VideoId { get; set; }
      public string SessionId { get; set; } = "";
      public DateTime OpenedUtc { get; set; }
      public DateTime? LastPlayStartUtc { get; set; }
      public TimeSpan AccumulatedPlayTime { get; set; }
      public double LastTimeMs { get; set; }
    }
  }
}
