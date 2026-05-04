using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace vrScraper.DB.Models
{
  [Index(nameof(SnapshotDate), IsUnique = true)]
  public class DbDailySnapshot
  {
    [Key]
    public long Id { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public int TotalCount { get; set; }
    public int WatchedCount { get; set; }
    public int UnwatchedCount { get; set; }
    public int LikedCount { get; set; }
  }
}
