using System.Diagnostics;
using Jellyfin.Plugin.PrivatePlayback.Policies;
using Xunit.Abstractions;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

public sealed class PerformanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void FiveMillionPolicyLookupsDoNotAllocate()
    {
        const int iterationCount = 5_000_000;
        var userId = Guid.NewGuid();
        var registry = new PolicyRegistry();
        registry.Publish(new Dictionary<Guid, PlaybackPolicy>
        {
            [userId] = PlaybackPolicy.FullPrivate
        });
        for (var index = 0; index < 10_000; index++)
        {
            _ = registry.GetPolicy(userId);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var protectedReads = 0;
        for (var index = 0; index < iterationCount; index++)
        {
            if (!registry.GetPolicy(userId).IsNormal)
            {
                protectedReads++;
            }
        }

        stopwatch.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var nanosecondsPerLookup = stopwatch.Elapsed.TotalNanoseconds / iterationCount;
        output.WriteLine(
            "{0:N0} lookups in {1:F3} ms; {2:F1} ns/lookup; {3:N0} bytes allocated.",
            iterationCount,
            stopwatch.Elapsed.TotalMilliseconds,
            nanosecondsPerLookup,
            allocatedBytes);

        Assert.Equal(iterationCount, protectedReads);
        Assert.InRange(allocatedBytes, 0, 1024);
    }
}
