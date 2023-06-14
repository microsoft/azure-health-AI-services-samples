using Newtonsoft.Json;

namespace StressTestConsoleClient.models
{
    internal class DocumentPayload
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
