namespace Jellyfin.Plugin.PrivatePlayback.Services;

internal sealed class UnavailablePlaybackDataMaintenance : IPlaybackDataMaintenance
{
    public PlaybackDataPreview Preview(Guid userId, CancellationToken cancellationToken)
        => throw new NotSupportedException("Private Playback enforcement is not active.");

    public PlaybackDataCleanupResult Clear(Guid userId, CancellationToken cancellationToken)
        => throw new NotSupportedException("Private Playback enforcement is not active.");
}
