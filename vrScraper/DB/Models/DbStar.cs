using vrScraper.Misc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace vrScraper.DB.Models
{

  [Index(nameof(Name), IsUnique = true)]
  public class DbStar
  {
    [Key]
    public long Id { get; set; }

    [Required]
    public string Name { get; set; }

    public virtual List<DbVideoItem> Videos { get; set; }

  }

}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
