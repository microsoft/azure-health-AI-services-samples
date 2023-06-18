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
    private readonly List<Tuple<QueueItem, TimeSpan>> _completedItems = new List<Tuple<QueueItem, TimeSpan>>();
    private readonly ILogger _logger;
    private readonly IFileStorage _outputStorage;
    private readonly DataProcessingOptions _dataProcessingOptions;
    private Dataset dataset;
    private readonly object _lock = new object();

    public HealthcareAnalysisProcessor(ILogger<HealthcareAnalysisProcessor> logger,
                                       TextAnalytics4HealthClient textAnalyticsClient,
                                       FileStorageManager fileStorageManager,
                                       IOptions<DataProcessingOptions> dataProcessingOptions)
    {
        _textAnalyticsClient = textAnalyticsClient ?? throw new ArgumentNullException(nameof(textAnalyticsClient));

        _outputStorage = fileStorageManager.OutputStorage;
        _logger = logger;
        _dataProcessingOptions = dataProcessingOptions.Value;
        MaxSize = _dataProcessingOptions.InitialQueueSize;
    }


    public int Count => _jobsQueue.Count;

    public int MaxSize { get; private set; }


    public async Task StartAsync(Dataset dataset)
    {
        _logger.LogInformation("StartAsync called");
        this.dataset = dataset;

        var queueProcessingTask = StartQueueProcessingAsync();

        var sendDocumentsTasks = new List<Task>();
        while (true)
        {
            if (_jobsQueue.Count >= MaxSize)
            {
                _logger.LogDebug("jobsQueueCount={jobsQueueCount} >= MaxSize={MaxSize}. waiting for next cycle", _jobsQueue.Count, MaxSize);
                await Task.Delay(500);
                continue;
            }
            var payload = dataset.GetNextPayload();

            if (!payload.Documents.Any())
            {
                break;
            }
            var task = SendPaylodToProcessing(payload);
            sendDocumentsTasks.Add(task);
            if (sendDocumentsTasks.Count > 10)
            {
                await Task.WhenAll(sendDocumentsTasks);
                sendDocumentsTasks.Clear();
                sendDocumentsTasks.Add(Task.Delay(2500)); // make sure we wait for at least 2.5 seconds before sending the next batch of requests, so as to not hit the request limit
            }
        }
        await queueProcessingTask;
    }

    private async Task SendPaylodToProcessing(Ta4hInputPayload payload)
    {
        var jobId = await _textAnalyticsClient.StartHealthcareAnalysisOperationAsync(payload);
        _jobsQueue.Enqueue(new QueueItem(jobId, payload.TotalCharLength, DateTime.UtcNow, NextCheckDateTime: DateTime.UtcNow + GetEstimatedProcessingTime(payload.TotalCharLength), LastCheckedDateTime: DateTime.UtcNow));
        _logger.LogDebug($"{DateTime.Now} :: Job {jobId} started : Sent {payload.Documents.Count} docs for processing: {string.Join('|', payload.Documents.Select(d => d.Id).ToArray())}");
    }


    public async Task StartQueueProcessingAsync()
    {
        var queueProcessingTasks = new List<Task>();
        while (true)
        {
            if (_jobsQueue.TryDequeue(out var item))
            {
                var task = ProcessQueueItemAsync(item);
                queueProcessingTasks.Add(task);
                if (queueProcessingTasks.Count > 10 || dataset.IsComplete)
                {
                    await Task.WhenAll(queueProcessingTasks.ToArray());
                    queueProcessingTasks.Clear();
                }
            }
            else
            {
                if (dataset.IsComplete)
                {
                    break;
                }
                else
                {
                    await Task.Delay(waitTimeWhenQueueIsEmpty);
                }
            }
        }
        _logger.LogInformation("Completed processing all queue items");
    }

    private async Task ProcessQueueItemAsync(QueueItem item)
    {
        if (DateTime.UtcNow < item.NextCheckDateTime)
        {
            if (item.LastCheckedDateTime < DateTime.UtcNow - TimeSpan.FromSeconds(5)) 
            {
                _logger.LogDebug("JobId {item.JobId}: too early to check. will check status after {NextCheckDateTime}",  item.JobId, item.NextCheckDateTime);
            }
            _jobsQueue.Enqueue(item with { LastCheckedDateTime = DateTime.UtcNow});
        }
        else
        {
            var jobId = item.JobId;
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
                    _logger.LogError($"ERROR: taks failed {item.JobId}");
                }
                else
                {
                    _logger.LogInformation("JobId {item.JobId} completed successfully", item.JobId);
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
        await StoreSuccessfulJobResultsAsync(response.Tasks.Items[0].Results);
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

    private async Task StoreSuccessfulJobResultsAsync(HealthcareResults results)
    {
        try
        {
            var resultsStored = await BatchProcessor.ProcessInBatchesAsync(
                results.Documents, results.Documents.Count > 10 ? 10 : results.Documents.Count, async (doc) =>
                {
                    var resultsFileName = doc.Id + ".json";
                    await _outputStorage.SaveJsonFileAsync(doc, resultsFileName);
                    return resultsFileName;
                });
        }
        catch (AggregateException ae)
        {
            foreach (var ex in ae.InnerExceptions)
            {
                _logger.LogError(ex.StackTrace);
            }
        }
    }

    private TimeSpan GetEstimatedProcessingTime(int inputSize)
    {
        return TimeSpan.FromMilliseconds(inputSize) + TimeSpan.FromSeconds(2);
    }

}