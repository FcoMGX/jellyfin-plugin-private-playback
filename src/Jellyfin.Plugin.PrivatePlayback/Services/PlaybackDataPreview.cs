namespace Jellyfin.Plugin.PrivatePlayback.Services;

/// <summary>
/// Describes playback data that an administrative cleanup would affect.
/// </summary>
/// <param name="UserId">The Jellyfin user id.</param>
/// <param name="AffectedItemCount">The number of affected items.</param>
public sealed record PlaybackDataPreview(Guid UserId, int AffectedItemCount);
