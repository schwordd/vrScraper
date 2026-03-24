namespace vrScraper.Services.ParsingModels
{
  public class AdditionalVideoDetails
  {
    public string? Name { get; set; }
    public string? Bitrate { get; set; }
    public ushort? Width { get; set; }
    public ushort? Height { get; set; }
    public string? Description { get; set; }
    public DateTime? UploadDate { get; set; }
    public long? Views { get; set; }
    public double? Rating { get; set; }
    public List<string> Actors { get; set; } = [];
  }
}
