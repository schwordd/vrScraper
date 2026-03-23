namespace vrScraper.Controllers.Models.HereSphere
{
    public class HereSphereEventModel
    {
        public string? UserName { get; set; }
        public string? Id { get; set; }
        public string? Title { get; set; }
        public int Event { get; set; }
        public double Time { get; set; }
        public double Speed { get; set; }
        public double Utc { get; set; }
        public string? ConnectionKey { get; set; }
    }
}
