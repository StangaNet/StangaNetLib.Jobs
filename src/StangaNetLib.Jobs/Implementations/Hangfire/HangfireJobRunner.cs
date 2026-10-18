using Hangfire;
using Hangfire.Server;
using StangaNetLib.Jobs.Abstractions;
using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Implementations.Hangfire;

/// <summary>
/// Internal Hangfire adapter that bridges the <see cref="IBackgroundJob{TArgs}"/> abstraction
/// to Hangfire's method-invocation model. Hangfire serialises <c>RunAsync</c>'s arguments and
/// resolves this class from DI on execution, injecting <see cref="PerformContext"/> automatically.
/// Registered as an open generic: <c>services.AddTransient(typeof(HangfireJobRunner&lt;,&gt;));</c>
/// </summary>
internal sealed class HangfireJobRunner<TJob, TArgs>
    where TJob : class, IBackgroundJob<TArgs>
{
    private readonly TJob _job;

    /// <summary>Initialises the runner with the wrapped job instance provided by DI.</summary>
    /// <param name="job">The concrete job implementation resolved from the DI container.</param>
    public HangfireJobRunner(TJob job)
    {
        _job = job;
    }

    /// <summary>
    /// Entry point called by Hangfire. Builds a <see cref="JobContext"/> from the Hangfire
    /// execution context, then delegates to <see cref="IBackgroundJob{TArgs}.ExecuteAsync"/>.
    /// </summary>
    /// <param name="args">The serialised job arguments stored by Hangfire at scheduling time.</param>
    /// <param name="performContext">
    /// Hangfire execution context injected automatically at runtime; pass <c>null!</c> in scheduling
    /// expressions — Hangfire replaces it before invoking this method.
    /// </param>
    [JobDisplayName("{0}")]
    public async Task RunAsync(TArgs args, PerformContext performContext)
    {
        var context = new JobContext
        {
            JobId = performContext.BackgroundJob.Id,
            AttemptNumber = performContext.GetJobParameter<int>("RetryCount") + 1,
            ScheduledAt = new DateTimeOffset(performContext.BackgroundJob.CreatedAt, TimeSpan.Zero)
        };
        await _job.ExecuteAsync(args, context);
    }
}
