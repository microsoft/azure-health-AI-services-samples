using Newtonsoft.Json;
using System.Collections.Generic;

namespace TextAnalyticsForHealthAsync_Client.Models
{
    internal class ResponseHealthTasks
    {
        [JsonProperty("completed")]
        public int Completed { get; set; }
        [JsonProperty("failed")]
        public int Failed { get; set; }
        [JsonProperty("inProgress")]
        public int InProgress { get; set; }
        [JsonProperty("total")]
        public int Total { get; set; }
        [JsonProperty("items")]
        public List<ReponseHealthItems> Items { get; set; }
    }
}
