using System.Collections.Concurrent;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StangaNetLib.Jobs.Models;
using StangaNetLib.Jobs.Settings;

namespace StangaNetLib.Jobs.Implementations.InMemory;

/// <summary>
/// Hosted service that drives the in-memory job backend. Spawns <see cref="JobsSettings.WorkerCount"/>
/// concurrent queue workers and one recurring-job scheduler loop.
/// </summary>
internal sealed class InMemoryJobProcessor : BackgroundService
{
    private readonly InMemoryJobStore _store;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<JobsSettings> _options;
    private readonly ILogger<InMemoryJobProcessor> _logger;

    // Tracks recurring jobs currently executing to prevent overlapping runs
    private readonly ConcurrentDictionary<string, byte> _runningRecurring = new();

    /// <summary>
    /// Initialises the processor with its required dependencies.
    /// </summary>
    /// <param name="store">The shared in-memory job store.</param>
    /// <param name="scopeFactory">Factory used to create a new DI scope per job execution.</param>
    /// <param name="options">Bound settings controlling retry behaviour and worker count.</param>
    /// <param name="logger">Logger for job lifecycle events.</param>
    public InMemoryJobProcessor(
        InMemoryJobStore store,
        IServiceScopeFactory scopeFactory,
        IOptions<JobsSettings> options,
        ILogger<InMemoryJobProcessor> logger)
    {
        _store = store;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _options.Value;
        var workerTasks = Enumerable.Range(0, settings.WorkerCount)
            .Select(_ => ProcessQueueAsync(stoppingToken))
            .ToList();
        workerTasks.Add(ProcessRecurringAsync(stoppingToken));
        await Task.WhenAll(workerTasks);
    }

    /// <summary>Drains the job queue until cancellation, executing each job with retry.</summary>
    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        await foreach (var jobId in _store.QueueReader.ReadAllAsync(ct))
        {
            var entry = _store.GetJob(jobId);
            if (entry is null || entry.State == JobState.Deleted) continue;

            await ExecuteWithRetryAsync(entry, ct);
        }
    }

    /// <summary>
    /// Executes <paramref name="entry"/> up to <c>DefaultRetryCount + 1</c> times,
    /// waiting <c>DefaultRetryDelay</c> between attempts.
    /// </summary>
    private async Task ExecuteWithRetryAsync(InMemoryJobEntry entry, CancellationToken ct)
    {
        var settings = _options.Value;
        var maxAttempts = settings.DefaultRetryCount + 1;

        while (entry.AttemptCount < maxAttempts)
        {
            entry.AttemptCount++;
            entry.State = JobState.Processing;
            entry.StartedAt = DateTimeOffset.UtcNow;

            var context = new JobContext
            {
                JobId = entry.Id,
                AttemptNumber = entry.AttemptCount,
                ScheduledAt = entry.ScheduledAt
            };

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await entry.Execute(scope.ServiceProvider, context, ct);

                entry.State = JobState.Succeeded;
                entry.CompletedAt = DateTimeOffset.UtcNow;
                _logger.LogDebug("Job {JobId} ({JobType}) succeeded on attempt {Attempt} in {Elapsed:F0}ms",
                    entry.Id, entry.JobType, entry.AttemptCount,
                    (entry.CompletedAt.Value - entry.StartedAt!.Value).TotalMilliseconds);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                entry.State = JobState.Enqueued;
                return;
            }
            catch (Exception ex)
            {
                entry.ErrorMessage = ex.Message;
                _logger.LogWarning(ex, "Job {JobId} ({JobType}) failed on attempt {Attempt}/{Max}",
                    entry.Id, entry.JobType, entry.AttemptCount, maxAttempts);

                if (entry.AttemptCount >= maxAttempts)
                {
                    entry.State = JobState.Failed;
                    entry.CompletedAt = DateTimeOffset.UtcNow;
                    return;
                }

                await Task.Delay(settings.DefaultRetryDelay, ct);
            }
        }
    }

    /// <summary>
    /// Periodically scans all recurring jobs and fires any that are due.
    /// Uses <see cref="JobsSettings.RecurringCheckInterval"/> as the polling period.
    /// </summary>
    private async Task ProcessRecurringAsync(CancellationToken ct)
    {
        var interval = _options.Value.RecurringCheckInterval;
        using var timer = new PeriodicTimer(interval);

        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var recurring in _store.GetAllRecurring())
            {
                if (recurring.NextRunAt.HasValue && recurring.NextRunAt.Value <= now)
                {
                    // Skip if this recurring job is still executing from a previous tick
                    if (_runningRecurring.TryAdd(recurring.Id, 0))
                    {
                        recurring.LastExecutedAt = now;
                        recurring.NextRunAt = ComputeNextRun(recurring.CronExpression, now);
                        _ = FireRecurringJobAsync(recurring, ct);
                    }
                    else
                    {
                        _logger.LogDebug("Recurring job {JobId} skipped: previous execution still running", recurring.Id);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Executes <paramref name="entry"/> in a new DI scope, then releases the concurrency slot.
    /// Any exception is caught and logged so it does not propagate to the scheduler loop.
    /// </summary>
    private async Task FireRecurringJobAsync(InMemoryRecurringJobEntry entry, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await entry.Execute(scope.ServiceProvider, ct);
            _logger.LogDebug("Recurring job {JobId} ({JobType}) completed successfully", entry.Id, entry.JobType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recurring job {JobId} ({JobType}) threw an exception", entry.Id, entry.JobType);
        }
        finally
        {
            _runningRecurring.TryRemove(entry.Id, out _);
        }
    }

    /// <summary>
    /// Parses <paramref name="cronExpression"/> and returns the next UTC occurrence after
    /// <paramref name="after"/>, or <c>null</c> if the expression produces no future occurrences.
    /// </summary>
    private static DateTimeOffset? ComputeNextRun(string cronExpression, DateTimeOffset after)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, CronFormat.Standard);
            return cron.GetNextOccurrence(after, TimeZoneInfo.Utc);
        }
        catch
        {
            return null;
        }
    }
}
