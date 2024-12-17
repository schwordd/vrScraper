using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace deovrScraper.DB.Models
{
  public class VideoSource
  {
    public string LabelShort { get; set; }
    public string Src { get; set; }
    public string Type { get; set; }
    public bool Default { get; set; }
    public int Resolution { get; set; }
  }

}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.