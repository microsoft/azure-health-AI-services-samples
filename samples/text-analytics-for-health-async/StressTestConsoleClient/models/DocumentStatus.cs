using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StressTestConsoleClient.models
{
    internal class DocumentStatus
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("statusQueryGetUri")]
        public string StatusQueryGetUri { get; set; }
        [JsonProperty("sendEventPostUri")]
        public string SendEventPostUri { get; set; }
        [JsonProperty("terminatePostUri")]
        public string TerminatePostUri { get; set; }
        [JsonProperty("purgeHistoryDeleteUri")]
        public string PurgeHistoryDeleteUri { get; set; }
        [JsonProperty("restartPostUri")]
        public string RestartPostUri { get; set; }
        [JsonProperty("suspendPostUri")]
        public string SuspendPostUri { get; set; }
        [JsonProperty("resumePostUri")]
        public string ResumePostUri { get; set; }
        public bool Finished { get;set; }
        public bool Error { get; set; }
        public string ErrorMessage { get; set; }
    }
}
