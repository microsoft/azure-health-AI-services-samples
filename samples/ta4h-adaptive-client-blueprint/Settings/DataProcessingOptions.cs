public class DataProcessingOptions
{
    /// <summary>
    /// The initial maximum number of current jobs that can be sent to text analytics.
    /// </summary>
    public int InitialQueueSize { get; set; } = 20;

    /// <summary>
    /// If true, the order of the documents set for processing will be random.
    /// </summary>
    public bool Shuffle { get; set; } = false;

    /// <summary>
    /// The maximun number of documents to be processed by the application.
    /// </summary>
    public int MaxDocs { get; set; }

    /// <summary>
    /// For dev purposes only - the application will cycle to the documents this number of times.
    /// </summary>
    public int RepeatTimes { get; set; } = 1;

}


