using System.ComponentModel.DataAnnotations;

namespace vrScraper.DB.Models
{
    public class DbVideoEngagement
    {
        [Key]
        public long VideoId { get; set; }
        public int OpenCount { get; set; }
        public int ScrubEventCount { get; set; }
        public int BackwardScrubCount { get; set; }
        public double ScrubCoveragePercent { get; set; }
        public DateTime? LastSessionUtc { get; set; }

        public virtual DbVideoItem Video { get; set; } = null!;
    }
}
