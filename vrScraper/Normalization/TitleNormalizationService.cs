using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using vrScraper.DB;
using vrScraper.DB.Models;
using vrScraper.Normalization.Interfaces;
using vrScraper.Services;
using vrScraper.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.Normalization
{
  public class TitleNormalizationService(
    ILogger<TitleNormalizationService> logger,
    IVideoService videoService,
    IServiceProvider serviceProvider) : ITitleNormalizationService
  {
    private readonly HashSet<string> _dynamicDictionary = new(StringComparer.OrdinalIgnoreCase);
    private bool _dynamicDictionaryLoaded;

    private void EnsureDynamicDictionary()
    {
      if (_dynamicDictionaryLoaded) return;
      _dynamicDictionaryLoaded = true;

      try
      {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

        // Add all star name parts
        foreach (var star in context.Stars.Select(s => s.Name).ToList())
        {
          foreach (var part in star.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            _dynamicDictionary.Add(part.ToLowerInvariant());
        }

        // Add all tag names
        foreach (var tag in context.Tags.Select(t => t.Name).ToList())
        {
          _dynamicDictionary.Add(tag.ToLowerInvariant());
          foreach (var part in tag.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            _dynamicDictionary.Add(part.ToLowerInvariant());
        }

        // Extract vocabulary from non-obfuscated titles
        // Only add words that are ALREADY in the main dictionary (prevents pollution from
        // obfuscated titles that IsObfuscated() missed, like i/l swaps: "Mldnlght")
        var cleanTitles = context.VideoItems
          .Select(v => v.Title)
          .ToList()
          .Where(t => !IsObfuscated(t))
          .ToList();

        int cleanWordsAdded = 0;
        foreach (var title in cleanTitles)
        {
          foreach (var word in title.Split([' ', '-', ',', '.', '(', ')', ':', '\'', '"', '!', '?'], StringSplitOptions.RemoveEmptyEntries))
          {
            var clean = word.Trim().ToLowerInvariant();
            if (clean.Length >= 3 && clean.All(char.IsLetter))
            {
              // Only add if it's a known English word (prevents "mldnlght" etc.)
              if (CharacterMappings.WordDictionary.Contains(clean))
              {
                _dynamicDictionary.Add(clean);
                cleanWordsAdded++;
              }
            }
          }
        }

        logger.LogInformation("Dynamic dictionary loaded: {Count} words (from stars, tags, and {Titles} clean titles)",
          _dynamicDictionary.Count, cleanTitles.Count);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to load dynamic dictionary");
      }
    }

    private bool IsInDictionary(string word)
    {
      var lower = word.ToLowerInvariant();
      if (CharacterMappings.WordDictionary.Contains(lower) || _dynamicDictionary.Contains(lower))
        return true;
      // Check common inflections: strip trailing s/es/ed/ing/er
      if (lower.EndsWith("s") && CharacterMappings.WordDictionary.Contains(lower[..^1]))
        return true;
      if (lower.EndsWith("es") && CharacterMappings.WordDictionary.Contains(lower[..^2]))
        return true;
      if (lower.EndsWith("ed") && CharacterMappings.WordDictionary.Contains(lower[..^2]))
        return true;
      if (lower.EndsWith("ing") && lower.Length > 5 && CharacterMappings.WordDictionary.Contains(lower[..^3]))
        return true;
      if (lower.EndsWith("ers") && CharacterMappings.WordDictionary.Contains(lower[..^1])) // honeymooners → honeymooner
        return true;
      return false;
    }

    // ── Public API ─────────────────────────────────────────────────────

    public bool IsObfuscated(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return false;

      // Filenames are not obfuscated — they just happen to contain metadata
      // Check original, confusable-decoded, and Cyrillic-lookalike variants
      // Cyrillic р(U+0440) looks like p, so .mр4 = .mp4
      var titleForExtCheck = title
        .Replace('\u0440', 'p')  // Cyrillic р → p
        .Replace('\u0435', 'e')  // Cyrillic е → e
        .Replace('\u043E', 'o')  // Cyrillic о → o
        .Replace('\u0430', 'a'); // Cyrillic а → a
      if (CharacterMappings.FileExtensions.Any(ext => titleForExtCheck.Contains(ext, StringComparison.OrdinalIgnoreCase)))
        return false;

      // Titles with significant CJK/Japanese/Korean content are not leet-obfuscated
      // (they may contain catalog codes like "Test339B天月あず" which should not be decoded)
      int cjkChars = title.Count(c => c >= 0x3000 && c <= 0x9FFF || c >= 0xAC00 && c <= 0xD7AF || c >= 0x3040 && c <= 0x30FF);
      if (cjkChars >= 3)
        return false;

      int suspiciousChars = 0;
      int leetTransitions = 0;

      for (int i = 0; i < title.Length; i++)
      {
        char c = title[i];

        // Count non-basic-latin characters (above U+00FF)
        if (c > 0x00FF)
          suspiciousChars++;

        // Count surrogate pairs (mathematical symbols etc.)
        if (char.IsHighSurrogate(c))
          suspiciousChars++;

        // Count leet-speak transitions (digit adjacent to letter)
        if (i > 0 && CharacterMappings.LeetMap.ContainsKey(c) && char.IsLetter(title[i - 1]))
          leetTransitions++;
        if (i > 0 && char.IsLetter(c) && CharacterMappings.LeetMap.ContainsKey(title[i - 1]))
          leetTransitions++;
      }

      return suspiciousChars > 0 || leetTransitions > 1;
    }

    private int CountDictionaryWords(string text)
    {
      return text.Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
        .Count(w => w.Length >= 3 && w.All(char.IsLetter) && IsInDictionary(w));
    }

    public string? NormalizeTitle(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return null;

      EnsureDynamicDictionary();

      // Always run safe pass (Confusables + Dictionary i/l fix — can't damage anything)
      var safe = NormalizeSafe(title);

      // Always run full pass (Leet + Reversed + Alt-Leet)
      var full = NormalizeFull(title);

      // Pick the best result:
      // 1. If full decode improved dict word count → use full (even if not "obfuscated")
      // 2. If full decode didn't help but safe did → use safe
      // 3. If nothing helped → null
      int origWords = CountDictionaryWords(title);

      bool obfuscated = IsObfuscated(title);

      if (full != title)
      {
        int fullWords = CountDictionaryWords(full);
        logger.LogDebug("NormalizeTitle: orig={Orig} origWords={OW} full={Full} fullWords={FW} obfuscated={Obf}",
          title, origWords, full, fullWords, obfuscated);
        // If known obfuscated: accept if dict words maintained or improved (>= trust the decoder)
        // If not known obfuscated: must strictly improve (> prevents damage to clean titles)
        if (obfuscated ? fullWords >= origWords : fullWords > origWords)
          return full;
      }

      if (safe != title)
      {
        int safeWords = CountDictionaryWords(safe);
        logger.LogDebug("NormalizeTitle: safe={Safe} safeWords={SW} origWords={OW}",
          safe, safeWords, origWords);
        // Safe pass: accept if it maintains or improves (>= is fine, safe can't damage)
        if (safeWords >= origWords)
          return safe;
      }

      return null;
    }

    /// <summary>
    /// Full normalization pipeline: Unicode + Leet + Reversed + Dictionary + Alt-Leet.
    /// Use for titles known to be obfuscated.
    /// </summary>
    private string NormalizeFull(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return title;

      // Decode HTML entities
      title = title.Replace("&#039;", "'").Replace("&amp;", "&")
                   .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");

      EnsureDynamicDictionary();

      var result = NormalizeUnicode(title);
      result = HandleReversedAscii(result);
      var preDict = result; // snapshot before leet decode for change-tracking
      result = DecodeLeetSpeak(result);
      result = PostProcessWithDictionary(result, preDict);
      result = TryAltLeetFallback(result, title);
      result = CollapseSpaces(result);
      result = ToTitleCase(result);

      return result.Trim();
    }

    public string NormalizeTitleLegacy(string title) => NormalizeFull(title);

    /// <summary>
    /// Safe normalization pass: only Unicode confusables, accents, dictionary i/l correction.
    /// Cannot damage clean titles — no leet decode, no reversed text detection.
    /// </summary>
    private string NormalizeSafe(string title)
    {
      if (string.IsNullOrWhiteSpace(title)) return title;

      title = title.Replace("&#039;", "'").Replace("&amp;", "&")
                   .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");

      var result = NormalizeUnicode(title);
      result = CollapseSpaces(result);
      result = ToTitleCase(result);

      return result.Trim();
    }

    public List<(DbStar Star, double Confidence)> DetectStars(string normalizedTitle, List<DbStar> knownStars)
    {
      var results = new List<(DbStar Star, double Confidence)>();
      if (string.IsNullOrWhiteSpace(normalizedTitle)) return results;

      var titleLower = normalizedTitle.ToLowerInvariant();
      var titleWords = titleLower.Split([' ', '-', ',', '.', '(', ')', ':', '\'', '"'], StringSplitOptions.RemoveEmptyEntries);

      foreach (var star in knownStars)
      {
        if (star.Name.Length < 6) continue;

        var starWords = star.Name.ToLowerInvariant()
          .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (starWords.Length >= 2)
        {
          // Two-word name: each word must be at least 3 chars, exact match only
          if (starWords.Any(w => w.Length < 3)) continue;

          bool allMatch = starWords.All(sw => titleWords.Any(tw => MatchesStarWord(tw, sw)));
          if (allMatch)
            results.Add((star, 1.0));
        }
        else if (starWords.Length == 1 && starWords[0].Length >= 6)
        {
          // Single-word name: exact match only, min 6 chars
          if (titleWords.Any(tw => MatchesStarWord(tw, starWords[0])))
            results.Add((star, 1.0));
        }
      }

      return results;
    }

    public List<DbTag> DetectTags(string normalizedTitle, List<DbTag> knownTags)
    {
      if (string.IsNullOrWhiteSpace(normalizedTitle)) return [];

      var titleLower = normalizedTitle.ToLowerInvariant();
      return knownTags
        .Where(t => t.Name.Length >= 4 && titleLower.Contains(t.Name.ToLowerInvariant()))
        .ToList();
    }

    /// <summary>
    /// Matches a title word against a star name word, allowing trailing 's' (possessive/plural).
    /// e.g. "ryders" matches "ryder", "willow's" would be split at apostrophe so "willow" matches exactly.
    /// </summary>
    private static bool MatchesStarWord(string titleWord, string starWord)
      => titleWord == starWord || (titleWord.Length == starWord.Length + 1 && titleWord[^1] == 's' && titleWord.AsSpan(0, starWord.Length).SequenceEqual(starWord));

    public async Task<int> NormalizeAllTitles(bool forceReprocess = false, bool normalizeTitles = true, bool detectStars = true, bool detectTags = true, NormalizationProgress? progress = null, Action<string, string?, string>? onTitleProcessed = null, CancellationToken ct = default)
    {
      var allVideos = await videoService.GetVideoItems();

      // Info-Log: wie viele obfuskiert?
      var obfuscatedCount = allVideos.Count(v => IsObfuscated(v.Title));

      // Bestimme welche Titel verarbeitet werden:
      // - normalizeTitles: Titel die obfuskiert sind ODER noch kein NormalizedTitle haben
      //   Bei forceReprocess: nur obfuskierte oder fehlende NormalizedTitle (clean bereits verarbeitete überspringen)
      // - detectStars/detectTags: Videos ohne Stars/Tags
      var toNormalize = new List<DbVideoItem>();
      if (normalizeTitles)
      {
        toNormalize = allVideos
          .Where(v => forceReprocess
            ? (IsObfuscated(v.Title) || string.IsNullOrEmpty(v.NormalizedTitle))
            : string.IsNullOrEmpty(v.NormalizedTitle))
          .ToList();
      }

      var toDetectOnly = new List<DbVideoItem>();
      if (detectStars || detectTags)
      {
        var normalizeIds = new HashSet<long>(toNormalize.Select(v => v.Id));
        toDetectOnly = allVideos
          .Where(v => !normalizeIds.Contains(v.Id)
            && ((detectStars && (v.Stars == null || v.Stars.Count == 0))
             || (detectTags && (v.Tags == null || v.Tags.Count == 0))))
          .ToList();
      }

      var toProcess = toNormalize.Concat(toDetectOnly).ToList();

      if (toProcess.Count == 0)
      {
        logger.LogInformation("No titles to process");
        return 0;
      }

      logger.LogInformation("Processing {Count} titles ({Normalize} to normalize + {Detect} detect-only, {Obfuscated} obfuscated total)",
        toProcess.Count, toNormalize.Count, toDetectOnly.Count, obfuscatedCount);

      if (progress != null)
      {
        progress.Total = toProcess.Count;
        progress.Phase = "Initializing...";
      }

      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();
      var allStars = detectStars ? await context.Stars.Include(s => s.Videos).ToListAsync(ct) : [];
      var allTags = detectTags ? await context.Tags.Include(t => t.Videos).ToListAsync(ct) : [];

      int processed = 0;
      int starsDetected = 0;
      int tagsDetected = 0;
      int titlesNormalized = 0;
      int unsicher = 0;

      // Phase 1: Normalisierung — alle Titel durch NormalizeTitle() (Zwei-Pass)
      if (normalizeTitles && toNormalize.Count > 0)
      {
        for (int i = 0; i < toNormalize.Count; i++)
        {
          ct.ThrowIfCancellationRequested();
          var video = toNormalize[i];

          var normalized = NormalizeTitle(video.Title);
          if (normalized != null && IsPlausible(normalized))
          {
            { var (s, t) = await SaveNormalization(context, video, normalized, allStars, allTags, detectStars, detectTags, ct); starsDetected += s; tagsDetected += t; }
            onTitleProcessed?.Invoke(video.Title, normalized, "decoder");
            titlesNormalized++;
          }
          else if (normalized != null)
          {
            { var (s, t) = await SaveNormalization(context, video, normalized, allStars, allTags, detectStars, detectTags, ct); starsDetected += s; tagsDetected += t; }
            onTitleProcessed?.Invoke(video.Title, normalized, "decoder-unsicher");
            titlesNormalized++;
            unsicher++;
          }
          else
          {
            // NormalizeTitle returned null — no change, still run star/tag detection on original
            { var (s, t) = await SaveNormalization(context, video, video.Title, allStars, allTags, detectStars, detectTags, ct); starsDetected += s; tagsDetected += t; }
            onTitleProcessed?.Invoke(video.Title, null, "skip");
          }

          processed++;
          if (progress != null)
          {
            progress.Current = processed;
            progress.StarsDetected = starsDetected;
            progress.TagsDetected = tagsDetected;
            progress.TitlesNormalized = titlesNormalized;
            progress.Phase = $"Decoding ({unsicher} uncertain)";
          }

          if (processed % 100 == 0)
            await context.SaveChangesAsync(ct);

          if (processed % 10 == 0)
            await Task.Yield(); // Let UI thread update progress
        }

        await context.SaveChangesAsync(ct);

        logger.LogInformation("Normalization handled {Total} titles ({Normalized} changed, {Unsicher} unsicher)",
          toNormalize.Count, titlesNormalized, unsicher);
      }

      // Phase 2: Star/Tag Erkennung für restliche Videos (ohne Normalisierung)
      if (toDetectOnly.Count > 0)
      {
        if (progress != null)
          progress.Phase = $"Star/Tag detection ({toDetectOnly.Count} titles)";

        foreach (var video in toDetectOnly)
        {
          ct.ThrowIfCancellationRequested();

          var detectedStarList = detectStars
            ? DetectStars(video.Title, allStars).Where(d => d.Confidence >= 0.7).ToList()
            : new List<(DbStar Star, double Confidence)>();
          var detectedTagList = detectTags ? DetectTags(video.Title, allTags) : new List<DbTag>();

          var dbVideo = await context.VideoItems.FindAsync([video.Id], ct);
          if (dbVideo != null)
          {
            foreach (var (star, _) in detectedStarList)
            {
              if (!await context.VideoStars.AnyAsync(vs => vs.VideoId == dbVideo.Id && vs.StarId == star.Id, ct))
              {
                context.VideoStars.Add(new DbVideoStar { VideoId = dbVideo.Id, StarId = star.Id, IsAutoDetected = true });
                starsDetected++;
              }
            }

            foreach (var tag in detectedTagList)
            {
              if (!await context.VideoTags.AnyAsync(vt => vt.VideoId == dbVideo.Id && vt.TagId == tag.Id, ct))
              {
                context.VideoTags.Add(new DbVideoTag { VideoId = dbVideo.Id, TagId = tag.Id, IsAutoDetected = true });
                tagsDetected++;
              }
            }
          }

          processed++;
          if (progress != null)
          {
            progress.Current = processed;
            progress.StarsDetected = starsDetected;
            progress.TagsDetected = tagsDetected;
          }

          if (processed % 100 == 0)
            await context.SaveChangesAsync(ct);

          if (processed % 10 == 0)
            await Task.Yield();
        }

        await context.SaveChangesAsync(ct);
      }

      if (progress != null)
      {
        progress.Phase = "Done";
        progress.Current = processed;
      }

      logger.LogInformation(
        "Normalized {Titles} titles, detected {Stars} stars and {Tags} tags across {Total} videos",
        titlesNormalized, starsDetected, tagsDetected, processed);

      await videoService.ReloadVideos();

      return processed;
    }

    public async Task EnrichSingleVideo(long videoId, CancellationToken ct = default)
    {
      using var scope = serviceProvider.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<VrScraperContext>();

      var video = await context.VideoItems.FindAsync([videoId], ct);
      if (video == null) return;

      var titleToUse = video.Title;
      var normalized = NormalizeTitle(titleToUse);
      if (normalized != null && IsPlausible(normalized))
        titleToUse = normalized;
      else if (normalized != null)
        titleToUse = normalized;

      video.NormalizedTitle = titleToUse;

      var allStars = await context.Stars.ToListAsync(ct);
      var allTags = await context.Tags.ToListAsync(ct);

      var detectedStars = DetectStars(titleToUse, allStars).Where(d => d.Confidence >= 0.7).ToList();
      var detectedTags = DetectTags(titleToUse, allTags);

      foreach (var (star, _) in detectedStars)
      {
        if (!await context.VideoStars.AnyAsync(vs => vs.VideoId == video.Id && vs.StarId == star.Id, ct))
          context.VideoStars.Add(new DbVideoStar { VideoId = video.Id, StarId = star.Id, IsAutoDetected = true });
      }

      foreach (var tag in detectedTags)
      {
        if (!await context.VideoTags.AnyAsync(vt => vt.VideoId == video.Id && vt.TagId == tag.Id, ct))
          context.VideoTags.Add(new DbVideoTag { VideoId = video.Id, TagId = tag.Id, IsAutoDetected = true });
      }

      await context.SaveChangesAsync(ct);
    }

    private async Task<(int Stars, int Tags)> SaveNormalization(VrScraperContext context, DbVideoItem video,
      string normalizedTitle, List<DbStar> allStars, List<DbTag> allTags, bool detectStars, bool detectTags,
      CancellationToken ct)
    {
      int stars = 0, tags = 0;
      var detectedStarList = detectStars
        ? DetectStars(normalizedTitle, allStars).Where(d => d.Confidence >= 0.7).ToList()
        : new List<(DbStar Star, double Confidence)>();
      var detectedTagList = detectTags ? DetectTags(normalizedTitle, allTags) : new List<DbTag>();

      var dbVideo = await context.VideoItems.FindAsync([video.Id], ct);
      if (dbVideo == null) return (0, 0);

      dbVideo.NormalizedTitle = normalizedTitle;

      foreach (var (star, _) in detectedStarList)
      {
        if (!await context.VideoStars.AnyAsync(vs => vs.VideoId == dbVideo.Id && vs.StarId == star.Id, ct))
        {
          context.VideoStars.Add(new DbVideoStar { VideoId = dbVideo.Id, StarId = star.Id, IsAutoDetected = true });
          stars++;
        }
      }

      foreach (var tag in detectedTagList)
      {
        if (!await context.VideoTags.AnyAsync(vt => vt.VideoId == dbVideo.Id && vt.TagId == tag.Id, ct))
        {
          context.VideoTags.Add(new DbVideoTag { VideoId = dbVideo.Id, TagId = tag.Id, IsAutoDetected = true });
          tags++;
        }
      }

      return (stars, tags);
    }

    /// <summary>
    /// Checks if a decoded title looks plausible (contains real words).
    /// </summary>
    private bool IsPlausible(string decoded)
    {
      EnsureDynamicDictionary();
      var words = decoded.Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
        .Where(w => w.Length >= 3 && w.All(char.IsLetter))
        .ToList();
      if (words.Count == 0) return true; // no testable words, trust decoder
      int dictHits = words.Count(w => IsInDictionary(w));
      return (double)dictHits / words.Count > 0.5;
    }

    // ── Internal Methods ───────────────────────────────────────────────

    private static string NormalizeUnicode(string input)
    {
      // Pre-process: detect upside-down text — returns fully normalized ASCII if detected
      var upsideDown = HandleUpsideDown(input);
      if (upsideDown != null)
        return upsideDown;

      var sb = new StringBuilder(input.Length);

      for (int i = 0; i < input.Length; i++)
      {
        char c = input[i];

        // Handle surrogate pairs (Mathematical Alphanumeric Symbols etc.)
        if (char.IsHighSurrogate(c) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
        {
          int codepoint = char.ConvertToUtf32(c, input[i + 1]);
          if (CharacterMappings.SurrogatePairConfusables.TryGetValue(codepoint, out var mapped))
          {
            sb.Append(mapped);
          }
          // else: skip unknown surrogate pair (likely emoji/decorator)
          i++; // skip low surrogate
          continue;
        }

        // Handle BMP confusables
        if (CharacterMappings.Confusables.TryGetValue(c, out var replacement))
        {
          sb.Append(replacement);
          continue;
        }

        // Keep ASCII as-is
        if (c <= 0x007F)
        {
          sb.Append(c);
          continue;
        }

        // Latin Extended with diacritics — try to extract base letter
        // U+00C0-024F covers Latin-1 Supplement, Latin Extended-A, Latin Extended-B
        if (c >= 0x00C0 && c <= 0x024F)
        {
          // Direct accent map lookup first (more reliable than FormKD under InvariantGlobalization)
          if (CharacterMappings.AccentMap.TryGetValue(c, out var accentMapped))
          {
            sb.Append(accentMapped);
            continue;
          }

          // Try to decompose to base letter (e.g. Ť → T, ė → e, ŕ → r)
          var decomposed = c.ToString().Normalize(System.Text.NormalizationForm.FormKD);
          if (decomposed.Length > 0 && decomposed[0] <= 0x007F)
            sb.Append(decomposed[0]);
          else
            sb.Append(c);
          continue;
        }

        // Combining characters (U+0300-036F) — skip (diacritics on previous char)
        if (c >= 0x0300 && c <= 0x036F)
          continue;

        // Strip everything else (emoji, decorators, unknown Unicode)
        // But keep spaces (including fullwidth space U+3000)
        if (c == ' ' || c == '\u3000')
          sb.Append(' ');
      }

      // Spaced-out text detection: if >60% of "words" are single characters, collapse spaces
      var normalized = sb.ToString();
      var spacedWords = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (spacedWords.Length > 3)
      {
        // Only count single LETTERS (not digits) — "D S 1 5 2 8 2" is a catalog code, not spaced text
        int singleLetterCount = spacedWords.Count(w => w.Length == 1 && char.IsLetter(w[0]));
        if ((double)singleLetterCount / spacedWords.Length > 0.6)
        {
          // Collapse: join single-char runs, keep multi-char words separated
          var collapsed = new StringBuilder(normalized.Length);
          bool prevWasSingle = false;
          foreach (var sw in spacedWords)
          {
            bool isSingle = sw.Length == 1 && char.IsLetterOrDigit(sw[0]);
            if (collapsed.Length > 0 && !(prevWasSingle && isSingle))
              collapsed.Append(' ');
            collapsed.Append(sw);
            prevWasSingle = isSingle;
          }
          return collapsed.ToString();
        }
      }

      return normalized;
    }

    /// <summary>
    /// Detects upside-down text (ɐuɐʅ → anal) and reverses each word's characters.
    /// Only activates when >40% of non-space chars are upside-down characters.
    /// </summary>
    /// <summary>Returns normalized+reversed string if upside-down text detected, null otherwise.</summary>
    private static string? HandleUpsideDown(string input)
    {
      int upsideDownCount = 0;
      int totalChars = 0;
      foreach (var c in input)
      {
        if (c == ' ') continue;
        totalChars++;
        if (CharacterMappings.UpsideDownChars.Contains(c)) upsideDownCount++;
      }

      if (totalChars == 0 || (double)upsideDownCount / totalChars < 0.3)
        return null; // Not upside-down text

      // Map each char to ASCII; only flip original ASCII chars (not mapped ones)
      var sb = new StringBuilder(input.Length);
      foreach (var c in input)
      {
        if (c == ' ') { sb.Append(' '); continue; }
        if (CharacterMappings.Confusables.TryGetValue(c, out var mapped))
        {
          // Already correctly mapped by Confusables — don't flip
          sb.Append(mapped);
        }
        else if (c <= 0x007F)
        {
          // Original ASCII char in upside-down context — flip p↔d, b↔q, n↔u
          sb.Append(CharacterMappings.UpsideDownAsciiFlip.TryGetValue(c, out var flipped) ? flipped : c);
        }
        // skip unmapped non-ASCII
      }

      var words = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      var reversed = words
        .Select(w => new string(w.Reverse().ToArray()))
        .Reverse()
        .ToArray();

      return string.Join(' ', reversed);
    }

    /// <summary>
    /// Detects reversed ASCII text by checking if reversing words yields more dictionary hits.
    /// E.g. "yssup" → "pussy", "kcid" → "dick"
    /// Must be called after NormalizeUnicode() and before DecodeLeetSpeak().
    /// </summary>
    private string HandleReversedAscii(string input)
    {
      var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (words.Length == 0) return input;

      // Only consider ASCII-letter words for reversal check
      int forwardHits = 0;
      int reverseHits = 0;
      int testableWords = 0;

      foreach (var word in words)
      {
        if (word.Length < 3 || !word.All(c => char.IsLetter(c) && c <= 0x007F))
          continue;

        testableWords++;
        var lower = word.ToLowerInvariant();
        if (IsInDictionary(lower)) forwardHits++;

        var reversed = new string(lower.Reverse().ToArray());
        if (IsInDictionary(reversed)) reverseHits++;
      }

      // Only reverse if reverse hits are significantly better
      if (testableWords < 2 || reverseHits <= forwardHits)
        return input;

      // Reverse all words and their order
      var reversedWords = words
        .Select(w =>
        {
          if (w.Length < 2 || !w.All(c => char.IsLetter(c) && c <= 0x007F))
            return w;
          var chars = w.ToCharArray();
          // Preserve case pattern: if first char was upper, make first char of reversed upper
          bool firstWasUpper = char.IsUpper(chars[0]);
          Array.Reverse(chars);
          var rev = new string(chars).ToLowerInvariant();
          if (firstWasUpper && rev.Length > 0)
            rev = char.ToUpperInvariant(rev[0]) + rev[1..];
          return rev;
        })
        .Reverse()
        .ToArray();

      return string.Join(' ', reversedWords);
    }

    private static string DecodeLeetSpeak(string input)
    {
      // Protect date patterns, ordinals, and digit-abbreviations from leet-decoding
      var protectedInput = input;
      var placeholders = new List<(string placeholder, string original)>();
      int placeholderIdx = 0;

      foreach (var pattern in new[] { CharacterMappings.DatePattern, CharacterMappings.ProtectedPattern, CharacterMappings.PureNumberToken })
      {
        var matches = pattern.Matches(protectedInput);
        for (int d = matches.Count - 1; d >= 0; d--)
        {
          var match = matches[d];
          var placeholder = $"\x01P{placeholderIdx++}\x01";
          placeholders.Add((placeholder, match.Value));
          protectedInput = protectedInput[..match.Index] + placeholder + protectedInput[(match.Index + match.Length)..];
        }
      }

      // Multi-char replacements first
      var result = protectedInput.Replace("vv", "w", StringComparison.OrdinalIgnoreCase);

      // Single-char leet decode — treat other leet chars as valid neighbors
      // so consecutive leet like "61@k3" decodes fully to "blake"
      var sb = new StringBuilder(result.Length);
      for (int i = 0; i < result.Length; i++)
      {
        char c = result[i];

        // Skip entire placeholder sequences (\x01...\x01)
        if (c == '\x01')
        {
          sb.Append(c);
          i++;
          while (i < result.Length && result[i] != '\x01') { sb.Append(result[i]); i++; }
          if (i < result.Length) sb.Append(result[i]); // closing \x01
          continue;
        }

        if (CharacterMappings.LeetMap.TryGetValue(c, out var leetChar))
        {
          bool prevValid = i > 0 && CharacterMappings.IsLetterOrLeet(result[i - 1]);
          bool nextValid = i + 1 < result.Length && CharacterMappings.IsLetterOrLeet(result[i + 1]);

          // '!' is only leet when BOTH neighbors are actual letters (not other leet/punctuation)
          // "h!llo" → decode, "hello!" → punctuation, "!!!" → punctuation
          if (c == '!' && !(i > 0 && char.IsLetter(result[i - 1]) && i + 1 < result.Length && char.IsLetter(result[i + 1])))
          {
            sb.Append(c);
            continue;
          }

          // '@' and '$' should always be decoded (even standalone " @ " = "a", "word$" = "words")
          // Other leet chars need at least one letter/leet neighbor
          bool alwaysDecode = c == '@' || c == '$';
          if (alwaysDecode || prevValid || nextValid)
          {
            // Special case: '1' is ambiguous (l or i)
            // At word start (after space/punctuation or string start) → 'l' (like "1ittle")
            // Otherwise → 'i' (like "L1sa", "N1cole", "P1nelli")
            if (c == '1')
            {
              bool atWordStart = i == 0 || result[i - 1] == ' ' || result[i - 1] == '-';
              sb.Append(atWordStart ? 'l' : 'i');
            }
            else
            {
              sb.Append(leetChar);
            }
          }
          else
          {
            sb.Append(c);
          }
        }
        else
        {
          sb.Append(c);
        }
      }

      // Restore protected patterns
      var decoded = sb.ToString();
      foreach (var (placeholder, original) in placeholders)
      {
        decoded = decoded.Replace(placeholder, original);
      }

      return decoded;
    }

    private static string CollapseSpaces(string input)
    {
      var sb = new StringBuilder(input.Length);
      bool lastWasSpace = false;
      foreach (var c in input)
      {
        if (c == ' ')
        {
          if (!lastWasSpace) sb.Append(' ');
          lastWasSpace = true;
        }
        else
        {
          sb.Append(c);
          lastWasSpace = false;
        }
      }
      return sb.ToString();
    }

    private static string ToTitleCase(string input)
    {
      if (string.IsNullOrWhiteSpace(input)) return input;

      var words = input.Split(' ');
      for (int i = 0; i < words.Length; i++)
      {
        if (words[i].Length > 0)
        {
          words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
        }
      }
      return string.Join(' ', words);
    }

    /// <summary>
    /// Post-processes decoded text using dictionary lookup to resolve ambiguous characters.
    /// E.g. "Biake" → try I→l → "Blake" (in dictionary) → use "Blake"
    /// </summary>
    private string PostProcessWithDictionary(string input, string? preDecodedInput = null)
    {
      var words = input.Split(' ');
      var preWords = preDecodedInput?.Split(' ');
      var result = new string[words.Length];

      for (int w = 0; w < words.Length; w++)
      {
        var word = words[w];

        // Skip correction for words that were not changed by decoding (prevents false corrections on clean ASCII)
        if (preWords != null && w < preWords.Length && word == preWords[w])
        {
          result[w] = word;
          continue;
        }

        // Handle hyphenated words (e.g. "step-dad")
        if (word.Contains('-'))
        {
          var parts = word.Split('-');
          var corrected = parts.Select(p => CorrectWord(p)).ToArray();
          result[w] = string.Join('-', corrected);
        }
        else
        {
          result[w] = CorrectWord(word);
        }
      }

      return string.Join(' ', result);
    }

    private string TryAltLeetFallback(string processed, string originalTitle)
    {
      var processedParts = CharacterMappings.WordSplitPattern.Split(processed);
      var originalParts = CharacterMappings.WordSplitPattern.Split(originalTitle);

      // Build a quick lookup of original words by approximate position
      var result = new string[processedParts.Length];

      for (int w = 0; w < processedParts.Length; w++)
      {
        var pWord = processedParts[w];
        result[w] = pWord;

        // Skip separators, short words, already-correct words
        if (pWord.Length < 3 || !pWord.Any(char.IsLetter) || !pWord.All(char.IsLetter) || IsInDictionary(pWord))
          continue;

        // Try to find the corresponding original word (by position, approximately)
        string? origWord = w < originalParts.Length ? originalParts[w] : null;
        if (origWord == null) continue;

        // Check if original had leet chars that could have alternate mappings
        var hasAltLeet = origWord.Any(c => CharacterMappings.AltLeetMap.ContainsKey(c));
        if (!hasAltLeet) continue;

        // Try alternate leet mappings
        var bestCandidate = TryAltLeetCombinations(origWord);
        if (bestCandidate != null)
          result[w] = bestCandidate;
      }

      return string.Concat(result);
    }

    /// <summary>
    /// Tries alternate leet decode combinations for a single word and returns
    /// the first dictionary hit, or null if none found.
    /// </summary>
    private string? TryAltLeetCombinations(string word)
    {
      // Find positions with alt-leet chars
      var altPositions = new List<(int pos, char orig, char[] alts)>();
      for (int i = 0; i < word.Length; i++)
      {
        if (CharacterMappings.AltLeetMap.TryGetValue(word[i], out var alts))
          altPositions.Add((i, word[i], alts));
      }

      if (altPositions.Count == 0 || altPositions.Count > 5)
        return null;

      // For each alt-leet position, also include the standard leet decode as an option
      var options = new List<(int pos, char[] choices)>();
      foreach (var (pos, orig, alts) in altPositions)
      {
        var choices = new List<char>();
        if (CharacterMappings.LeetMap.TryGetValue(orig, out var standard))
          choices.Add(standard);
        choices.AddRange(alts);
        options.Add((pos, choices.Distinct().ToArray()));
      }

      // Also decode non-alt leet chars normally
      var baseChars = word.ToCharArray();
      for (int i = 0; i < baseChars.Length; i++)
      {
        if (!altPositions.Any(ap => ap.pos == i) && CharacterMappings.LeetMap.TryGetValue(baseChars[i], out var lc))
        {
          bool prevValid = i > 0 && CharacterMappings.IsLetterOrLeet(baseChars[i - 1]);
          bool nextValid = i + 1 < baseChars.Length && CharacterMappings.IsLetterOrLeet(baseChars[i + 1]);
          if (prevValid || nextValid)
            baseChars[i] = lc;
        }
      }

      // Try all combinations of alt positions (limit explosion)
      int totalCombos = 1;
      foreach (var (_, choices) in options)
        totalCombos *= choices.Length;
      if (totalCombos > 64) return null;

      for (int combo = 0; combo < totalCombos; combo++)
      {
        var attempt = (char[])baseChars.Clone();
        int divisor = 1;
        for (int o = 0; o < options.Count; o++)
        {
          var (pos, choices) = options[o];
          int choiceIdx = (combo / divisor) % choices.Length;
          attempt[pos] = choices[choiceIdx];
          divisor *= choices.Length;
        }

        var candidate = new string(attempt).ToLowerInvariant();

        // Direct dictionary check
        if (IsInDictionary(candidate))
        {
          var result = attempt;
          for (int i = 0; i < result.Length; i++)
          {
            if (char.IsLetter(word[i]) && char.IsUpper(word[i]))
              result[i] = char.ToUpperInvariant(result[i]);
          }
          return new string(result);
        }

        // Also try canonical index (resolves i/l, v/u, b/g on top of alt-leet)
        var canon = CharacterMappings.Canonicalize(candidate);
        if (CharacterMappings.CanonicalIndex.TryGetValue(canon, out var canonCandidates))
        {
          var best = canonCandidates.FirstOrDefault(c => c.Length == candidate.Length) ?? canonCandidates[0];
          var result = best.ToCharArray();
          for (int i = 0; i < Math.Min(result.Length, word.Length); i++)
          {
            if (char.IsUpper(word[i]))
              result[i] = char.ToUpperInvariant(result[i]);
          }
          return new string(result);
        }
      }

      return null;
    }

    private string CorrectWord(string word)
    {
      if (word.Length < 2) return word;
      var lower = word.ToLowerInvariant();
      if (IsInDictionary(lower)) return word;

      var canon = CharacterMappings.Canonicalize(lower);
      if (CharacterMappings.CanonicalIndex.TryGetValue(canon, out var candidates))
      {
        var best = candidates.FirstOrDefault(c => c.Length == lower.Length) ?? candidates[0];
        var result = word.ToCharArray();
        for (int i = 0; i < Math.Min(result.Length, best.Length); i++)
          result[i] = char.IsUpper(word[i]) ? char.ToUpperInvariant(best[i]) : best[i];
        return new string(result);
      }

      // Try with inflection stripping: "rldes" → strip "s" → canon("rlde") → find "ride" → rebuild "rides"
      string[] suffixes = ["s", "es", "ed", "ing", "ers", "er"];
      foreach (var suffix in suffixes)
      {
        if (lower.Length > suffix.Length + 2 && lower.EndsWith(suffix))
        {
          var stem = lower[..^suffix.Length];
          var stemCanon = CharacterMappings.Canonicalize(stem);
          if (CharacterMappings.CanonicalIndex.TryGetValue(stemCanon, out var stemCandidates))
          {
            var best = stemCandidates.FirstOrDefault(c => c.Length == stem.Length) ?? stemCandidates[0];
            var corrected = best + suffix;
            var result = word.ToCharArray();
            for (int i = 0; i < Math.Min(result.Length, corrected.Length); i++)
              result[i] = char.IsUpper(word[i]) ? char.ToUpperInvariant(corrected[i]) : corrected[i];
            return new string(result);
          }
        }
      }

      // Also check dynamic dictionary with canonical lookup
      var dynamicCanon = CharacterMappings.Canonicalize(lower);
      foreach (var dw in _dynamicDictionary)
      {
        if (CharacterMappings.Canonicalize(dw) == dynamicCanon)
        {
          var result = word.ToCharArray();
          for (int i = 0; i < Math.Min(result.Length, dw.Length); i++)
          {
            result[i] = char.IsUpper(word[i]) ? char.ToUpperInvariant(dw[i]) : dw[i];
          }
          return new string(result);
        }
      }

      return word;
    }

  }
}
