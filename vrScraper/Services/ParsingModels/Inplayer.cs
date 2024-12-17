#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace vrScraper.Services.ParsingModels
{
  public class Inplayer
  {
    public bool Active { get; set; }

    public string Src { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
  }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
