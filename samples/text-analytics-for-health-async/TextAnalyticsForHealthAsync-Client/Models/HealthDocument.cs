using Newtonsoft.Json;

namespace TextAnalyticsForHealthAsync_Client.Models
{
    internal class HealthDocument
    {
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("language")]
        public string Language { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}
