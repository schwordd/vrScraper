namespace vrScraper.DB.Models
{
  public class DbVideoStar
  {
    public long VideoId { get; set; }
    public virtual DbVideoItem Video { get; set; } = null!;

    public long StarId { get; set; }
    public virtual DbStar Star { get; set; } = null!;

    public bool IsAutoDetected { get; set; }
  }
}
