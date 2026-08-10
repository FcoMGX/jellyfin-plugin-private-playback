namespace Jellyfin.Plugin.PrivatePlayback.Configuration;

/// <summary>
/// Selects how Jellyfin playback data is persisted for a user.
/// </summary>
public enum PolicyMode
{
    /// <summary>Use unmodified Jellyfin behaviour.</summary>
    Normal = 0,

    /// <summary>Protect every supported playback field.</summary>
    FullPrivate = 1,

    /// <summary>Apply the individual field switches.</summary>
    Custom = 2
}
