using Newtonsoft.Json;

namespace vrScraper.Services.ParsingModels
{
    public class Mp4
    {

        [JsonProperty("2160p(4K)@60fps HD")]
        public Quality? Quality2160p60FPS { get; set; }

        [JsonProperty("2160p(4K) HD")]
        public Quality? Quality2160p { get; set; }

        [JsonProperty("1440p(2K)@60fps HD")]
        public Quality? Quality1440p60FPS { get; set; }

        [JsonProperty("1440p(2K) HD")]
        public Quality? Quality1440p { get; set; }

        [JsonProperty("1080p@60fps HD")]
        public Quality? Quality1080p60FPS { get; set; }

        [JsonProperty("1080p HD")]
        public Quality? Quality1080p { get; set; }

        [JsonProperty("720p@60fps HD")]
        public Quality? Quality720p60FPS { get; set; }

        [JsonProperty("720p HD")]
        public Quality? Quality720p { get; set; }

        [JsonProperty("480p")]
        public Quality? Quality480p { get; set; }

        [JsonProperty("360p")]
        public Quality? Quality360p { get; set; }

        [JsonProperty("240p")]
        public Quality? Quality240p { get; set; }

        [JsonIgnore]
        public Quality? HighestQuality
        {
            get
            {
                List<Quality> qualities = new List<Quality>();

                if (Quality2160p60FPS != null) qualities.Add(Quality2160p60FPS);
                if (Quality2160p != null) qualities.Add(Quality2160p);
                if (Quality1440p60FPS != null) qualities.Add(Quality1440p60FPS);
                if (Quality1440p != null) qualities.Add(Quality1440p);
                if (Quality1080p60FPS != null) qualities.Add(Quality1080p60FPS);
                if (Quality1080p != null) qualities.Add(Quality1080p);
                if (Quality720p60FPS != null) qualities.Add(Quality720p60FPS);
                if (Quality720p != null) qualities.Add(Quality720p);
                if (Quality480p != null) qualities.Add(Quality480p);
                if (Quality360p != null) qualities.Add(Quality360p);
                if (Quality240p != null) qualities.Add(Quality240p);

                return qualities.OrderByDescending(q => q.Resolution).FirstOrDefault();
            }
        }
    }
}
