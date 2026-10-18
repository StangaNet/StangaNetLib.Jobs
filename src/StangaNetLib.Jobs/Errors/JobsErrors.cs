using StangaNetLib.Core.Common;

namespace StangaNetLib.Jobs.Errors;

/// <summary>Well-known errors produced by <c>StangaNetLib.Jobs</c>.</summary>
public static class JobsErrors
{
    /// <summary>The requested job ID does not exist in the backend.</summary>
    /// <param name="jobId">The job identifier that was looked up.</param>
    /// <returns>A <see cref="Error"/> with type <see cref="ErrorType.NotFound"/>.</returns>
    public static Error JobNotFound(string jobId)
        => Error.NotFound("Job", jobId);

    /// <summary>The provided cron expression could not be parsed.</summary>
    /// <param name="expression">The invalid cron expression string.</param>
    /// <returns>A <see cref="Error"/> with type <see cref="ErrorType.Validation"/> and code <c>Validation.CronExpression</c>.</returns>
    public static Error InvalidCronExpression(string expression)
        => Error.Validation("CronExpression", $"'{expression}' is not a valid cron expression.");

    /// <summary>The delay passed to <c>ScheduleAsync</c> is negative.</summary>
    /// <param name="delay">The negative <see cref="TimeSpan"/> that was supplied.</param>
    /// <returns>A <see cref="Error"/> with type <see cref="ErrorType.Validation"/> and code <c>Validation.Delay</c>.</returns>
    public static Error InvalidDelay(TimeSpan delay)
        => Error.Validation("Delay", $"Delay must be non-negative. Received: {delay}.");

    /// <summary>The job backend is unavailable or not responding.</summary>
    /// <param name="detail">A human-readable description of the failure.</param>
    /// <returns>A <see cref="Error"/> with type <see cref="ErrorType.Internal"/>.</returns>
    public static Error BackendUnavailable(string detail)
        => Error.Internal($"Job backend unavailable: {detail}");
}
