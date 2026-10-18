using StangaNetLib.Core.Validators;

namespace StangaNetLib.Jobs.Settings;

/// <summary>
/// Validates <see cref="JobsSettings"/> at application startup.
/// Registered automatically by <c>AddStangaNetLibJobs</c> and <c>AddStangaNetLibJobsWithHangfire</c>.
/// </summary>
internal sealed class JobsSettingsValidator : SettingsValidatorBase<JobsSettings>
{
    /// <inheritdoc/>
    protected override void ValidateCore(JobsSettings options, List<string> errors)
    {
        if (options.WorkerCount < 1)
            errors.Add($"{nameof(JobsSettings.WorkerCount)} must be at least 1. Received: {options.WorkerCount}.");

        if (options.DefaultRetryCount < 0)
            errors.Add($"{nameof(JobsSettings.DefaultRetryCount)} must be 0 or greater. Received: {options.DefaultRetryCount}.");

        if (options.DefaultRetryDelay < TimeSpan.Zero)
            errors.Add($"{nameof(JobsSettings.DefaultRetryDelay)} must be non-negative. Received: {options.DefaultRetryDelay}.");

        if (options.RecurringCheckInterval < TimeSpan.FromSeconds(1))
            errors.Add($"{nameof(JobsSettings.RecurringCheckInterval)} must be at least 1 second. Received: {options.RecurringCheckInterval}.");
    }
}
