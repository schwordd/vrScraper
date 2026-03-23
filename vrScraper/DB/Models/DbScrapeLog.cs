using System.ComponentModel.DataAnnotations;

namespace vrScraper.DB.Models
{
    public class DbScrapeLog
    {
        [Key]
        public long Id { get; set; }
        [Required]
        public string Site { get; set; } = "";
        public DateTime StartedUtc { get; set; }
        public DateTime? FinishedUtc { get; set; }
        [Required]
        public string TriggerType { get; set; } = "Manual";
        public int PagesScraped { get; set; }
        public int NewVideos { get; set; }
        public int DuplicatesSkipped { get; set; }
        public int Errors { get; set; }
        [Required]
        public string Status { get; set; } = "Running";
    }
}
