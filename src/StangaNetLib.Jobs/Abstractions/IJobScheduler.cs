using StangaNetLib.Core.Common;

namespace StangaNetLib.Jobs.Abstractions;

/// <summary>
/// Schedules fire-and-forget, delayed, and recurring background jobs.
/// </summary>
public interface IJobScheduler
{
    /// <summary>
    /// Enqueues a job for immediate execution.
    /// </summary>
    /// <typeparam name="TJob">The job type, must implement <see cref="IBackgroundJob{TArgs}"/>.</typeparam>
    /// <typeparam name="TArgs">The argument type for the job.</typeparam>
    /// <param name="args">The arguments to pass to the job at execution time.</param>
    /// <param name="cancellationToken">Token to cancel the enqueue operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the job ID, or a failure.</returns>
    Task<Result<string>> EnqueueAsync<TJob, TArgs>(TArgs args, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>;

    /// <summary>
    /// Enqueues a job with an idempotency key. Concurrent callers with the same key share one job id
    /// when an <c>IKeyedLock</c> is registered; otherwise behaves like <see cref="EnqueueAsync{TJob,TArgs}(TArgs,CancellationToken)"/>.
    /// </summary>
    Task<Result<string>> EnqueueAsync<TJob, TArgs>(
        TArgs args,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>;

    /// <summary>
    /// Schedules a job to run after the specified delay.
    /// Returns <c>Jobs.InvalidDelay</c> if <paramref name="delay"/> is negative.
    /// </summary>
    /// <typeparam name="TJob">The job type, must implement <see cref="IBackgroundJob{TArgs}"/>.</typeparam>
    /// <typeparam name="TArgs">The argument type for the job.</typeparam>
    /// <param name="args">The arguments to pass to the job at execution time.</param>
    /// <param name="delay">How long to wait before executing the job. Must be non-negative.</param>
    /// <param name="cancellationToken">Token to cancel the schedule operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the job ID, or a failure with <c>Jobs.InvalidDelay</c>.</returns>
    Task<Result<string>> ScheduleAsync<TJob, TArgs>(TArgs args, TimeSpan delay, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>;

    /// <summary>
    /// Schedules a job after <paramref name="delay"/> with an idempotency key (see enqueue overload).
    /// </summary>
    Task<Result<string>> ScheduleAsync<TJob, TArgs>(
        TArgs args,
        TimeSpan delay,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>;

    /// <summary>
    /// Schedules a job to run at a specific point in time.
    /// If <paramref name="runAt"/> is in the past, the job executes immediately.
    /// </summary>
    /// <typeparam name="TJob">The job type, must implement <see cref="IBackgroundJob{TArgs}"/>.</typeparam>
    /// <typeparam name="TArgs">The argument type for the job.</typeparam>
    /// <param name="args">The arguments to pass to the job at execution time.</param>
    /// <param name="runAt">The UTC time at which to execute the job. If in the past, executes immediately.</param>
    /// <param name="cancellationToken">Token to cancel the schedule operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the job ID, or a failure.</returns>
    Task<Result<string>> ScheduleAtAsync<TJob, TArgs>(TArgs args, DateTimeOffset runAt, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>;

    /// <summary>
    /// Schedules a job at <paramref name="runAt"/> with an idempotency key (see enqueue overload).
    /// </summary>
    Task<Result<string>> ScheduleAtAsync<TJob, TArgs>(
        TArgs args,
        DateTimeOffset runAt,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>;

    /// <summary>
    /// Adds or updates a recurring job identified by <paramref name="jobId"/>.
    /// If a recurring job with the same ID already exists, it is replaced.
    /// </summary>
    /// <typeparam name="TJob">The job type, must implement <see cref="IBackgroundJob{TArgs}"/>.</typeparam>
    /// <typeparam name="TArgs">The argument type for the job.</typeparam>
    /// <param name="jobId">A unique, stable identifier for this recurring job.</param>
    /// <param name="args">The arguments passed to the job each time it runs.</param>
    /// <param name="cronExpression">A cron expression defining the schedule (e.g. <c>"0 * * * *"</c> for hourly).</param>
    /// <returns><see cref="Result.Success()"/> if registered, or a failure with <c>Jobs.InvalidCronExpression</c>.</returns>
    Result AddOrUpdateRecurring<TJob, TArgs>(string jobId, TArgs args, string cronExpression)
        where TJob : class, IBackgroundJob<TArgs>;

    /// <summary>
    /// Removes the recurring job with the given ID. No-ops if the ID does not exist.
    /// </summary>
    /// <param name="jobId">The unique identifier of the recurring job to remove.</param>
    /// <returns><see cref="Result.Success()"/> always (idempotent removal).</returns>
    Result RemoveRecurring(string jobId);

    /// <summary>
    /// Immediately enqueues a one-off execution of the recurring job identified by <paramref name="jobId"/>,
    /// without affecting its next scheduled run.
    /// Returns <c>Jobs.NotFound</c> if no recurring job with that ID exists.
    /// </summary>
    /// <param name="jobId">The unique identifier of the recurring job to trigger.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the new one-off job ID, or a failure with <c>Jobs.NotFound</c>.</returns>
    Task<Result<string>> TriggerRecurringAsync(string jobId, CancellationToken cancellationToken = default);
}
