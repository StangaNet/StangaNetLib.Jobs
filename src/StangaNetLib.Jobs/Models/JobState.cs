namespace StangaNetLib.Jobs.Models;

/// <summary>The execution state of a background job.</summary>
public enum JobState
{
    /// <summary>Job is waiting in the queue to be picked up.</summary>
    Enqueued,
    /// <summary>Job is currently being executed by a worker.</summary>
    Processing,
    /// <summary>Job completed successfully.</summary>
    Succeeded,
    /// <summary>Job failed and all retry attempts are exhausted.</summary>
    Failed,
    /// <summary>Job was deleted before or during execution.</summary>
    Deleted,
    /// <summary>Job is a recurring entry (not a single execution).</summary>
    Recurring,
    /// <summary>Job is scheduled to run at a future point in time.</summary>
    Scheduled,
}
