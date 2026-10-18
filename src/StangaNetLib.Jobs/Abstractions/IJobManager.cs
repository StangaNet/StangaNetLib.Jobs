using StangaNetLib.Core.Common;
using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Abstractions;

/// <summary>
/// Queries and manages existing background jobs.
/// </summary>
public interface IJobManager
{
    /// <summary>
    /// Retrieves information about a specific job by ID.
    /// </summary>
    /// <param name="jobId">The job identifier returned by <see cref="IJobScheduler"/>.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> with <see cref="JobInfo"/>, or a failure with <c>Jobs.NotFound</c>.</returns>
    Task<Result<JobInfo>> GetAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a job. For pending/scheduled jobs the execution is cancelled.
    /// For already-executed jobs the record is removed from storage.
    /// </summary>
    /// <param name="jobId">The job identifier to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see cref="Result.Success()"/> if deleted, or a failure with <c>Jobs.NotFound</c>.</returns>
    Task<Result> DeleteAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all non-recurring jobs currently tracked by the backend
    /// (enqueued, processing, scheduled, succeeded, failed).
    /// Results are capped at a reasonable page size by the backend implementation.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> with the list of <see cref="JobInfo"/> records.</returns>
    Task<Result<IReadOnlyList<JobInfo>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all currently registered recurring jobs.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> with the list of recurring <see cref="JobInfo"/> records.</returns>
    Task<Result<IReadOnlyList<JobInfo>>> GetRecurringJobsAsync(CancellationToken cancellationToken = default);
}
