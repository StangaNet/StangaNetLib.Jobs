using Hangfire;
using Hangfire.Storage;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.Jobs.Abstractions;
using StangaNetLib.Jobs.Errors;
using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Implementations.Hangfire;

/// <summary>
/// Hangfire implementation of <see cref="IJobManager"/>. Queries and mutates job state through
/// Hangfire's <see cref="JobStorage"/> connection and monitoring APIs.
/// </summary>
internal sealed class HangfireJobManager : IJobManager
{
    private readonly JobStorage _storage;
    private readonly IBackgroundJobClient _client;

    /// <summary>Initialises the manager with the Hangfire storage and job client.</summary>
    /// <param name="storage">Hangfire storage used to open connections and query job data.</param>
    /// <param name="client">Hangfire client used to issue delete commands.</param>
    public HangfireJobManager(JobStorage storage, IBackgroundJobClient client)
    {
        _storage = Guard.Against.Null(storage, nameof(storage));
        _client = Guard.Against.Null(client, nameof(client));
    }

    /// <inheritdoc/>
    public Task<Result<JobInfo>> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

        using var connection = _storage.GetConnection();
        var data = connection.GetJobData(jobId);
        if (data is null)
            return Task.FromResult(Result<JobInfo>.Failure(JobsErrors.JobNotFound(jobId)));

        var info = new JobInfo
        {
            Id = jobId,
            JobType = data.Job?.Type.Name ?? string.Empty,
            State = MapState(data.State),
            ScheduledAt = data.CreatedAt == default
                ? null
                : new DateTimeOffset(data.CreatedAt, TimeSpan.Zero)
        };
        return Task.FromResult(Result<JobInfo>.Success(info));
    }

    /// <inheritdoc/>
    public Task<Result> DeleteAsync(string jobId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

        using var connection = _storage.GetConnection();
        var data = connection.GetJobData(jobId);
        if (data is null)
            return Task.FromResult(Result.Failure(JobsErrors.JobNotFound(jobId)));

        _client.Delete(jobId);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<JobInfo>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var monitoring = _storage.GetMonitoringApi();
        var jobs = new List<JobInfo>();

        var enqueued = monitoring.EnqueuedJobs("default", 0, 50);
        jobs.AddRange(enqueued.Select(kv => new JobInfo
        {
            Id = kv.Key,
            JobType = kv.Value.Job?.Type.Name ?? string.Empty,
            State = JobState.Enqueued,
            ScheduledAt = kv.Value.EnqueuedAt.HasValue
                ? new DateTimeOffset(kv.Value.EnqueuedAt.Value, TimeSpan.Zero)
                : null
        }));

        var processing = monitoring.ProcessingJobs(0, 50);
        jobs.AddRange(processing.Select(kv => new JobInfo
        {
            Id = kv.Key,
            JobType = kv.Value.Job?.Type.Name ?? string.Empty,
            State = JobState.Processing,
            StartedAt = kv.Value.StartedAt.HasValue
                ? new DateTimeOffset(kv.Value.StartedAt.Value, TimeSpan.Zero)
                : null
        }));

        var scheduled = monitoring.ScheduledJobs(0, 50);
        jobs.AddRange(scheduled.Select(kv => new JobInfo
        {
            Id = kv.Key,
            JobType = kv.Value.Job?.Type.Name ?? string.Empty,
            State = JobState.Scheduled,
            ScheduledAt = kv.Value.EnqueueAt == default
                ? null
                : new DateTimeOffset(kv.Value.EnqueueAt, TimeSpan.Zero)
        }));

        var failed = monitoring.FailedJobs(0, 50);
        jobs.AddRange(failed.Select(kv => new JobInfo
        {
            Id = kv.Key,
            JobType = kv.Value.Job?.Type.Name ?? string.Empty,
            State = JobState.Failed,
            ErrorMessage = kv.Value.ExceptionMessage,
            CompletedAt = kv.Value.FailedAt.HasValue
                ? new DateTimeOffset(kv.Value.FailedAt.Value, TimeSpan.Zero)
                : null
        }));

        return Task.FromResult(Result<IReadOnlyList<JobInfo>>.Success(jobs.AsReadOnly()));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<JobInfo>>> GetRecurringJobsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _storage.GetConnection();
        var dtos = connection.GetRecurringJobs();

        IReadOnlyList<JobInfo> jobs = dtos.Select(d => new JobInfo
        {
            Id = d.Id,
            JobType = d.Job?.Type.Name ?? string.Empty,
            State = JobState.Recurring,
            CronExpression = d.Cron,
            ScheduledAt = d.LastExecution.HasValue
                ? new DateTimeOffset(d.LastExecution.Value, TimeSpan.Zero)
                : null,
            NextRunAt = d.NextExecution.HasValue
                ? new DateTimeOffset(d.NextExecution.Value, TimeSpan.Zero)
                : null
        })
        .ToList()
        .AsReadOnly();

        return Task.FromResult(Result<IReadOnlyList<JobInfo>>.Success(jobs));
    }

    private static JobState MapState(string? state) => state switch
    {
        "Enqueued" => JobState.Enqueued,
        "Processing" => JobState.Processing,
        "Succeeded" => JobState.Succeeded,
        "Failed" => JobState.Failed,
        "Deleted" => JobState.Deleted,
        "Scheduled" => JobState.Scheduled,
        _ => JobState.Enqueued
    };
}
