namespace StressTestConsoleClient.models
{
    internal class DurableAzureFunctionStatus
    {
        public string Name { get; set; }
        public string InstanceId { get; set; }
        public RuntimeStatus RuntimeStatus { get; set; }
        public string CustomStatus { get; set; }
        public string Output { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastUpdatedTime { get; set; }
        public List<FunctionInput> FunctionInput { get; set; }

    }

    internal enum RuntimeStatus
    {
        Pending, 
        Running,
        Completed,
        ContinuedAsNew,
        Failed,
        Terminated,
        Suspended
    }

    public class FunctionInput
    {
        public object Language { get; set; }
        public string Id { get; set; }
        public string Text { get; set; }
    }

}
