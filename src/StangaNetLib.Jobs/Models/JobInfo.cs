namespace StangaNetLib.Jobs.Models;

/// <summary>A snapshot of a background job's current state and metadata.</summary>
public sealed class JobInfo
{
    /// <summary>The unique identifier of the job.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The display name or type name of the job.</summary>
    public string JobType { get; init; } = string.Empty;

    /// <summary>The current state of the job.</summary>
    public JobState State { get; init; }

    /// <summary>The cron expression for recurring jobs; <c>null</c> for one-off jobs.</summary>
    public string? CronExpression { get; init; }

    /// <summary>The UTC time when the job was enqueued or scheduled.</summary>
    public DateTimeOffset? ScheduledAt { get; init; }

    /// <summary>The UTC time when job execution started.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>The UTC time when job execution completed (success or failure).</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>The next scheduled UTC execution time for recurring jobs.</summary>
    public DateTimeOffset? NextRunAt { get; init; }

    /// <summary>The error message from the last failed attempt, if any.</summary>
    public string? ErrorMessage { get; init; }
}
