#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace vrScraper.Services.ParsingModels
{
    public class VideoItemDetails
    {
        public string Vid { get; set; }
        public bool Available { get; set; }
        public bool Fallback { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public Sources Sources { get; set; }
        public List<object> BackupServers { get; set; }
        public List<object> BackupServersHls { get; set; }
        public List<object> VolPrefixes { get; set; }
        public List<object> VolPrefixesHls { get; set; }
        public int LastSpeed { get; set; }
        public string Vtt { get; set; }
        public int ActiveLimits { get; set; }
        public string DashReport { get; set; }
        public string HlsReport { get; set; }
        public int Volume { get; set; }
        public Inplayer Inplayer { get; set; }
        public Vast Vast { get; set; }
        public object Netblock { get; set; }
        public Speedtest Speedtest { get; set; }
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.