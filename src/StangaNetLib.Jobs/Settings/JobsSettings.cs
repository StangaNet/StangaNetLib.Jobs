namespace StangaNetLib.Jobs.Settings;

/// <summary>Configuration for StangaNetLib.Jobs services.</summary>
public sealed class JobsSettings
{
    /// <summary>The <c>appsettings.json</c> section key. Value: <c>"JobsSettings"</c>.</summary>
    public const string SectionName = "JobsSettings";

    /// <summary>
    /// Number of times a failed job is retried by the in-memory backend before being marked as <c>Failed</c>.
    /// Has no effect on the Hangfire backend (configure Hangfire's retry filters separately).
    /// Defaults to 3.
    /// </summary>
    public int DefaultRetryCount { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in the in-memory backend.
    /// Defaults to 5 seconds.
    /// </summary>
    public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Number of concurrent workers processing the in-memory job queue.
    /// Defaults to 4.
    /// </summary>
    public int WorkerCount { get; set; } = 4;

    /// <summary>
    /// How often the in-memory recurring-job scheduler checks for due jobs.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan RecurringCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
}
