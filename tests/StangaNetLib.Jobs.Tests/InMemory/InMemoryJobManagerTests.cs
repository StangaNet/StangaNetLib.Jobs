using StangaNetLib.Core.Common;
using StangaNetLib.Jobs.Implementations.InMemory;
using StangaNetLib.Jobs.Models;

namespace StangaNetLib.Jobs.Tests.InMemory;

public sealed class InMemoryJobManagerTests
{
    private static (InMemoryJobScheduler scheduler, InMemoryJobManager manager, InMemoryJobStore store) Build()
    {
        var store = new InMemoryJobStore();
        return (new InMemoryJobScheduler(store), new InMemoryJobManager(store), store);
    }

    [Fact]
    public async Task GetAsync_ExistingJob_ReturnsJobInfo()
    {
        var (scheduler, manager, _) = Build();
        var enqueueResult = await scheduler.EnqueueAsync<FakeJob, string>("test");

        var result = await manager.GetAsync(enqueueResult.Value!);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(enqueueResult.Value);
        result.Value.JobType.Should().Be(nameof(FakeJob));
    }

    [Fact]
    public async Task GetAsync_NonExistingJob_ReturnsNotFound()
    {
        var (_, manager, _) = Build();

        var result = await manager.GetAsync("nonexistent-id");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ExistingJob_RemovesFromStore()
    {
        var (scheduler, manager, store) = Build();
        var enqueueResult = await scheduler.EnqueueAsync<FakeJob, string>("test");

        var deleteResult = await manager.DeleteAsync(enqueueResult.Value!);

        deleteResult.IsSuccess.Should().BeTrue();
        store.GetJob(enqueueResult.Value!).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingJob_ReturnsNotFound()
    {
        var (_, manager, _) = Build();

        var result = await manager.DeleteAsync("ghost-id");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetAllAsync_NoJobs_ReturnsEmptyList()
    {
        var (_, manager, _) = Build();

        var result = await manager.GetAllAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_AfterEnqueue_ReturnsJobEntries()
    {
        var (scheduler, manager, _) = Build();
        await scheduler.EnqueueAsync<FakeJob, string>("a");
        await scheduler.EnqueueAsync<FakeJob, string>("b");

        var result = await manager.GetAllAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(j => j.JobType.Should().Be(nameof(FakeJob)));
    }

    [Fact]
    public async Task GetAllAsync_DeletedJobsExcluded()
    {
        var (scheduler, manager, _) = Build();
        var r1 = await scheduler.EnqueueAsync<FakeJob, string>("a");
        await scheduler.EnqueueAsync<FakeJob, string>("b");
        await manager.DeleteAsync(r1.Value!);

        var result = await manager.GetAllAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRecurringJobsAsync_NoRecurring_ReturnsEmptyList()
    {
        var (_, manager, _) = Build();

        var result = await manager.GetRecurringJobsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecurringJobsAsync_AfterAddRecurring_ReturnsEntry()
    {
        var (scheduler, manager, _) = Build();
        scheduler.AddOrUpdateRecurring<FakeJob, string>("my-recurring", "data", "0 * * * *");

        var result = await manager.GetRecurringJobsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(j => j.Id == "my-recurring");
        result.Value[0].State.Should().Be(JobState.Recurring);
        result.Value[0].CronExpression.Should().Be("0 * * * *");
    }
}
