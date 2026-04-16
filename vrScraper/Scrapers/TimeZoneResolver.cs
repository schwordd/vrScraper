namespace vrScraper.Scrapers
{
  // Resolves an IANA or Windows timezone ID to a TimeZoneInfo, trying cross-platform
  // conversion as a fallback. Returns null when the ID cannot be mapped at all.
  public static class TimeZoneResolver
  {
    public static TimeZoneInfo? TryResolve(string id)
    {
      if (string.IsNullOrWhiteSpace(id)) return null;

      if (TryFind(id, out var direct)) return direct;

      if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var winId) && TryFind(winId, out var win))
        return win;

      if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId) && TryFind(ianaId, out var iana))
        return iana;

      return null;
    }

    private static bool TryFind(string id, out TimeZoneInfo tz)
    {
      try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
      catch { tz = TimeZoneInfo.Utc; return false; }
    }
  }
}
