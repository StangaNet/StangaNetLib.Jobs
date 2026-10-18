using StangaNetLib.Jobs.Implementations.InMemory;

namespace StangaNetLib.Jobs.Tests.InMemory;

public sealed class RecurringJobTests
{
    private static (InMemoryJobScheduler scheduler, InMemoryJobStore store) Build()
    {
        var store = new InMemoryJobStore();
        return (new InMemoryJobScheduler(store), store);
    }

    [Fact]
    public void AddOrUpdateRecurring_ValidCron_ReturnsSuccess()
    {
        var (scheduler, store) = Build();

        var result = scheduler.AddOrUpdateRecurring<FakeJob, string>("job1", "args", "0 * * * *");

        result.IsSuccess.Should().BeTrue();
        store.GetRecurring("job1").Should().NotBeNull();
    }

    [Fact]
    public void AddOrUpdateRecurring_InvalidCron_ReturnsValidationFailure()
    {
        var (scheduler, _) = Build();

        var result = scheduler.AddOrUpdateRecurring<FakeJob, string>("job1", "args", "not-a-cron");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(StangaNetLib.Core.Common.ErrorType.Validation);
        result.Error.Code.Should().Contain("CronExpression");
    }

    [Fact]
    public void AddOrUpdateRecurring_SameId_UpdatesExisting()
    {
        var (scheduler, store) = Build();

        scheduler.AddOrUpdateRecurring<FakeJob, string>("job1", "v1", "0 * * * *");
        scheduler.AddOrUpdateRecurring<FakeJob, string>("job1", "v2", "0 0 * * *");

        store.GetAllRecurring().Should().ContainSingle(j => j.Id == "job1");
        store.GetRecurring("job1")!.CronExpression.Should().Be("0 0 * * *");
    }

    [Fact]
    public void RemoveRecurring_ExistingId_RemovesEntry()
    {
        var (scheduler, store) = Build();
        scheduler.AddOrUpdateRecurring<FakeJob, string>("job1", "args", "0 * * * *");

        var result = scheduler.RemoveRecurring("job1");

        result.IsSuccess.Should().BeTrue();
        store.GetRecurring("job1").Should().BeNull();
    }

    [Fact]
    public void RemoveRecurring_NonExistingId_ReturnsSuccess()
    {
        var (scheduler, _) = Build();

        // Idempotent — no error even if the ID never existed
        var result = scheduler.RemoveRecurring("ghost");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AddOrUpdateRecurring_NextRunAtIsPopulated()
    {
        var (scheduler, store) = Build();

        scheduler.AddOrUpdateRecurring<FakeJob, string>("job1", "args", "0 * * * *");

        store.GetRecurring("job1")!.NextRunAt.Should().NotBeNull();
        store.GetRecurring("job1")!.NextRunAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}
