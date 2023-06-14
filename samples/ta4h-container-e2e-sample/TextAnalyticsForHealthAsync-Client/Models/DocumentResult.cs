using Azure.AI.TextAnalytics;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace TextAnalyticsForHealthAsync_Client.Models
{
    internal class DocumentResult
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("healthcareEntitiesResult")]
        public IEnumerable<HealthcareEntity> HealthcareEntitiesResult { get; set; }
    }
}
