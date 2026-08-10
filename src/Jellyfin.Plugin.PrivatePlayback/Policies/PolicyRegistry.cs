namespace Jellyfin.Plugin.PrivatePlayback.Policies;

/// <summary>
/// Publishes immutable-by-convention policy snapshots for the playback hot path.
/// </summary>
public sealed class PolicyRegistry
{
    private IReadOnlyDictionary<Guid, PlaybackPolicy> _snapshot =
        new Dictionary<Guid, PlaybackPolicy>();

    /// <summary>Gets the effective policy for a user.</summary>
    /// <param name="userId">The immutable Jellyfin user id.</param>
    /// <returns>The configured policy or normal behaviour.</returns>
    public PlaybackPolicy GetPolicy(Guid userId)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot.TryGetValue(userId, out var policy)
            ? policy
            : PlaybackPolicy.Normal;
    }

    /// <summary>Atomically publishes a validated policy snapshot.</summary>
    /// <param name="policies">The validated policies.</param>
    public void Publish(IReadOnlyDictionary<Guid, PlaybackPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var copy = new Dictionary<Guid, PlaybackPolicy>(policies);
        Volatile.Write(ref _snapshot, copy);
    }

    /// <summary>Atomically restores the fail-safe normal-behaviour snapshot.</summary>
    public void PublishSafeDefault()
        => Volatile.Write(ref _snapshot, new Dictionary<Guid, PlaybackPolicy>());
}
