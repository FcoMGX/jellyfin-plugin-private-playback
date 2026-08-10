using System.Collections.Concurrent;
using System.Threading;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PrivatePlayback.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

internal sealed class FakeUserDataManager : IUserDataManager
{
    private readonly ConcurrentDictionary<(Guid UserId, Guid ItemId), UserItemData> _data = new();
    private int _saveCount;

    public event EventHandler<UserDataSaveEventArgs>? UserDataSaved;

    public int SaveCount => Volatile.Read(ref _saveCount);

    public void Seed(User user, BaseItem item, UserItemData data)
        => _data[(user.Id, item.Id)] = PolicyUserDataManager.Clone(data);

    public UserItemData Persisted(User user, BaseItem item)
        => PolicyUserDataManager.Clone(_data[(user.Id, item.Id)]);

    public void SaveUserData(
        User user,
        BaseItem item,
        UserItemData userData,
        UserDataSaveReason reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var saved = PolicyUserDataManager.Clone(userData);
        _data[(user.Id, item.Id)] = saved;
        Interlocked.Increment(ref _saveCount);
        UserDataSaved?.Invoke(this, new UserDataSaveEventArgs
        {
            Item = item,
            Keys = item.GetUserDataKeys(),
            SaveReason = reason,
            UserData = saved,
            UserId = user.Id
        });
    }

    public void SaveUserData(
        User user,
        BaseItem item,
        UpdateUserItemDataDto userDataDto,
        UserDataSaveReason reason)
    {
        var data = GetUserData(user, item);
        if (userDataDto.PlaybackPositionTicks.HasValue)
        {
            data.PlaybackPositionTicks = userDataDto.PlaybackPositionTicks.Value;
        }

        if (userDataDto.PlayCount.HasValue)
        {
            data.PlayCount = userDataDto.PlayCount.Value;
        }

        if (userDataDto.IsFavorite.HasValue)
        {
            data.IsFavorite = userDataDto.IsFavorite.Value;
        }

        if (userDataDto.Likes.HasValue)
        {
            data.Likes = userDataDto.Likes.Value;
        }

        if (userDataDto.Played.HasValue)
        {
            data.Played = userDataDto.Played.Value;
        }

        if (userDataDto.LastPlayedDate.HasValue)
        {
            data.LastPlayedDate = userDataDto.LastPlayedDate.Value;
        }

        if (userDataDto.Rating.HasValue)
        {
            data.Rating = userDataDto.Rating.Value;
        }

        SaveUserData(user, item, data, reason, CancellationToken.None);
    }

    public UserItemData GetUserData(User user, BaseItem item)
        => _data.GetOrAdd(
            (user.Id, item.Id),
            _ => new UserItemData { Key = item.Id.ToString("N") });

    public UserItemDataDto GetUserDataDto(BaseItem item, User user)
        => ToDto(GetUserData(user, item), item.Id);

    public UserItemDataDto GetUserDataDto(
        BaseItem item,
        BaseItemDto? itemDto,
        User user,
        DtoOptions options)
        => ToDto(GetUserData(user, item), item.Id);

    public bool UpdatePlayState(BaseItem item, UserItemData data, long? reportedPositionTicks)
    {
        data.PlaybackPositionTicks = reportedPositionTicks ?? 0;
        return data.Played;
    }

    private static UserItemDataDto ToDto(UserItemData data, Guid itemId)
        => new()
        {
            IsFavorite = data.IsFavorite,
            Key = data.Key,
            LastPlayedDate = data.LastPlayedDate,
            ItemId = itemId,
            PlaybackPositionTicks = data.PlaybackPositionTicks,
            PlayCount = data.PlayCount,
            Played = data.Played,
            Rating = data.Rating
        };
}
