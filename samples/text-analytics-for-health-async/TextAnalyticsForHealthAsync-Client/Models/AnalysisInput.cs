using Newtonsoft.Json;
using System.Collections.Generic;

namespace TextAnalyticsForHealthAsync_Client.Models
{
    internal class AnalysisInput
    {
        [JsonProperty("documents")]
        public List<HealthDocument> Documents { get; set; }
    }
}
