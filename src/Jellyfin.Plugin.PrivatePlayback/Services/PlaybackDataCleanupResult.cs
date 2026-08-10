namespace Jellyfin.Plugin.PrivatePlayback.Services;

/// <summary>
/// Describes the completed administrative playback-data cleanup.
/// </summary>
/// <param name="UserId">The Jellyfin user id.</param>
/// <param name="ClearedItemCount">The number of items changed.</param>
public sealed record PlaybackDataCleanupResult(Guid UserId, int ClearedItemCount);
