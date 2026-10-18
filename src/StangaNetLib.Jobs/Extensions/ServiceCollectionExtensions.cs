using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StangaNetLib.Concurrency.Locking;
using StangaNetLib.Jobs.Abstractions;
using StangaNetLib.Jobs.Implementations.Hangfire;
using StangaNetLib.Jobs.Implementations.InMemory;
using StangaNetLib.Jobs.Settings;

namespace StangaNetLib.Jobs.Extensions;

/// <summary>
/// Extension methods for registering StangaNetLib.Jobs services.
/// </summary>
/// <example>
/// appsettings.json:
/// <code>
/// {
///   "JobsSettings": {
///     "DefaultRetryCount": 3,
///     "DefaultRetryDelay": "00:00:05",
///     "WorkerCount": 4,
///     "RecurringCheckInterval": "00:00:30"
///   }
/// }
/// </code>
/// Program.cs (in-memory backend):
/// <code>
/// builder.Services.AddStangaNetLibJobs(builder.Configuration);
/// // Register your job classes:
/// builder.Services.AddTransient&lt;SendWelcomeEmailJob&gt;();
/// </code>
/// Program.cs (Hangfire backend):
/// <code>
/// builder.Services.AddStangaNetLibJobsWithHangfire(builder.Configuration, hangfire =>
///     hangfire.UseInMemoryStorage());
/// app.UseHangfireDashboard();
/// app.UseHangfireServer();
/// </code>
/// </example>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StangaNetLib.Jobs with the built-in <b>in-memory</b> backend.
    /// Suitable for development, testing, and single-instance deployments.
    /// Uses configuration from the <c>JobsSettings</c> section of <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JobsSettings>(configuration.GetSection(JobsSettings.SectionName));
        return RegisterInMemory(services);
    }

    /// <summary>
    /// Registers StangaNetLib.Jobs with the built-in <b>in-memory</b> backend using default settings.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibJobs(this IServiceCollection services)
    {
        services.Configure<JobsSettings>(_ => { });
        return RegisterInMemory(services);
    }

    /// <summary>
    /// Registers StangaNetLib.Jobs with the <b>Hangfire</b> backend.
    /// Requires an active <c>JobStorage</c> — configure storage via <paramref name="configureHangfire"/>
    /// (e.g. <c>.UseInMemoryStorage()</c>, <c>.UseSqlServerStorage(connStr)</c>).
    /// Uses configuration from the <c>JobsSettings</c> section of <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <param name="configureHangfire">Delegate to configure Hangfire storage and options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibJobsWithHangfire(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IGlobalConfiguration> configureHangfire)
    {
        services.Configure<JobsSettings>(configuration.GetSection(JobsSettings.SectionName));
        return RegisterHangfire(services, configureHangfire);
    }

    /// <summary>
    /// Registers StangaNetLib.Jobs with the <b>Hangfire</b> backend using default settings.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureHangfire">Delegate to configure Hangfire storage and options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibJobsWithHangfire(
        this IServiceCollection services,
        Action<IGlobalConfiguration> configureHangfire)
    {
        services.Configure<JobsSettings>(_ => { });
        return RegisterHangfire(services, configureHangfire);
    }

    private static IServiceCollection RegisterInMemory(IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<JobsSettings>, JobsSettingsValidator>();
        services.TryAddSingleton<InMemoryJobStore>();
        services.TryAddSingleton<IJobScheduler>(sp =>
            new InMemoryJobScheduler(
                sp.GetRequiredService<InMemoryJobStore>(),
                sp.GetService<IKeyedLock>()));
        services.TryAddSingleton<IJobManager, InMemoryJobManager>();
        services.AddHostedService<InMemoryJobProcessor>();
        return services;
    }

    private static IServiceCollection RegisterHangfire(IServiceCollection services, Action<IGlobalConfiguration> configureHangfire)
    {
        services.AddSingleton<IValidateOptions<JobsSettings>, JobsSettingsValidator>();
        services.AddHangfire(configureHangfire);
        services.AddHangfireServer();
        services.AddTransient(typeof(HangfireJobRunner<,>));
        services.TryAddSingleton<IJobScheduler, HangfireJobScheduler>();
        services.TryAddSingleton<IJobManager, HangfireJobManager>();
        return services;
    }
}
