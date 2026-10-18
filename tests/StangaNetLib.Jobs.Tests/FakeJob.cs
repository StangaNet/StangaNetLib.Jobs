using StangaNetLib.Jobs.Abstractions;
using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Tests;

internal sealed class FakeJob : IBackgroundJob<string>
{
    private readonly Action<string>? _onExecute;

    public FakeJob() { }

    public FakeJob(Action<string> onExecute) => _onExecute = onExecute;

    public Task ExecuteAsync(string args, JobContext context, CancellationToken cancellationToken = default)
    {
        _onExecute?.Invoke(args);
        return Task.CompletedTask;
    }
}
