using Newtonsoft.Json;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace deovrScraper.Services.ParsingModels
{
    public class Quality
    {
        public string LabelShort { get; set; }
        public string Src { get; set; }
        public string Type { get; set; }
        public bool Default { get; set; }

        [JsonIgnore]
        public int Resolution
        {
            get
            {
                return LabelShort switch
                {
                    "2160p" => 2160,
                    "1440p" => 1440,
                    "1080p" => 1080,
                    "720p" => 720,
                    "480p" => 480,
                    "360p" => 360,
                    "240p" => 240,
                    _ => 0,
                };
            }
        }
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.