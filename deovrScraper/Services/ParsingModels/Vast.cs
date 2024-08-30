#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace deovrScraper.Services.ParsingModels
{
    public class Vast
    {
        public bool Active { get; set; }
        public object Tag { get; set; }
        public int Timeout { get; set; }
        public int MaxSkipOffset { get; set; }
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.