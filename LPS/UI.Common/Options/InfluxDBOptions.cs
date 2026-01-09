namespace LPS.UI.Common.Options
{
    public class InfluxDBOptions
    {
        public bool? Enabled { get; set; }
        public string? Url { get; set; }
        public string? Token { get; set; }
        public string? Organization { get; set; }
        public string? Bucket { get; set; }
    }
}
