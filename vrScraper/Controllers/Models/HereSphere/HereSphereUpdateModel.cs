#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace vrScraper.Controllers.Models.HereSphere
{
  public class HereSphereUpdateModel
  {

    public string Username { get; set; }
    public string Password { get; set; }
    public bool IsFavorite { get; set; }
    public double Rating { get; set; }
    public List<HereSphereTagModel> Tags { get; set; }
  }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

