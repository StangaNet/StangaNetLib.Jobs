using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Implementations.InMemory;

/// <summary>
/// Mutable record for a single in-memory job execution. Shared between the scheduler
/// (which creates it) and the processor (which mutates its state fields).
/// </summary>
internal sealed class InMemoryJobEntry
{
    /// <summary>Unique job identifier assigned at creation time.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name derived from <c>typeof(TJob).Name</c>.</summary>
    public string JobType { get; init; } = string.Empty;

    /// <summary>Captured delegate that resolves the job from DI and calls <c>ExecuteAsync</c>.</summary>
    public Func<IServiceProvider, JobContext, CancellationToken, Task> Execute { get; init; } = null!;

    /// <summary>UTC time when the job was enqueued or scheduled.</summary>
    public DateTimeOffset ScheduledAt { get; init; }

    /// <summary>Current lifecycle state; mutated by the processor.</summary>
    public JobState State { get; set; } = JobState.Enqueued;

    /// <summary>UTC time when the most recent execution attempt started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>UTC time when the job reached a terminal state (Succeeded or Failed).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Error message from the last failed attempt, if any.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Total number of execution attempts made so far.</summary>
    public int AttemptCount { get; set; }
}
