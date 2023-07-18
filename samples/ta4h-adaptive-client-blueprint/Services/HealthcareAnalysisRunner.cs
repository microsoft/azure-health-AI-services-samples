using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;


public class HealthcareAnalysisRunner
{
    private readonly TextAnalytics4HealthClient _textAnalyticsClient;
    private readonly ConcurrentQueue<QueueItem> _jobsQueue = new();
    private readonly TimeSpan waitTimeWhenQueueIsEmpty = TimeSpan.FromMilliseconds(500);
    private readonly List<Tuple<QueueItem, TimeSpan>> _completedItems = new();
    private readonly ILogger _logger;
    private readonly IDataHandler _dataHandler;
    private readonly DataProcessingOptions _dataProcessingOptions;
    private readonly object _lock = new object();
    private bool _datasetCompleted = false;

    public HealthcareAnalysisRunner(ILogger<HealthcareAnalysisRunner> logger,
                                       IDataHandler dataHandler,
                                       TextAnalytics4HealthClient textAnalyticsClient,
                                       IOptions<DataProcessingOptions> dataProcessingOptions)
    {
        _textAnalyticsClient = textAnalyticsClient ?? throw new ArgumentNullException(nameof(textAnalyticsClient));

        _logger = logger;
        _dataHandler = dataHandler;
        _dataProcessingOptions = dataProcessingOptions.Value;
        MaxAllowedPendingJobsCount = _dataProcessingOptions.InitialQueueSize;
    }


    public int MaxAllowedPendingJobsCount { get; private set; }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var documentSendingTask = StartSendingAllDocumentsToTA4H();
        var queueProcessingTask = StartJobsQueueProcessingAsync();
        await documentSendingTask;
        await queueProcessingTask;
    }


    private async Task StartSendingAllDocumentsToTA4H()
    {
        while (true)
        {
            var paylods = await _dataHandler.LoadNextBatchOfPayloadsAsync();
            if (!paylods.Any())
            {
                _logger.LogInformation("No more payloads to send for processing, waiting for the jobs queue to complete");
                _datasetCompleted = true;
                break;
            }
            int size = paylods.Count;
            var tasks = new List<Task>();
            for (int i = 0; i < size; i++)
            {
                var payload = paylods[i];
                tasks.Add(SendPaylodForProcessing(payload));
                if (tasks.Count == _dataProcessingOptions.Concurrency || i == size - 1)
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
            }
        }
    }

    private async Task StartJobsQueueProcessingAsync()
    {
        while (true)
        {
            var tasks = new List<Task>();
            if (_jobsQueue.TryDequeue(out var item))
            {
                tasks.Add(ProcessQueueItemAsync(item));
                if (tasks.Count == _dataProcessingOptions.Concurrency || _jobsQueue.Count == 0 )
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
            }
            else
            {
                if (_datasetCompleted)
                {
                    break;
                }
                _logger.LogDebug("No jobs is queue, will try again in {ms} ms.", waitTimeWhenQueueIsEmpty.TotalMilliseconds);
                await Task.Delay(waitTimeWhenQueueIsEmpty);
            }
        }
        _logger.LogInformation("Completed processing of all queue items");
    }

    private async Task WaitIfJobsQueueTooBigAsync()
    {
        while (_jobsQueue.Count >= MaxAllowedPendingJobsCount)
        {
            _logger.LogDebug("jobsQueueCount={jobsQueueCount} >= MaxSize={MaxSize}. waiting for next cycle", _jobsQueue.Count, MaxAllowedPendingJobsCount);
            await Task.Delay(500);
        }
        return;
    }

    private async Task SendPaylodForProcessing(Ta4hInputPayload payload)
    {
        await WaitIfJobsQueueTooBigAsync();
        var jobId = payload.DocumentsMetadata.Select(d => d.JobId).Distinct().Single(); // we assume that all documents in payload have the same jobId or don't have a job Id at all
        jobId ??= await _textAnalyticsClient.SendPayloadToProcessingAsync(payload);
        _jobsQueue.Enqueue(new QueueItem(payload, payload.TotalCharLength, DateTime.UtcNow, NextCheckDateTime: DateTime.UtcNow + GetEstimatedProcessingTime(payload.TotalCharLength), LastCheckedDateTime: DateTime.UtcNow));
        _logger.LogDebug("Job {jobId} started : Sent {docCount} docs for processing: {docs}", jobId, payload.Documents.Count, string.Join('|', payload.Documents.Select(d => d.Id).ToArray()));
        await _dataHandler.UpdateProcessingJobAsync(payload, jobId);
    }


    private async Task ProcessQueueItemAsync(QueueItem item)
    {
        var jobId = item.Payload.DocumentsMetadata.First().JobId;
        if (DateTime.UtcNow < item.NextCheckDateTime)
        {
            _jobsQueue.Enqueue(item with { LastCheckedDateTime = DateTime.UtcNow });
        }
        else
        {
            TextAnlyticsJobResponse response = await _textAnalyticsClient.GetHealthcareAnalysisOperationStatusAndResultsAsync(jobId);
            if (!JobStatus.IsTerminalStatus(response.Status))
            {
                if (response.Status == JobStatus.Running)
                {
                    var newItem = item with { LastCheckedDateTime = DateTime.UtcNow, NextCheckDateTime = DateTime.UtcNow + TimeSpan.FromSeconds(15) };
                    _jobsQueue.Enqueue(newItem);
                }
                else if (response.Status == JobStatus.NotStarted)
                {
                    var newItem = item with { LastCheckedDateTime = DateTime.UtcNow, NextCheckDateTime = DateTime.UtcNow + GetEstimatedProcessingTime(item.InputSize) + TimeSpan.FromSeconds(15) };
                    _jobsQueue.Enqueue(newItem);
                }
            }
            else
            {
                if (response.Status == JobStatus.Failed || response.Tasks.Items[0].Status == JobStatus.Failed)
                {
                    _logger.LogError("Job with Id {jobId} Failed", jobId);
                    if (item.Payload.Documents.Count > 1)
                    {
                        await RetryDocumentsFromFailedJobAsync(item);
                    }
                    else
                    {
                        _logger.LogError("Document with id {docId} Failed", item.Payload.DocumentsMetadata.First().DocumentId);
                        await _dataHandler.StoreFailedJobResultsAsync(item.Payload);
                    }
                }
                else
                {
                    _logger.LogInformation("JobId {jobId} completed successfully", jobId);
                    await ProcessSuccessfulJobAsync(item, response);
                }
            }
        }
    }

    private async  Task RetryDocumentsFromFailedJobAsync(QueueItem item)
    {
        var ndocs = item.Payload.Documents.Count;
        _logger.LogInformation("Requeueing documents from failed job");
        // if the failure of the job is due to one "bad" document that causes some unexpected error, retry the documents in the job separately so that we get the most successful documents.
        var firstHalf = new Ta4hInputPayload
        {
            Documents = item.Payload.Documents.Take(ndocs / 2).ToList(),
            DocumentsMetadata = item.Payload.DocumentsMetadata.Take(ndocs / 2).ToList()
        };
        var secondHalf = new Ta4hInputPayload
        {
            Documents = item.Payload.Documents.Skip(ndocs / 2).ToList(),
            DocumentsMetadata = item.Payload.DocumentsMetadata.Skip(ndocs / 2).ToList()
        };
        await SendPaylodForProcessing(firstHalf);
        await SendPaylodForProcessing(secondHalf);
    }

    private async Task ProcessSuccessfulJobAsync(QueueItem item, TextAnlyticsJobResponse response)
    {
        TimeSpan jobDuration = response.LastUpdatedDateTime - response.CreatedDateTime;
        lock (_lock)
        {
            _completedItems.Add(new(item, jobDuration));
            if (_completedItems.Count == MaxAllowedPendingJobsCount)
            {
                AdjustMaxPendingJobsLimit();
                _completedItems.Clear();
            }
        }
        await _dataHandler.StoreSuccessfulJobResultsAsync(item.Payload, response.Tasks.Items[0].Results);
    }

    private void AdjustMaxPendingJobsLimit()
    {
        var estiamtedMeanWaitTime = EstimateMeanWaitTimeForBatch();
        var currentMaxSize = MaxAllowedPendingJobsCount;
        int newMaxSize;
        if (estiamtedMeanWaitTime < TimeSpan.FromSeconds(60))
        {
            // as long as the estimated time a job waits before processing is less than 1 minute, we can increase the
            // max size of pending jobs.
            newMaxSize = MaxAllowedPendingJobsCount * 2;
        }
        else
        {
            // if the wait time is longer than 1 minute, we may decrease or increase the max size in a factor
            // set according to the esitimates wait time.
            newMaxSize = (int)(MaxAllowedPendingJobsCount * (TimeSpan.FromSeconds(90) / estiamtedMeanWaitTime));
        }
        var absoluteMax = _dataProcessingOptions.AbsoluteMaxPendingJobCount;
        // on top of adapting MaxAllowedPendingJobsSize based on the time it takes, we want to limit the growth to try to reduce the risk of getting throttled by the API.
        MaxAllowedPendingJobsCount = (newMaxSize > absoluteMax) ? absoluteMax : (newMaxSize == 0) ? 1 : newMaxSize;
        _logger.LogInformation("estimatedMeanWaitTime: {estiamtedMeanWaitTime}, currentMaxSize: {currentMaxSize}, nextMaxSize: {MaxSize}", estiamtedMeanWaitTime, currentMaxSize, MaxAllowedPendingJobsCount);
    }

    private TimeSpan EstimateMeanWaitTimeForBatch()
    {
        var totalTime = TimeSpan.Zero;
        int count = _completedItems.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            var (item, jobDuration) = _completedItems[i];
            var diff = jobDuration - GetEstimatedProcessingTime(item.InputSize);
            var estimatedWaitTime = diff < TimeSpan.Zero ? TimeSpan.Zero : diff;
            totalTime += estimatedWaitTime;
        }
        var meanWaitTime = totalTime / count;
        return meanWaitTime;
    }

    private TimeSpan GetEstimatedProcessingTime(int inputSize)
    {
        // The backend throughput is appx. 1000 chars per second.
        return TimeSpan.FromMilliseconds(inputSize) + TimeSpan.FromSeconds(2);
    }


}