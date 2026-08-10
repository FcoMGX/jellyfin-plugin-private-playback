namespace Jellyfin.Plugin.PrivatePlayback.Api;

/// <summary>
/// Carries the explicit confirmation required for destructive playback-data cleanup.
/// </summary>
/// <param name="Confirmation">The exact confirmation phrase.</param>
public sealed record PlaybackDataCleanupRequest(string Confirmation);
