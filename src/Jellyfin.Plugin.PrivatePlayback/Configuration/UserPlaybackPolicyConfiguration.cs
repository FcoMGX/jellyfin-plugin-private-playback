namespace Jellyfin.Plugin.PrivatePlayback.Configuration;

/// <summary>
/// Stores the administrator-selected playback policy for one Jellyfin user id.
/// </summary>
public sealed class UserPlaybackPolicyConfiguration
{
    /// <summary>Gets or sets the immutable Jellyfin user id.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets a display-only copy of the last known user name.</summary>
    public string LastKnownName { get; set; } = string.Empty;

    /// <summary>Gets or sets the policy mode.</summary>
    public PolicyMode Mode { get; set; }

    /// <summary>Gets or sets a value indicating whether resume progress may be persisted.</summary>
    public bool RememberProgress { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether watched state may be persisted.</summary>
    public bool RememberWatched { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether play count and last-played date may be persisted.</summary>
    public bool RecordHistory { get; set; } = true;
}
