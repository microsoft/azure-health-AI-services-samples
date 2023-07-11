using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

namespace TextAnalyticsHealthcareAdaptiveClient.Core.Services;

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
        MaxAllowedPendingJobsSize = _dataProcessingOptions.InitialQueueSize;
    }


    public int MaxAllowedPendingJobsSize { get; private set; }


    public async Task StartAsync()
    {

        _logger.LogInformation($"{nameof(StartAsync)} called");

        var queueProcessingTask = StartJobsQueueProcessingAsync();
        var docsProcessed = _dataProcessingOptions.MaxDocs;
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
                await StartPaylodProcessing(payload);
            }
        }
        await queueProcessingTask;
    }

    private async Task StartJobsQueueProcessingAsync()
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
        _logger.LogInformation("Completed processing of all queue items");
    }

    private async Task WaitIfJobsQueueTooBigAsync()
    {
        while (_jobsQueue.Count >= MaxAllowedPendingJobsSize)
        {
            _logger.LogDebug("jobsQueueCount={jobsQueueCount} >= MaxSize={MaxSize}. waiting for next cycle", _jobsQueue.Count, MaxAllowedPendingJobsSize);
            await Task.Delay(500);
        }
        return;
    }

    private async Task StartPaylodProcessing(Ta4hInputPayload payload)
    {
        await WaitIfJobsQueueTooBigAsync();
        var jobId = await _textAnalyticsClient.StartHealthcareAnalysisOperationAsync(payload);
        _jobsQueue.Enqueue(new QueueItem(payload, payload.TotalCharLength, DateTime.UtcNow, NextCheckDateTime: DateTime.UtcNow + GetEstimatedProcessingTime(payload.TotalCharLength), LastCheckedDateTime: DateTime.UtcNow));
        _logger.LogDebug($"Job {jobId} started : Sent {payload.Documents.Count} docs for processing: {string.Join('|', payload.Documents.Select(d => d.Id).ToArray())}");
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
        await StartPaylodProcessing(firstHalf);
        await StartPaylodProcessing(secondHalf);
    }

    private async Task ProcessSuccessfulJobAsync(QueueItem item, TextAnlyticsJobResponse response)
    {
        TimeSpan jobDuration = response.LastUpdatedDateTime - response.CreatedDateTime;
        lock (_lock)
        {
            _completedItems.Add(new(item, jobDuration));
            if (_completedItems.Count == MaxAllowedPendingJobsSize)
            {
                var estiamtedMeanWaitTime = EstimateMeanWaitTimeForBatch();
                var currentMaxSize = MaxAllowedPendingJobsSize;
                int newMaxSize;
                if (estiamtedMeanWaitTime < TimeSpan.FromSeconds(60))
                {
                    newMaxSize = MaxAllowedPendingJobsSize * 2;
                }
                else
                {
                    newMaxSize = (int)(MaxAllowedPendingJobsSize * (TimeSpan.FromSeconds(90) / estiamtedMeanWaitTime));
                }
                if (newMaxSize > 300)
                {
                    newMaxSize = 300;
                }
                if (newMaxSize == 0)
                {
                    newMaxSize = 1;
                }
                MaxAllowedPendingJobsSize = newMaxSize;
                _logger.LogInformation("estiamtedMeanWaitTime: {estiamtedMeanWaitTime}, currentMaxSize: {currentMaxSize}, nextMaxSize: {MaxSize}", estiamtedMeanWaitTime, currentMaxSize, MaxAllowedPendingJobsSize);
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