#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace deovrScraper.Common
{
  public class AppSettings
  {
    public string? LogPath { get; set; } = null;
    public int Port { get; set; } = 5000;
    public string Ip { get; set; } = "192.168.0.100";
    public string DataDir { get; set; }
    public string ConnectionString { get; set; }
  }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
