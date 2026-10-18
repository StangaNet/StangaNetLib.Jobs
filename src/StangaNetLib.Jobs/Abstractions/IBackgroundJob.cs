using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Abstractions;

/// <summary>
/// Defines a background job that accepts typed arguments.
/// Implement this interface for each job type and register the implementation in the DI container.
/// </summary>
/// <typeparam name="TArgs">The argument type passed to the job at execution time.</typeparam>
/// <example>
/// <code>
/// public sealed class SendWelcomeEmailJob : IBackgroundJob&lt;SendWelcomeEmailArgs&gt;
/// {
///     private readonly IEmailSender _email;
///
///     public SendWelcomeEmailJob(IEmailSender email) => _email = email;
///
///     public async Task ExecuteAsync(SendWelcomeEmailArgs args, JobContext context, CancellationToken ct)
///         => await _email.SendAsync(args.To, "Welcome!", ct);
/// }
/// </code>
/// </example>
public interface IBackgroundJob<TArgs>
{
    /// <summary>
    /// Executes the job with the provided arguments and context.
    /// Throw an exception to signal failure; the backend will retry according to its retry policy.
    /// </summary>
    /// <param name="args">The arguments provided when the job was scheduled.</param>
    /// <param name="context">Execution context containing job ID, attempt number, and scheduled time.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task ExecuteAsync(TArgs args, JobContext context, CancellationToken cancellationToken = default);
}
