using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StangaNetLib.Core.Common;
using StangaNetLib.Jobs.Implementations.InMemory;
using StangaNetLib.Jobs.Models;
using StangaNetLib.Jobs.Settings;

namespace StangaNetLib.Jobs.Tests.InMemory;

public sealed class InMemoryJobSchedulerTests
{
    private static (InMemoryJobScheduler scheduler, InMemoryJobStore store) Build()
    {
        var store = new InMemoryJobStore();
        var scheduler = new InMemoryJobScheduler(store);
        return (scheduler, store);
    }

    [Fact]
    public async Task EnqueueAsync_ValidArgs_ReturnsNonEmptyJobId()
    {
        var (scheduler, _) = Build();

        var result = await scheduler.EnqueueAsync<FakeJob, string>("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EnqueueAsync_TwoCalls_ReturnDistinctIds()
    {
        var (scheduler, _) = Build();

        var r1 = await scheduler.EnqueueAsync<FakeJob, string>("a");
        var r2 = await scheduler.EnqueueAsync<FakeJob, string>("b");

        r1.Value.Should().NotBe(r2.Value);
    }

    [Fact]
    public async Task EnqueueAsync_JobStoredInStore()
    {
        var (scheduler, store) = Build();

        var result = await scheduler.EnqueueAsync<FakeJob, string>("payload");

        store.GetJob(result.Value!).Should().NotBeNull();
    }

    [Fact]
    public async Task EnqueueAsync_JobIsExecutedByProcessor()
    {
        var executed = new TaskCompletionSource<string>();
        var store = new InMemoryJobStore();
        var scheduler = new InMemoryJobScheduler(store);

        var services = new ServiceCollection();
        services.AddSingleton(new FakeJob(args => executed.SetResult(args)));
        var sp = services.BuildServiceProvider();

        var options = Options.Create(new JobsSettings { WorkerCount = 1 });
        var processor = new InMemoryJobProcessor(
            store,
            sp.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<InMemoryJobProcessor>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var processorTask = processor.StartAsync(cts.Token);

        await scheduler.EnqueueAsync<FakeJob, string>("hello-payload");

        var received = await executed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().Be("hello-payload");

        await cts.CancelAsync();
    }

    [Fact]
    public async Task ScheduleAsync_NegativeDelay_ReturnsValidationFailure()
    {
        var (scheduler, _) = Build();

        // Fix #1: should return Result.Failure, not throw
        var result = await scheduler.ScheduleAsync<FakeJob, string>("x", TimeSpan.FromSeconds(-1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Contain("Delay");
    }

    [Fact]
    public async Task ScheduleAtAsync_PastTime_EnqueuesImmediately()
    {
        var (scheduler, store) = Build();

        var result = await scheduler.ScheduleAtAsync<FakeJob, string>("x", DateTimeOffset.UtcNow.AddMinutes(-5));

        result.IsSuccess.Should().BeTrue();
        store.GetJob(result.Value!).Should().NotBeNull();
    }

    [Fact]
    public async Task ScheduleAsync_FutureDelay_JobStoredAsScheduled()
    {
        var (scheduler, store) = Build();

        var result = await scheduler.ScheduleAsync<FakeJob, string>("x", TimeSpan.FromHours(1));

        result.IsSuccess.Should().BeTrue();
        var entry = store.GetJob(result.Value!);
        entry.Should().NotBeNull();
        entry!.State.Should().Be(JobState.Scheduled);
    }

    [Fact]
    public async Task TriggerRecurringAsync_ExistingJob_EnqueuesOneOffExecution()
    {
        var (scheduler, store) = Build();
        scheduler.AddOrUpdateRecurring<FakeJob, string>("my-job", "args", "0 * * * *");

        var result = await scheduler.TriggerRecurringAsync("my-job");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
        store.GetJob(result.Value!).Should().NotBeNull();
    }

    [Fact]
    public async Task TriggerRecurringAsync_NonExistingJob_ReturnsNotFound()
    {
        var (scheduler, _) = Build();

        var result = await scheduler.TriggerRecurringAsync("ghost-job");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task TriggerRecurringAsync_DoesNotAlterNextRunAt()
    {
        var (scheduler, store) = Build();
        scheduler.AddOrUpdateRecurring<FakeJob, string>("my-job", "args", "0 * * * *");
        var nextRunBefore = store.GetRecurring("my-job")!.NextRunAt;

        await scheduler.TriggerRecurringAsync("my-job");

        store.GetRecurring("my-job")!.NextRunAt.Should().Be(nextRunBefore);
    }
}
