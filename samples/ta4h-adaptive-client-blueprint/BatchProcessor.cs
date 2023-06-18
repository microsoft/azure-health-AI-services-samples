public static class BatchProcessor
{
    public static async Task<IEnumerable<U>> ProcessInBatchesAsync<T, U>(
        IEnumerable<T> data,
        int maxThreads,
        Func<T, Task<U>> processor
        )
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (processor == null) throw new ArgumentNullException(nameof(processor));
        if (maxThreads <= 0) throw new ArgumentException("maxThreads should be greater than 0", nameof(maxThreads));

        var results = new List<U>();
        var exceptions = new List<Exception>();

        var allData = data.ToList();
        for (int i = 0; i < allData.Count; i += maxThreads)
        {
            var batch = allData.Skip(i).Take(maxThreads);
            var tasks = new List<Task<U>>();

            foreach (var item in batch)
            {
                tasks.Add(Task.Run(() => processor(item)).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        lock (exceptions)
                        {
                            exceptions.AddRange(t.Exception.InnerExceptions);
                        }
                    }
                    return t.IsCompletedSuccessfully ? t.Result : default(U);
                }));
            }

            var batchResults = await Task.WhenAll(tasks);

            lock (results)
            {
                results.AddRange(batchResults.Where(r => r != null));
            }
        }

        if (exceptions.Any())
        {
            throw new AggregateException("One or more exceptions occurred during processing", exceptions);
        }

        return results;
    }
}
