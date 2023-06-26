using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

namespace TextAnalyticsHealthcareAdaptiveClient;

public class HealthcareAnalysisProcessor
{
    private readonly TextAnalytics4HealthClient _textAnalyticsClient;
    private readonly ConcurrentQueue<QueueItem> _jobsQueue = new();
    private readonly TimeSpan waitTimeWhenQueueIsEmpty = TimeSpan.FromMilliseconds(500);
    private readonly List<Tuple<QueueItem, TimeSpan>> _completedItems = new();
    private readonly ILogger _logger;
    private readonly IDataHandler _dataHandler;
    private readonly IFileStorage _outputStorage;
    private readonly DataProcessingOptions _dataProcessingOptions;
    private readonly object _lock = new object();
    private bool _datasetCompleted = false;

    public HealthcareAnalysisProcessor(ILogger<HealthcareAnalysisProcessor> logger,
                                       IDataHandler dataHandler,
                                       TextAnalytics4HealthClient textAnalyticsClient,
                                       FileStorageManager fileStorageManager,
                                       IOptions<DataProcessingOptions> dataProcessingOptions)
    {
        _textAnalyticsClient = textAnalyticsClient ?? throw new ArgumentNullException(nameof(textAnalyticsClient));

        _outputStorage = fileStorageManager.OutputStorage;
        _logger = logger;
        _dataHandler = dataHandler;
        _dataProcessingOptions = dataProcessingOptions.Value;
        MaxSize = _dataProcessingOptions.InitialQueueSize;
    }


    public int Count => _jobsQueue.Count;

    public int MaxSize { get; private set; }


    public async Task StartAsync()
    {
        
        _logger.LogInformation("StartAsync called");

        var queueProcessingTask = StartJobsQueueProcessingAsync();

        while (true)
        {
            var nextBatch = await _dataHandler.LoadNextBatchOfPayloadsAsync();
            if (!nextBatch.Any()) 
            {
                _logger.LogInformation("No more payloads to send for processing, waiting for the jobs queue to complete");
                _datasetCompleted = true;
                break;
            }
            foreach (var payload in nextBatch)
            {
                _logger.LogInformation("payload");
                await WaitIfJobsQueueToBigAsync();
                await SendPaylodToProcessing(payload); 
            }            
        }
        await queueProcessingTask;
    }

    private async Task WaitIfJobsQueueToBigAsync()
    {
        while (_jobsQueue.Count >= MaxSize)
        {
            _logger.LogDebug("jobsQueueCount={jobsQueueCount} >= MaxSize={MaxSize}. waiting for next cycle", _jobsQueue.Count, MaxSize);
            await Task.Delay(500);
        }
        return;
    }

    private async Task SendPaylodToProcessing(Ta4hInputPayload payload)
    {
        var jobId = await _textAnalyticsClient.StartHealthcareAnalysisOperationAsync(payload);
        payload.DocumentsMetadata.ForEach(m => { m.JobId = jobId; });
        _jobsQueue.Enqueue(new QueueItem(payload, payload.TotalCharLength, DateTime.UtcNow, NextCheckDateTime: DateTime.UtcNow + GetEstimatedProcessingTime(payload.TotalCharLength), LastCheckedDateTime: DateTime.UtcNow));
        _logger.LogDebug($"{DateTime.Now} :: Job {jobId} started : Sent {payload.Documents.Count} docs for processing: {string.Join('|', payload.Documents.Select(d => d.Id).ToArray())}");

    }


    public async Task StartJobsQueueProcessingAsync()
    {
        while (true)
        {
            if (_jobsQueue.TryDequeue(out var item))
            {
                
                await ProcessQueueItemAsync(item);
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
        _logger.LogInformation("Completed processing all queue items");
    }

    private async Task ProcessQueueItemAsync(QueueItem item)
    {
        var jobId = item.Payload.DocumentsMetadata.First().JobId;
        if (DateTime.UtcNow < item.NextCheckDateTime)
        {
            if (item.LastCheckedDateTime < DateTime.UtcNow - TimeSpan.FromSeconds(5)) 
            {
                _logger.LogDebug("JobId {jobId}: too early to check. will check status after {NextCheckDateTime}", jobId, item.NextCheckDateTime);
            }
            _jobsQueue.Enqueue(item with { LastCheckedDateTime = DateTime.UtcNow});
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
                    _logger.LogError("ERROR: task failed {jobId}", jobId);
                    var ndocs = item.Payload.Documents.Count;
                    if (item.Payload.Documents.Count > 1)
                    {

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
                        _jobsQueue.Enqueue(new QueueItem(firstHalf, firstHalf.TotalCharLength, DateTime.UtcNow, NextCheckDateTime: DateTime.UtcNow + GetEstimatedProcessingTime(firstHalf.TotalCharLength), LastCheckedDateTime: DateTime.UtcNow));
                        _jobsQueue.Enqueue(new QueueItem(secondHalf, secondHalf.TotalCharLength, DateTime.UtcNow, NextCheckDateTime: DateTime.UtcNow + GetEstimatedProcessingTime(secondHalf.TotalCharLength), LastCheckedDateTime: DateTime.UtcNow));
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

    private async Task ProcessSuccessfulJobAsync(QueueItem item, TextAnlyticsJobResponse response)
    {
        TimeSpan jobDuration = response.LastUpdatedDateTime - response.CreatedDateTime;
        lock (_lock)
        {
            _completedItems.Add(new(item, jobDuration));
            if (_completedItems.Count == MaxSize)
            {
                var estiamtedMeanWaitTime = EstimateMeanWaitTimeForBatch();
                var currentMaxSize = MaxSize;
                int newMaxSize;
                if (estiamtedMeanWaitTime < TimeSpan.FromSeconds(60))
                {
                    newMaxSize = MaxSize * 2;
                }
                else
                {
                    newMaxSize = (int)(MaxSize * (TimeSpan.FromSeconds(90) / estiamtedMeanWaitTime));
                }
                if (newMaxSize > 300)
                {
                    newMaxSize = 300;
                }
                if (newMaxSize == 0)
                {
                    newMaxSize = 1;
                }
                MaxSize = newMaxSize;
                _logger.LogInformation("estiamtedMeanWaitTime: {estiamtedMeanWaitTime}, currentMaxSize: {currentMaxSize}, nextMaxSize: {MaxSize}", estiamtedMeanWaitTime, currentMaxSize, MaxSize);
                _completedItems.Clear();
            }
        }
        await _dataHandler.StoreSuccessfulJobResultsAsync(item.Payload, response.Tasks.Items[0].Results);
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
        return TimeSpan.FromMilliseconds(inputSize) + TimeSpan.FromSeconds(2);
    }

}