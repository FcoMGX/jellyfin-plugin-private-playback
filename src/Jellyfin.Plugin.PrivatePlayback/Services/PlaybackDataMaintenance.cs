using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.PrivatePlayback.Services;

internal sealed class PlaybackDataMaintenance : IPlaybackDataMaintenance
{
    private readonly ILibraryManager _libraryManager;
    private readonly PolicyUserDataManager _userDataManager;
    private readonly IUserManager _userManager;

    public PlaybackDataMaintenance(
        ILibraryManager libraryManager,
        IUserManager userManager,
        PolicyUserDataManager decoratedManager)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _userDataManager = decoratedManager;
    }

    public PlaybackDataPreview Preview(Guid userId, CancellationToken cancellationToken)
    {
        var user = GetUser(userId);
        var count = 0;
        foreach (var item in GetItems())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_userDataManager.HasPersistedPlaybackData(user, item, cancellationToken))
            {
                count++;
            }
        }

        return new PlaybackDataPreview(userId, count);
    }

    public PlaybackDataCleanupResult Clear(Guid userId, CancellationToken cancellationToken)
    {
        var user = GetUser(userId);
        var count = 0;
        foreach (var item in GetItems())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_userDataManager.ClearPersistedPlaybackData(user, item, cancellationToken))
            {
                count++;
            }
        }

        return new PlaybackDataCleanupResult(userId, count);
    }

    private IReadOnlyList<BaseItem> GetItems()
        => _libraryManager.GetItemList(new InternalItemsQuery
        {
            Recursive = true,
            IsFolder = false,
            EnableTotalRecordCount = false,
            MediaTypes = [MediaType.Audio, MediaType.Video, MediaType.Book]
        });

    private Jellyfin.Database.Implementations.Entities.User GetUser(Guid userId)
        => _userManager.GetUserById(userId)
            ?? throw new KeyNotFoundException("The requested Jellyfin user does not exist.");
}
