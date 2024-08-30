using deovrScraper.Misc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace deovrScraper.DB.Models
{

  [Index(nameof(Site), nameof(SiteVideoId), IsUnique = true)]
  public class DbVideoItem
  {
    [Key]
    public long Id { get; set; }

    [Required]
    public string Site { get; set; }

    [Required]
    public string SiteVideoId { get; set; }

    [Required]
    public string Title { get; set; }

    public TimeSpan Duration { get; set; }
    public double? Rating { get; set; }
    public long? Views { get; set; }
    public string? Uploader { get; set; }
    public string? Link { get; set; }
    public string? Thumbnail { get; set; }
    public string? Quality { get; set; }
    public bool IsVr { get; set; }
    public string? DataVp { get; set; }

    public virtual List<DbTag> Tags { get; set; }

    public virtual List<DbStar> Stars { get; set; }

    public bool ParsedDetails { get; set; }

  }

}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.