namespace StangaNetLib.Jobs.Implementations.InMemory;

/// <summary>
/// Mutable record for a registered recurring job. Holds the cron schedule and
/// a captured execution delegate; state fields are mutated by the recurring processor.
/// </summary>
internal sealed class InMemoryRecurringJobEntry
{
    /// <summary>Stable identifier provided by the caller at registration time.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name derived from <c>typeof(TJob).Name</c>.</summary>
    public string JobType { get; init; } = string.Empty;

    /// <summary>Standard cron expression (5 fields) defining the execution schedule.</summary>
    public string CronExpression { get; init; } = string.Empty;

    /// <summary>Captured delegate that resolves the job from DI and calls <c>ExecuteAsync</c>.</summary>
    public Func<IServiceProvider, CancellationToken, Task> Execute { get; init; } = null!;

    /// <summary>UTC time of the last successful execution tick, or <c>null</c> if never run.</summary>
    public DateTimeOffset? LastExecutedAt { get; set; }

    /// <summary>UTC time of the next scheduled execution, computed from the cron expression.</summary>
    public DateTimeOffset? NextRunAt { get; set; }
}
