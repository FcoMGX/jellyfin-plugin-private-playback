namespace Jellyfin.Plugin.PrivatePlayback.Services;

/// <summary>
/// Reports whether exact-version enforcement was installed.
/// </summary>
/// <param name="IsActive">Whether enforcement is active.</param>
/// <param name="Reason">The non-sensitive status explanation.</param>
/// <param name="ServerVersion">The detected Jellyfin server version.</param>
public sealed record EnforcementStatus(
    bool IsActive,
    string Reason,
    string ServerVersion);
