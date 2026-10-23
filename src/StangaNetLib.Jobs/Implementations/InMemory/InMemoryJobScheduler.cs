using Cronos;
using Microsoft.Extensions.DependencyInjection;
using StangaNetLib.Concurrency.Locking;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.Jobs.Abstractions;
using StangaNetLib.Jobs.Errors;
using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Implementations.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IJobScheduler"/>. Stores jobs in <see cref="InMemoryJobStore"/>
/// and relies on <see cref="InMemoryJobProcessor"/> to execute them.
/// </summary>
/// <remarks>
/// When an <see cref="IKeyedLock"/> is supplied, <see cref="TriggerRecurringAsync"/> acquires a
/// per-<c>jobId</c> lock before checking for the recurring job and enqueueing its one-off copy.
/// This prevents two concurrent triggers of the same recurring job from racing and both succeeding.
/// The Hangfire backend provides equivalent protection via its database-level distributed lock.
/// </remarks>
internal sealed class InMemoryJobScheduler : IJobScheduler
{
    private readonly InMemoryJobStore _store;
    private readonly IKeyedLock? _keyedLock;

    /// <summary>Initialises the scheduler with the shared job store.</summary>
    /// <param name="store">The singleton store shared with the manager and processor.</param>
    /// <param name="keyedLock">
    /// Optional per-key async lock. When provided, <see cref="TriggerRecurringAsync"/> acquires
    /// a lock on the recurring <c>jobId</c> to prevent concurrent duplicate enqueueing.
    /// </param>
    public InMemoryJobScheduler(InMemoryJobStore store, IKeyedLock? keyedLock = null)
    {
        _store = Guard.Against.Null(store, nameof(store));
        _keyedLock = keyedLock;
    }

    /// <inheritdoc/>
    public Task<Result<string>> EnqueueAsync<TJob, TArgs>(TArgs args, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
        => EnqueueCoreAsync<TJob, TArgs>(args, idempotencyKey: null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<string>> EnqueueAsync<TJob, TArgs>(
        TArgs args,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        Guard.Against.NullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        return EnqueueCoreAsync<TJob, TArgs>(args, idempotencyKey, cancellationToken);
    }

    private async Task<Result<string>> EnqueueCoreAsync<TJob, TArgs>(
        TArgs args,
        string? idempotencyKey,
        CancellationToken cancellationToken)
        where TJob : class, IBackgroundJob<TArgs>
    {
        if (idempotencyKey is not null && _keyedLock is not null)
        {
            await using var handle = await _keyedLock.AcquireAsync($"jobs:idem:{idempotencyKey}", cancellationToken)
                .ConfigureAwait(false);
            if (_store.TryGetIdempotentJobId(idempotencyKey, out var existing) && existing is not null)
                return Result<string>.Success(existing);

            var id = await CreateAndEnqueueAsync<TJob, TArgs>(args, cancellationToken).ConfigureAwait(false);
            _store.TryAddIdempotentJobId(idempotencyKey, id);
            return Result<string>.Success(id);
        }

        if (idempotencyKey is not null
            && _store.TryGetIdempotentJobId(idempotencyKey, out var known)
            && known is not null)
        {
            return Result<string>.Success(known);
        }

        var newId = await CreateAndEnqueueAsync<TJob, TArgs>(args, cancellationToken).ConfigureAwait(false);
        if (idempotencyKey is not null)
            _store.TryAddIdempotentJobId(idempotencyKey, newId);
        return Result<string>.Success(newId);
    }

    private async Task<string> CreateAndEnqueueAsync<TJob, TArgs>(TArgs args, CancellationToken cancellationToken)
        where TJob : class, IBackgroundJob<TArgs>
    {
        var id = NewId();
        _store.AddJob(new InMemoryJobEntry
        {
            Id = id,
            JobType = typeof(TJob).Name,
            ScheduledAt = DateTimeOffset.UtcNow,
            Execute = (sp, ctx, ct) =>
            {
                var job = sp.GetRequiredService<TJob>();
                return job.ExecuteAsync(args, ctx, ct);
            }
        });
        await _store.EnqueueAsync(id, cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <inheritdoc/>
    public Task<Result<string>> ScheduleAsync<TJob, TArgs>(TArgs args, TimeSpan delay, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
        => ScheduleAsync<TJob, TArgs>(args, delay, idempotencyKey: null!, cancellationToken, hasKey: false);

    /// <inheritdoc/>
    public Task<Result<string>> ScheduleAsync<TJob, TArgs>(
        TArgs args,
        TimeSpan delay,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        Guard.Against.NullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        return ScheduleAsync<TJob, TArgs>(args, delay, idempotencyKey, cancellationToken, hasKey: true);
    }

    private async Task<Result<string>> ScheduleAsync<TJob, TArgs>(
        TArgs args,
        TimeSpan delay,
        string? idempotencyKey,
        CancellationToken cancellationToken,
        bool hasKey)
        where TJob : class, IBackgroundJob<TArgs>
    {
        if (delay < TimeSpan.Zero)
            return JobsErrors.InvalidDelay(delay);

        return hasKey
            ? await ScheduleAtAsync<TJob, TArgs>(args, DateTimeOffset.UtcNow.Add(delay), idempotencyKey!, cancellationToken)
                .ConfigureAwait(false)
            : await ScheduleAtAsync<TJob, TArgs>(args, DateTimeOffset.UtcNow.Add(delay), cancellationToken)
                .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<Result<string>> ScheduleAtAsync<TJob, TArgs>(TArgs args, DateTimeOffset runAt, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
        => ScheduleAtCoreAsync<TJob, TArgs>(args, runAt, idempotencyKey: null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<string>> ScheduleAtAsync<TJob, TArgs>(
        TArgs args,
        DateTimeOffset runAt,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        Guard.Against.NullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        return ScheduleAtCoreAsync<TJob, TArgs>(args, runAt, idempotencyKey, cancellationToken);
    }

    private async Task<Result<string>> ScheduleAtCoreAsync<TJob, TArgs>(
        TArgs args,
        DateTimeOffset runAt,
        string? idempotencyKey,
        CancellationToken cancellationToken)
        where TJob : class, IBackgroundJob<TArgs>
    {
        if (idempotencyKey is not null && _keyedLock is not null)
        {
            await using var handle = await _keyedLock.AcquireAsync($"jobs:idem:{idempotencyKey}", cancellationToken)
                .ConfigureAwait(false);
            if (_store.TryGetIdempotentJobId(idempotencyKey, out var existing) && existing is not null)
                return Result<string>.Success(existing);

            var id = ScheduleAtInternal<TJob, TArgs>(args, runAt);
            _store.TryAddIdempotentJobId(idempotencyKey, id);
            return Result<string>.Success(id);
        }

        if (idempotencyKey is not null
            && _store.TryGetIdempotentJobId(idempotencyKey, out var known)
            && known is not null)
        {
            return Result<string>.Success(known);
        }

        var newId = ScheduleAtInternal<TJob, TArgs>(args, runAt);
        if (idempotencyKey is not null)
            _store.TryAddIdempotentJobId(idempotencyKey, newId);
        return Result<string>.Success(newId);
    }

    private string ScheduleAtInternal<TJob, TArgs>(TArgs args, DateTimeOffset runAt)
        where TJob : class, IBackgroundJob<TArgs>
    {
        var id = NewId();
        var scheduledAt = runAt < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : runAt;

        var entry = new InMemoryJobEntry
        {
            Id = id,
            JobType = typeof(TJob).Name,
            ScheduledAt = scheduledAt,
            State = JobState.Scheduled,
            Execute = (sp, ctx, ct) =>
            {
                var job = sp.GetRequiredService<TJob>();
                return job.ExecuteAsync(args, ctx, ct);
            }
        };
        _store.AddJob(entry);

        var delay = scheduledAt - DateTimeOffset.UtcNow;
        _ = Task.Run(async () =>
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            if (entry.State == JobState.Deleted) return;

            entry.State = JobState.Enqueued;
            await _store.EnqueueAsync(id, CancellationToken.None);
        });

        return id;
    }

    /// <summary>
    /// Materializes a recurring occurrence as a scheduled one-off job (used by the horizon processor).
    /// </summary>
    internal string MaterializeRecurringOccurrence(InMemoryRecurringJobEntry recurring, DateTimeOffset runAt)
    {
        var id = NewId();
        var scheduledAt = runAt < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : runAt;
        var entry = new InMemoryJobEntry
        {
            Id = id,
            JobType = recurring.JobType,
            ScheduledAt = scheduledAt,
            State = JobState.Scheduled,
            Execute = (sp, ctx, ct) => recurring.Execute(sp, ct)
        };
        _store.AddJob(entry);

        var delay = scheduledAt - DateTimeOffset.UtcNow;
        _ = Task.Run(async () =>
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            if (entry.State == JobState.Deleted) return;

            entry.State = JobState.Enqueued;
            await _store.EnqueueAsync(id, CancellationToken.None);
        });

        return id;
    }

    /// <inheritdoc/>
    public Result AddOrUpdateRecurring<TJob, TArgs>(string jobId, TArgs args, string cronExpression)
        where TJob : class, IBackgroundJob<TArgs>
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));
        Guard.Against.NullOrWhiteSpace(cronExpression, nameof(cronExpression));

        CronExpression cron;
        try
        {
            cron = CronExpression.Parse(cronExpression, CronFormat.Standard);
        }
        catch (CronFormatException)
        {
            return JobsErrors.InvalidCronExpression(cronExpression);
        }

        var nextRun = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
        var entry = new InMemoryRecurringJobEntry
        {
            Id = jobId,
            JobType = typeof(TJob).Name,
            CronExpression = cronExpression,
            NextRunAt = nextRun,
            Execute = (sp, ct) =>
            {
                var job = sp.GetRequiredService<TJob>();
                var context = new JobContext
                {
                    JobId = jobId,
                    AttemptNumber = 1,
                    ScheduledAt = DateTimeOffset.UtcNow
                };
                return job.ExecuteAsync(args, context, ct);
            }
        };
        _store.AddOrUpdateRecurring(entry);
        return Result.Success();
    }

    /// <inheritdoc/>
    public Result RemoveRecurring(string jobId)
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));
        _store.RemoveRecurring(jobId);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<string>> TriggerRecurringAsync(string jobId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

        IAsyncDisposable? lockHandle = null;
        if (_keyedLock is not null)
            lockHandle = await _keyedLock.AcquireAsync(jobId, cancellationToken);

        try
        {
            var recurring = _store.GetRecurring(jobId);
            if (recurring is null)
                return JobsErrors.JobNotFound(jobId);

            var id = NewId();
            _store.AddJob(new InMemoryJobEntry
            {
                Id = id,
                JobType = recurring.JobType,
                ScheduledAt = DateTimeOffset.UtcNow,
                Execute = (sp, ctx, ct) => recurring.Execute(sp, ct)
            });
            await _store.EnqueueAsync(id, cancellationToken);
            return Result<string>.Success(id);
        }
        finally
        {
            if (lockHandle is not null)
                await lockHandle.DisposeAsync();
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}
