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
    /// The maximun number of documents to be processed by the application in one run.
    /// </summary>
    public int MaxDocs { get; set; } = int.MaxValue;

    /// <summary>
    /// The maximun number of documents to be loaded into memory from processing.
    /// </summary>
    public int MaxBatchSize { get; set; } = 1000;

    /// <summary>
    /// For dev purposes only - the application will cycle to the documents this number of times.
    /// </summary>
    public int RepeatTimes { get; set; } = 1;

    /// <summary>
    /// Max number of cuncurrent threads when calling the ta4h API
    /// </summary>
    public int Concurrency { get; set; } = 10;

    /// <summary>
    /// The max number of pending ta4h jobs we allow concurrently
    /// </summary>
    public int AbsoluteMaxPendingJobCount { get; set; } = 450;


}


