using System.Collections.Concurrent;
using System.Threading.Channels;

namespace StangaNetLib.Jobs.Implementations.InMemory;

/// <summary>
/// Thread-safe in-process storage for job and recurring-job entries, plus the unbounded
/// channel used as the execution queue. Registered as a singleton and shared between
/// <see cref="InMemoryJobScheduler"/>, <see cref="InMemoryJobManager"/>, and <see cref="InMemoryJobProcessor"/>.
/// </summary>
internal sealed class InMemoryJobStore
{
    private readonly ConcurrentDictionary<string, InMemoryJobEntry> _jobs = new();
    private readonly ConcurrentDictionary<string, InMemoryRecurringJobEntry> _recurring = new();
    private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new(StringComparer.Ordinal);
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });

    /// <summary>Reader side of the job queue; consumed by worker loops in <see cref="InMemoryJobProcessor"/>.</summary>
    public ChannelReader<string> QueueReader => _queue.Reader;

    /// <summary>Adds or replaces the job entry for <see cref="InMemoryJobEntry.Id"/>.</summary>
    public void AddJob(InMemoryJobEntry entry)
        => _jobs[entry.Id] = entry;

    /// <summary>Returns the job entry for <paramref name="id"/>, or <c>null</c> if not found.</summary>
    public InMemoryJobEntry? GetJob(string id)
        => _jobs.TryGetValue(id, out var entry) ? entry : null;

    /// <summary>Removes the job entry for <paramref name="id"/>. Returns <c>true</c> if it existed.</summary>
    public bool RemoveJob(string id)
        => _jobs.TryRemove(id, out _);

    /// <summary>Returns a point-in-time snapshot of all job entries.</summary>
    public IReadOnlyCollection<InMemoryJobEntry> GetAllJobs()
        => _jobs.Values.ToList().AsReadOnly();

    /// <summary>Writes <paramref name="jobId"/> to the processing channel, making it available to worker loops.</summary>
    public ValueTask EnqueueAsync(string jobId, CancellationToken ct = default)
        => _queue.Writer.WriteAsync(jobId, ct);

    /// <summary>Adds or replaces the recurring entry for <see cref="InMemoryRecurringJobEntry.Id"/>.</summary>
    public void AddOrUpdateRecurring(InMemoryRecurringJobEntry entry)
        => _recurring[entry.Id] = entry;

    /// <summary>Removes the recurring entry for <paramref name="id"/>. Returns <c>true</c> if it existed.</summary>
    public bool RemoveRecurring(string id)
        => _recurring.TryRemove(id, out _);

    /// <summary>Returns the recurring entry for <paramref name="id"/>, or <c>null</c> if not found.</summary>
    public InMemoryRecurringJobEntry? GetRecurring(string id)
        => _recurring.TryGetValue(id, out var entry) ? entry : null;

    /// <summary>Returns a point-in-time snapshot of all recurring job entries.</summary>
    public IReadOnlyCollection<InMemoryRecurringJobEntry> GetAllRecurring()
        => _recurring.Values.ToList().AsReadOnly();

    /// <summary>Tries to get an existing job id for an idempotency key.</summary>
    public bool TryGetIdempotentJobId(string idempotencyKey, out string? jobId)
        => _idempotencyIndex.TryGetValue(idempotencyKey, out jobId);

    /// <summary>Indexes <paramref name="jobId"/> under <paramref name="idempotencyKey"/> if absent.</summary>
    public bool TryAddIdempotentJobId(string idempotencyKey, string jobId)
        => _idempotencyIndex.TryAdd(idempotencyKey, jobId);
}
