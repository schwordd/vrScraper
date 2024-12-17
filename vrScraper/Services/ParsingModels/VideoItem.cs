using Newtonsoft.Json;
using vrScraper.Misc;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace vrScraper.Services.ParsingModels
{
    public class VideoItem
    {
        public string? Title { get; set; }

        [JsonConverter(typeof(TimeSpanSecondsConverter))]
        public TimeSpan? Duration { get; set; }

        public double Rating { get; set; }
        public long Views { get; set; }
        public string? Uploader { get; set; }
        public string Link { get; set; }
        public string Thumbnail { get; set; }
        public string Quality { get; set; }
        public string VideoId { get; set; }
        public bool IsVr { get; set; }
        public string? DataVp { get; set; }
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.