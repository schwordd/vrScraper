#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace deovrScraper.Services.ParsingModels
{
    public class VideoPlayerSettings
    {
        public bool Autoplay { get; set; }
        public bool Disable { get; set; }
        public bool Responsive { get; set; }
        public bool EnableCover { get; set; }
        public bool Embed { get; set; }
        public bool Muted { get; set; }
        public bool Ar169 { get; set; }
        public List<double> PlaybackRates { get; set; }
        public string Poster { get; set; }
        public string Vid { get; set; }
        public string Hash { get; set; }
        public string Url { get; set; }
        public bool VR { get; set; }
        public string VRplugin { get; set; }
        public string VRtype { get; set; }
        public bool Vjs { get; set; }
        public bool InitExtraFunc { get; set; }
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
