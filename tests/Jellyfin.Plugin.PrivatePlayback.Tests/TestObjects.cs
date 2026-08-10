using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

internal static class TestObjects
{
    public static User User(string name, long internalId)
        => new(name, "test-auth", "test-reset")
        {
            InternalId = internalId
        };

    public static Video Video()
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Test video"
        };

    public static UserItemData Data(
        long progress = 0,
        bool played = false,
        int playCount = 0,
        DateTime? lastPlayed = null)
        => new()
        {
            Key = Guid.NewGuid().ToString("N"),
            PlaybackPositionTicks = progress,
            Played = played,
            PlayCount = playCount,
            LastPlayedDate = lastPlayed
        };
}
