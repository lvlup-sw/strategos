using TUnit.Core;

namespace Strategos.Infrastructure.Tests;

public sealed class DependencyTests
{
    [Test]
    public async Task InfrastructureProject_HighPerformancePackages_CanResolve()
    {
        // Verify CommunityToolkit.HighPerformance is available
        var spanOwnerType = typeof(CommunityToolkit.HighPerformance.Buffers.SpanOwner<int>);
        await Assert.That(spanOwnerType).IsNotNull();

        // Verify BitFaster.Caching is available
        var concurrentLruType = typeof(BitFaster.Caching.Lru.ConcurrentLru<string, string>);
        await Assert.That(concurrentLruType).IsNotNull();

        // Verify MemoryPack is available
        var memoryPackType = typeof(MemoryPack.MemoryPackSerializer);
        await Assert.That(memoryPackType).IsNotNull();
    }
}
