using Newtonsoft.Json;
using System.Collections.Generic;

namespace TextAnalyticsForHealthAsync_Client.Models
{
    internal class Results
    {
        [JsonProperty("documents")]
        public List<ReponseHealthDocument> Documents { get; set; }
    }
}
