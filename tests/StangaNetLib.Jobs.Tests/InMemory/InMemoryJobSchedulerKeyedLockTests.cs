using System.Collections.Concurrent;
using StangaNetLib.Concurrency.Locking;
using StangaNetLib.Core.Common;
using StangaNetLib.Jobs.Implementations.InMemory;

namespace StangaNetLib.Jobs.Tests.InMemory;

public sealed class InMemoryJobSchedulerKeyedLockTests
{
    private static (InMemoryJobScheduler scheduler, InMemoryJobStore store) Build(IKeyedLock? keyedLock = null)
    {
        var store = new InMemoryJobStore();
        var scheduler = new InMemoryJobScheduler(store, keyedLock);
        return (scheduler, store);
    }

    // ── Without lock: existing behaviour unchanged ────────────────────────────

    [Fact]
    public async Task TriggerRecurringAsync_WithoutKeyedLock_StillSucceeds()
    {
        var (scheduler, _) = Build(keyedLock: null);
        scheduler.AddOrUpdateRecurring<FakeJob, string>("job", "args", "0 * * * *");

        var result = await scheduler.TriggerRecurringAsync("job");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TriggerRecurringAsync_WithoutKeyedLock_NonExistent_ReturnsNotFound()
    {
        var (scheduler, _) = Build(keyedLock: null);

        var result = await scheduler.TriggerRecurringAsync("missing");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── With lock: same semantics, protected access ───────────────────────────

    [Fact]
    public async Task TriggerRecurringAsync_WithKeyedLock_ReturnsSuccess()
    {
        var (scheduler, store) = Build(new FakeKeyedLock());
        scheduler.AddOrUpdateRecurring<FakeJob, string>("job", "args", "0 * * * *");

        var result = await scheduler.TriggerRecurringAsync("job");

        result.IsSuccess.Should().BeTrue();
        store.GetJob(result.Value!).Should().NotBeNull();
    }

    [Fact]
    public async Task TriggerRecurringAsync_WithKeyedLock_NonExistent_ReturnsNotFound()
    {
        var (scheduler, _) = Build(new FakeKeyedLock());

        var result = await scheduler.TriggerRecurringAsync("ghost");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task TriggerRecurringAsync_WithKeyedLock_LockIsAcquiredAndReleased()
    {
        var trackingLock = new TrackingKeyedLock();
        var (scheduler, _) = Build(trackingLock);
        scheduler.AddOrUpdateRecurring<FakeJob, string>("my-job", "args", "0 * * * *");

        await scheduler.TriggerRecurringAsync("my-job");

        trackingLock.AcquireCount.Should().Be(1, "one trigger = one lock acquisition");
        trackingLock.ReleaseCount.Should().Be(1, "lock must always be released");
    }

    [Fact]
    public async Task TriggerRecurringAsync_WithKeyedLock_NonExistent_LockIsReleased()
    {
        var trackingLock = new TrackingKeyedLock();
        var (scheduler, _) = Build(trackingLock);

        await scheduler.TriggerRecurringAsync("missing");

        trackingLock.AcquireCount.Should().Be(1);
        trackingLock.ReleaseCount.Should().Be(1, "lock must be released even when job is not found");
    }

    // ── Lock does not bleed across different job IDs ──────────────────────────

    [Fact]
    public async Task TriggerRecurringAsync_WithKeyedLock_TwoDifferentJobIds_BothSucceed()
    {
        var (scheduler, _) = Build(new FakeKeyedLock());
        scheduler.AddOrUpdateRecurring<FakeJob, string>("job-a", "a", "0 * * * *");
        scheduler.AddOrUpdateRecurring<FakeJob, string>("job-b", "b", "0 * * * *");

        var r1 = await scheduler.TriggerRecurringAsync("job-a");
        var r2 = await scheduler.TriggerRecurringAsync("job-b");

        r1.IsSuccess.Should().BeTrue();
        r2.IsSuccess.Should().BeTrue();
        r1.Value.Should().NotBe(r2.Value, "each trigger produces a distinct one-off job ID");
    }

    // ── Concurrent triggers with lock serialize per job ID ────────────────────

    [Fact]
    public async Task TriggerRecurringAsync_WithKeyedLock_ConcurrentSameJobId_AllSucceed()
    {
        var (scheduler, store) = Build(new FakeKeyedLock());
        scheduler.AddOrUpdateRecurring<FakeJob, string>("concurrent-job", "args", "0 * * * *");

        var triggers = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            scheduler.TriggerRecurringAsync("concurrent-job")));

        triggers.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        var ids = triggers.Select(r => r.Value!).ToList();
        ids.Should().OnlyHaveUniqueItems("each concurrent trigger must produce a distinct job ID");
        ids.Should().AllSatisfy(id => store.GetJob(id).Should().NotBeNull());
    }

    // ── Constructor: null lock is accepted ────────────────────────────────────

    [Fact]
    public void Constructor_NullKeyedLock_DoesNotThrow()
    {
        var store = new InMemoryJobStore();

        var act = () => new InMemoryJobScheduler(store, null);

        act.Should().NotThrow();
    }
}

// ── Test helpers ─────────────────────────────────────────────────────────────

file sealed class FakeKeyedLock : IKeyedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct = default)
    {
        var sem = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        return new ReleaseHandle(sem);
    }

    public Task<Result<IAsyncDisposable>> TryAcquireAsync(string key, TimeSpan timeout, CancellationToken ct = default)
        => throw new NotSupportedException("not needed by these tests");
}

file sealed class TrackingKeyedLock : IKeyedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);
    private int _acquireCount;
    private int _releaseCount;

    public int AcquireCount => _acquireCount;
    public int ReleaseCount => _releaseCount;

    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct = default)
    {
        var sem = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        Interlocked.Increment(ref _acquireCount);
        return new TrackingHandle(sem, () => Interlocked.Increment(ref _releaseCount));
    }

    public Task<Result<IAsyncDisposable>> TryAcquireAsync(string key, TimeSpan timeout, CancellationToken ct = default)
        => throw new NotSupportedException("not needed by these tests");
}

file sealed class ReleaseHandle(SemaphoreSlim semaphore) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        semaphore.Release();
        return ValueTask.CompletedTask;
    }
}

file sealed class TrackingHandle(SemaphoreSlim semaphore, Action onRelease) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        semaphore.Release();
        onRelease();
        return ValueTask.CompletedTask;
    }
}
