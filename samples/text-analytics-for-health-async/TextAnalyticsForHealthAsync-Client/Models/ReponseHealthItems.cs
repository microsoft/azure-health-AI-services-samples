using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace TextAnalyticsForHealthAsync_Client.Models
{
    internal class ReponseHealthItems
    {
        [JsonProperty("jobId")]
        public string JobId { get; set; }
        [JsonProperty("lastUpdatedDateTime")]
        public DateTime LastUpdatedDateTime { get; set; }
        [JsonProperty("createdDateTime")]
        public DateTime CreatedDateTime { get; set; }
        [JsonProperty("expirationDateTime")]
        public DateTime ExpirationDateTime { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("errors")]
        public List<object> Errors { get; set; }
        [JsonProperty("results")]
        public Results Results { get; set; }
    }
}
