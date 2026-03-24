namespace vrScraper.DB.Models
{
  public class DbVideoTag
  {
    public long VideoId { get; set; }
    public virtual DbVideoItem Video { get; set; } = null!;

    public long TagId { get; set; }
    public virtual DbTag Tag { get; set; } = null!;

    public bool IsAutoDetected { get; set; }
  }
}
