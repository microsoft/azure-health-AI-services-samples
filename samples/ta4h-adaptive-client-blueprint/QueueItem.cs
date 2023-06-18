public record QueueItem(string JobId, int InputSize, DateTime CreatedDateTime, DateTime NextCheckDateTime, DateTime LastCheckedDateTime) { }

