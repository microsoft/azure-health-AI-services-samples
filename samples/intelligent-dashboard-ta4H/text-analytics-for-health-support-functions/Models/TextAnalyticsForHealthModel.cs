using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace text_analytics_for_health_support_functions.Models
{
    internal class AnalysisInput
    {
        [JsonProperty("documents")]
        public List<HealthDocument> Documents { get; set; }
    }

    internal class HealthDocument
    {
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("language")]
        public string Language { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    internal class Parameters
    {
        [JsonProperty("fhirVersion")]
        public string FhirVersion { get; set; }
        [JsonProperty("modelVersion")]
        public string ModelVersion { get; set; }
    }

    internal class TextAnalyticsForHealthModel
    {
        [JsonProperty("analysisInput")]
        public AnalysisInput AnalysisInput { get; set; }
        [JsonProperty("tasks")]
        public List<HealthTask> Tasks { get; set; }
    }

    internal class HealthTask
    {
        [JsonProperty("taskId")]
        public string TaskId { get; set; }
        [JsonProperty("kind")]
        public string Kind { get; set; }
        [JsonProperty("parameters")]
        public Parameters Parameters { get; set; }
    }


    internal class TextAnalyticsForHealthResponse
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
        [JsonProperty("tasks")]
        public ResponseHealthTasks ReponseHealthTasks { get; set; }
    }

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


    internal class Results
    {
        [JsonProperty("documents")]
        public List<ReponseHealthDocument> Documents { get; set; }
    }

    internal class ReponseHealthDocument
    {
        [JsonProperty("fhirBundle")]
        public object FhirBundle { get; set; }
    }
}
