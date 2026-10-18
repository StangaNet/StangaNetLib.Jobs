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
    public async Task<Result<string>> EnqueueAsync<TJob, TArgs>(TArgs args, CancellationToken cancellationToken = default)
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
        await _store.EnqueueAsync(id, cancellationToken);
        return Result<string>.Success(id);
    }

    /// <inheritdoc/>
    public async Task<Result<string>> ScheduleAsync<TJob, TArgs>(TArgs args, TimeSpan delay, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        // Return Result.Failure rather than throwing — callers expect Result pattern
        if (delay < TimeSpan.Zero)
            return JobsErrors.InvalidDelay(delay);

        return await ScheduleAtAsync<TJob, TArgs>(args, DateTimeOffset.UtcNow.Add(delay), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Result<string>> ScheduleAtAsync<TJob, TArgs>(TArgs args, DateTimeOffset runAt, CancellationToken cancellationToken = default)
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
        // Use CancellationToken.None for the delay itself — the request token should not cancel
        // a job that has already been accepted. Explicit cancellation is handled via DeleteAsync.
        _ = Task.Run(async () =>
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            if (entry.State == JobState.Deleted) return;

            entry.State = JobState.Enqueued;
            await _store.EnqueueAsync(id, CancellationToken.None);
        });

        return Task.FromResult(Result<string>.Success(id));
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
