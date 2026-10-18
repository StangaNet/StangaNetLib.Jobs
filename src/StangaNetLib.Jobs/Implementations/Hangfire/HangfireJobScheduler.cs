using Cronos;
using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.Jobs.Abstractions;
using StangaNetLib.Jobs.Errors;

namespace StangaNetLib.Jobs.Implementations.Hangfire;

/// <summary>
/// Hangfire implementation of <see cref="IJobScheduler"/>. Bridges scheduling operations to
/// <see cref="IBackgroundJobClient"/> and <see cref="IRecurringJobManager"/> via the
/// <see cref="HangfireJobRunner{TJob,TArgs}"/> adapter.
/// </summary>
internal sealed class HangfireJobScheduler : IJobScheduler
{
    private readonly IBackgroundJobClient _client;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly JobStorage _storage;

    /// <summary>Initialises the scheduler with the Hangfire infrastructure dependencies.</summary>
    /// <param name="client">Hangfire client used to enqueue and schedule one-off jobs.</param>
    /// <param name="recurringJobManager">Hangfire manager used to add, update, and remove recurring jobs.</param>
    /// <param name="storage">Hangfire storage used to look up recurring job definitions for manual triggering.</param>
    public HangfireJobScheduler(IBackgroundJobClient client, IRecurringJobManager recurringJobManager, JobStorage storage)
    {
        _client = Guard.Against.Null(client, nameof(client));
        _recurringJobManager = Guard.Against.Null(recurringJobManager, nameof(recurringJobManager));
        _storage = Guard.Against.Null(storage, nameof(storage));
    }

    /// <inheritdoc/>
    public Task<Result<string>> EnqueueAsync<TJob, TArgs>(TArgs args, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        var id = _client.Enqueue<HangfireJobRunner<TJob, TArgs>>(r => r.RunAsync(args, null!));
        return Task.FromResult(Result<string>.Success(id));
    }

    /// <inheritdoc/>
    public Task<Result<string>> ScheduleAsync<TJob, TArgs>(TArgs args, TimeSpan delay, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        if (delay < TimeSpan.Zero)
            return Task.FromResult(Result<string>.Failure(JobsErrors.InvalidDelay(delay)));

        var id = _client.Schedule<HangfireJobRunner<TJob, TArgs>>(r => r.RunAsync(args, null!), delay);
        return Task.FromResult(Result<string>.Success(id));
    }

    /// <inheritdoc/>
    public Task<Result<string>> ScheduleAtAsync<TJob, TArgs>(TArgs args, DateTimeOffset runAt, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        var effectiveRunAt = runAt < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : runAt;
        var id = _client.Schedule<HangfireJobRunner<TJob, TArgs>>(r => r.RunAsync(args, null!), effectiveRunAt);
        return Task.FromResult(Result<string>.Success(id));
    }

    /// <inheritdoc/>
    public Result AddOrUpdateRecurring<TJob, TArgs>(string jobId, TArgs args, string cronExpression)
        where TJob : class, IBackgroundJob<TArgs>
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));
        Guard.Against.NullOrWhiteSpace(cronExpression, nameof(cronExpression));

        try
        {
            CronExpression.Parse(cronExpression, CronFormat.Standard);
        }
        catch (CronFormatException)
        {
            return JobsErrors.InvalidCronExpression(cronExpression);
        }

        _recurringJobManager.AddOrUpdate<HangfireJobRunner<TJob, TArgs>>(
            jobId,
            r => r.RunAsync(args, null!),
            cronExpression);

        return Result.Success();
    }

    /// <inheritdoc/>
    public Result RemoveRecurring(string jobId)
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));
        _recurringJobManager.RemoveIfExists(jobId);
        return Result.Success();
    }

    /// <inheritdoc/>
    public Task<Result<string>> TriggerRecurringAsync(string jobId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

        using var connection = _storage.GetConnection();
        var recurring = connection.GetRecurringJobs()
            .FirstOrDefault(j => j.Id == jobId);

        if (recurring?.Job is null)
            return Task.FromResult(Result<string>.Failure(JobsErrors.JobNotFound(jobId)));

        // IRecurringJobManager has no Trigger method on its interface; re-enqueue via the raw Job descriptor
        var id = _client.Create(recurring.Job, new EnqueuedState());
        return Task.FromResult(Result<string>.Success(id));
    }
}
