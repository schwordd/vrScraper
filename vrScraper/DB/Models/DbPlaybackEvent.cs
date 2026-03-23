using System.ComponentModel.DataAnnotations;

namespace vrScraper.DB.Models
{
    public class DbPlaybackEvent
    {
        [Key]
        public long Id { get; set; }
        public long VideoId { get; set; }
        public int EventType { get; set; }
        public double TimeMs { get; set; }
        public double Speed { get; set; }
        public DateTime UtcTimestamp { get; set; }
    }
}
