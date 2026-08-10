namespace Jellyfin.Plugin.PrivatePlayback.Policies;

/// <summary>
/// Represents the effective persistence switches for a user.
/// </summary>
/// <param name="RememberProgress">Whether resume progress may be persisted.</param>
/// <param name="RememberWatched">Whether watched state may be persisted.</param>
/// <param name="RecordHistory">Whether play count and last-played date may be persisted.</param>
public readonly record struct PlaybackPolicy(
    bool RememberProgress,
    bool RememberWatched,
    bool RecordHistory)
{
    /// <summary>Gets a policy that preserves unmodified Jellyfin behaviour.</summary>
    public static PlaybackPolicy Normal { get; } = new(true, true, true);

    /// <summary>Gets a policy that protects every supported playback field.</summary>
    public static PlaybackPolicy FullPrivate { get; } = new(false, false, false);

    /// <summary>Gets a value indicating whether no field is protected.</summary>
    public bool IsNormal => RememberProgress && RememberWatched && RecordHistory;
}
