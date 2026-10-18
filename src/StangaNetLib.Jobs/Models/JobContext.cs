namespace StangaNetLib.Jobs.Models;

/// <summary>
/// Execution context passed to a job during <see cref="Abstractions.IBackgroundJob{TArgs}.ExecuteAsync"/>.
/// </summary>
public sealed class JobContext
{
    /// <summary>The unique identifier assigned to this job execution.</summary>
    public string JobId { get; init; } = string.Empty;

    /// <summary>
    /// The attempt number for this execution, starting at 1.
    /// Values greater than 1 indicate a retry.
    /// </summary>
    public int AttemptNumber { get; init; } = 1;

    /// <summary>The UTC time at which the job was originally scheduled or enqueued.</summary>
    public DateTimeOffset ScheduledAt { get; init; } = DateTimeOffset.UtcNow;
}
