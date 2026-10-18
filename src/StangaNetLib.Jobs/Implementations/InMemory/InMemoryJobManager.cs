using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.Jobs.Abstractions;
using StangaNetLib.Jobs.Errors;
using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Implementations.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IJobManager"/>. Reads and mutates entries
/// in the shared <see cref="InMemoryJobStore"/>.
/// </summary>
internal sealed class InMemoryJobManager : IJobManager
{
    private readonly InMemoryJobStore _store;

    /// <summary>Initialises the manager with the shared job store.</summary>
    /// <param name="store">The singleton store shared with the scheduler and processor.</param>
    public InMemoryJobManager(InMemoryJobStore store)
    {
        _store = Guard.Against.Null(store, nameof(store));
    }

    /// <inheritdoc/>
    public Task<Result<JobInfo>> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

        var entry = _store.GetJob(jobId);
        if (entry is null)
            return Task.FromResult(Result<JobInfo>.Failure(JobsErrors.JobNotFound(jobId)));

        return Task.FromResult(Result<JobInfo>.Success(MapToInfo(entry)));
    }

    /// <inheritdoc/>
    public Task<Result> DeleteAsync(string jobId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

        var entry = _store.GetJob(jobId);
        if (entry is null)
            return Task.FromResult(Result.Failure(JobsErrors.JobNotFound(jobId)));

        entry.State = JobState.Deleted;
        _store.RemoveJob(jobId);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<JobInfo>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JobInfo> jobs = _store.GetAllJobs()
            .Where(e => e.State != JobState.Deleted)
            .Select(MapToInfo)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(Result<IReadOnlyList<JobInfo>>.Success(jobs));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<JobInfo>>> GetRecurringJobsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JobInfo> result = _store.GetAllRecurring()
            .Select(r => new JobInfo
            {
                Id = r.Id,
                JobType = r.JobType,
                State = JobState.Recurring,
                CronExpression = r.CronExpression,
                ScheduledAt = r.LastExecutedAt,
                NextRunAt = r.NextRunAt
            })
            .ToList()
            .AsReadOnly();

        return Task.FromResult(Result<IReadOnlyList<JobInfo>>.Success(result));
    }

    private static JobInfo MapToInfo(InMemoryJobEntry entry) => new()
    {
        Id = entry.Id,
        JobType = entry.JobType,
        State = entry.State,
        ScheduledAt = entry.ScheduledAt,
        StartedAt = entry.StartedAt,
        CompletedAt = entry.CompletedAt,
        ErrorMessage = entry.ErrorMessage
    };
}
