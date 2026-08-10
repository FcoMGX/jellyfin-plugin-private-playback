using System.Threading;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PrivatePlayback.Policies;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PrivatePlayback.Services;

internal sealed class PolicyUserDataManager : IUserDataManager
{
    private const int LockStripeCount = 512;
    private readonly IUserDataManager _inner;
    private readonly ILogger<PolicyUserDataManager> _logger;
    private readonly PolicyRegistry _policies;
    private readonly object[] _lockStripes;

    public PolicyUserDataManager(
        IUserDataManager inner,
        PolicyRegistry policies,
        ILogger<PolicyUserDataManager> logger)
    {
        _inner = inner;
        _policies = policies;
        _logger = logger;
        _lockStripes = new object[LockStripeCount];
        for (var index = 0; index < _lockStripes.Length; index++)
        {
            _lockStripes[index] = new object();
        }
    }

    public event EventHandler<UserDataSaveEventArgs>? UserDataSaved
    {
        add => _inner.UserDataSaved += value;
        remove => _inner.UserDataSaved -= value;
    }

    public void SaveUserData(
        User user,
        BaseItem item,
        UserItemData userData,
        UserDataSaveReason reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(userData);

        var policy = GetPolicy(user.Id);
        if (policy.IsNormal)
        {
            _inner.SaveUserData(user, item, userData, reason, cancellationToken);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (GetLock(user.Id, item.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var baseline = _inner.GetUserData(user, item);
            if (baseline is null)
            {
                _logger.LogWarning("Jellyfin returned no user data baseline; normal save behaviour will be used.");
                _inner.SaveUserData(user, item, userData, reason, cancellationToken);
                return;
            }

            var filtered = Filter(Clone(userData), baseline, policy);
            if (!PersistedValuesEqual(filtered, baseline))
            {
                _inner.SaveUserData(user, item, filtered, reason, cancellationToken);
            }
        }
    }

    public void SaveUserData(
        User user,
        BaseItem item,
        UpdateUserItemDataDto userDataDto,
        UserDataSaveReason reason)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(userDataDto);

        var policy = GetPolicy(user.Id);
        if (policy.IsNormal)
        {
            _inner.SaveUserData(user, item, userDataDto, reason);
            return;
        }

        lock (GetLock(user.Id, item.Id))
        {
            var baseline = _inner.GetUserData(user, item);
            if (baseline is null)
            {
                _logger.LogWarning("Jellyfin returned no user data baseline; normal DTO save behaviour will be used.");
                _inner.SaveUserData(user, item, userDataDto, reason);
                return;
            }

            var candidate = Clone(baseline);
            Apply(userDataDto, candidate);
            var filtered = Filter(candidate, baseline, policy);
            if (!PersistedValuesEqual(filtered, baseline))
            {
                _inner.SaveUserData(user, item, filtered, reason, CancellationToken.None);
            }
        }
    }

    public UserItemData? GetUserData(User user, BaseItem item)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(item);

        var data = _inner.GetUserData(user, item);
        return data is null || GetPolicy(user.Id).IsNormal
            ? data
            : Clone(data);
    }

    public UserItemDataDto? GetUserDataDto(BaseItem item, User user)
        => _inner.GetUserDataDto(item, user);

    public UserItemDataDto? GetUserDataDto(
        BaseItem item,
        BaseItemDto? itemDto,
        User user,
        DtoOptions options)
        => _inner.GetUserDataDto(item, itemDto, user, options);

    public bool UpdatePlayState(BaseItem item, UserItemData data, long? reportedPositionTicks)
        => _inner.UpdatePlayState(item, data, reportedPositionTicks);

    internal bool HasPersistedPlaybackData(
        User user,
        BaseItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (GetLock(user.Id, item.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = _inner.GetUserData(user, item);
            return existing is not null && ContainsPlaybackData(existing);
        }
    }

    internal bool ClearPersistedPlaybackData(
        User user,
        BaseItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (GetLock(user.Id, item.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = _inner.GetUserData(user, item);
            if (existing is null || !ContainsPlaybackData(existing))
            {
                return false;
            }

            var cleared = Clone(existing);
            cleared.PlaybackPositionTicks = 0;
            cleared.PlayCount = 0;
            cleared.LastPlayedDate = null;
            cleared.Played = false;
            _inner.SaveUserData(
                user,
                item,
                cleared,
                UserDataSaveReason.UpdateUserData,
                cancellationToken);
            return true;
        }
    }

    internal static UserItemData Clone(UserItemData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new UserItemData
        {
            Key = source.Key,
            AudioStreamIndex = source.AudioStreamIndex,
            IsFavorite = source.IsFavorite,
            LastPlayedDate = source.LastPlayedDate,
            PlaybackPositionTicks = source.PlaybackPositionTicks,
            PlayCount = source.PlayCount,
            Played = source.Played,
            Rating = source.Rating,
            SubtitleStreamIndex = source.SubtitleStreamIndex
        };
    }

    internal static bool PersistedValuesEqual(UserItemData left, UserItemData right)
        => left.AudioStreamIndex == right.AudioStreamIndex
            && left.IsFavorite == right.IsFavorite
            && left.LastPlayedDate == right.LastPlayedDate
            && left.PlaybackPositionTicks == right.PlaybackPositionTicks
            && left.PlayCount == right.PlayCount
            && left.Played == right.Played
            && left.Rating == right.Rating
            && left.SubtitleStreamIndex == right.SubtitleStreamIndex;

    private static bool ContainsPlaybackData(UserItemData data)
        => data.PlaybackPositionTicks != 0
            || data.PlayCount != 0
            || data.LastPlayedDate.HasValue
            || data.Played;

    private static void Apply(UpdateUserItemDataDto source, UserItemData target)
    {
        if (source.PlaybackPositionTicks.HasValue)
        {
            target.PlaybackPositionTicks = source.PlaybackPositionTicks.Value;
        }

        if (source.PlayCount.HasValue)
        {
            target.PlayCount = source.PlayCount.Value;
        }

        if (source.IsFavorite.HasValue)
        {
            target.IsFavorite = source.IsFavorite.Value;
        }

        if (source.Likes.HasValue)
        {
            target.Likes = source.Likes.Value;
        }

        if (source.Played.HasValue)
        {
            target.Played = source.Played.Value;
        }

        if (source.LastPlayedDate.HasValue)
        {
            target.LastPlayedDate = source.LastPlayedDate.Value;
        }

        if (source.Rating.HasValue)
        {
            target.Rating = source.Rating.Value;
        }
    }

    private static UserItemData Filter(
        UserItemData candidate,
        UserItemData baseline,
        PlaybackPolicy policy)
    {
        if (!policy.RememberProgress)
        {
            candidate.PlaybackPositionTicks = baseline.PlaybackPositionTicks;
        }

        if (!policy.RememberWatched)
        {
            candidate.Played = baseline.Played;
        }

        if (!policy.RecordHistory)
        {
            candidate.PlayCount = baseline.PlayCount;
            candidate.LastPlayedDate = baseline.LastPlayedDate;
        }

        return candidate;
    }

    private PlaybackPolicy GetPolicy(Guid userId)
    {
        try
        {
            return _policies.GetPolicy(userId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Private Playback policy lookup failed; normal Jellyfin behavior will be used.");
            return PlaybackPolicy.Normal;
        }
    }

    private object GetLock(Guid userId, Guid itemId)
    {
        var hash = HashCode.Combine(userId, itemId) & int.MaxValue;
        return _lockStripes[hash % _lockStripes.Length];
    }
}
