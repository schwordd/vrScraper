#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using System.ComponentModel.DataAnnotations;

namespace deovrScraper.DB.Models
{
  public class DbSetting
  {
    [Key]
    public string Key { get; set; }

    [Required]
    public string Type { get; set; }

    [Required]
    public string Value { get; set; }
  }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
