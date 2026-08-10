namespace Jellyfin.Plugin.PrivatePlayback.Services;

/// <summary>
/// Provides explicit administrative inspection and cleanup of existing playback data.
/// </summary>
public interface IPlaybackDataMaintenance
{
    /// <summary>Counts items containing playback data for a user.</summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The preview result.</returns>
    PlaybackDataPreview Preview(Guid userId, CancellationToken cancellationToken);

    /// <summary>Clears supported playback data for a user.</summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cleanup result.</returns>
    PlaybackDataCleanupResult Clear(Guid userId, CancellationToken cancellationToken);
}
