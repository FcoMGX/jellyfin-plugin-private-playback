using Jellyfin.Plugin.PrivatePlayback.Policies;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

public sealed class PolicyRegistryTests
{
    [Fact]
    public async Task ConcurrentReadsObserveOnlyCompleteSnapshots()
    {
        var userId = Guid.NewGuid();
        var registry = new PolicyRegistry();
        var invalidObservation = 0;
        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var index = 0; index < 10_000; index++)
            {
                var policy = registry.GetPolicy(userId);
                if (policy != PlaybackPolicy.Normal && policy != PlaybackPolicy.FullPrivate)
                {
                    Interlocked.Increment(ref invalidObservation);
                }
            }
        }));
        var writer = Task.Run(() =>
        {
            for (var index = 0; index < 1_000; index++)
            {
                registry.Publish(new Dictionary<Guid, PlaybackPolicy>
                {
                    [userId] = PlaybackPolicy.FullPrivate
                });
                registry.PublishSafeDefault();
            }
        });

        await Task.WhenAll(readers.Append(writer));

        Assert.Equal(0, invalidObservation);
    }
}
