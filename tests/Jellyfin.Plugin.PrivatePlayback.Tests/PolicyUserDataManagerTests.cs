using Jellyfin.Plugin.PrivatePlayback.Policies;
using Jellyfin.Plugin.PrivatePlayback.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

public sealed class PolicyUserDataManagerTests
{
    [Fact]
    public void NormalUserUsesCoreObjectAndPersistsUnchanged()
    {
        var (manager, inner, _, user, item) = Create(PlaybackPolicy.Normal);
        var coreObject = inner.GetUserData(user, item);

        var returned = manager.GetUserData(user, item)!;
        Assert.Same(coreObject, returned);

        returned.PlaybackPositionTicks = 500;
        returned.PlayCount = 1;
        manager.SaveUserData(user, item, returned, UserDataSaveReason.PlaybackProgress, CancellationToken.None);

        Assert.Equal(1, inner.SaveCount);
        Assert.Equal(500, inner.Persisted(user, item).PlaybackPositionTicks);
    }

    [Fact]
    public void FullPrivatePreservesExistingPlaybackDataButAllowsOtherFields()
    {
        var previousDate = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var (manager, inner, _, user, item) = Create(
            PlaybackPolicy.FullPrivate,
            TestObjects.Data(100, true, 3, previousDate));
        var candidate = manager.GetUserData(user, item)!;

        candidate.PlaybackPositionTicks = 900;
        candidate.Played = false;
        candidate.PlayCount = 8;
        candidate.LastPlayedDate = previousDate.AddDays(1);
        candidate.IsFavorite = true;
        candidate.Rating = 8;
        candidate.AudioStreamIndex = 2;
        candidate.SubtitleStreamIndex = 3;
        manager.SaveUserData(user, item, candidate, UserDataSaveReason.PlaybackFinished, CancellationToken.None);

        var persisted = inner.Persisted(user, item);
        Assert.Equal(100, persisted.PlaybackPositionTicks);
        Assert.True(persisted.Played);
        Assert.Equal(3, persisted.PlayCount);
        Assert.Equal(previousDate, persisted.LastPlayedDate);
        Assert.True(persisted.IsFavorite);
        Assert.Equal(8, persisted.Rating);
        Assert.Equal(2, persisted.AudioStreamIndex);
        Assert.Equal(3, persisted.SubtitleStreamIndex);
        Assert.Equal(1, inner.SaveCount);
    }

    [Fact]
    public void FullPrivateSkipsWriteWhenOnlyPlaybackFieldsChanged()
    {
        var (manager, inner, _, user, item) = Create(PlaybackPolicy.FullPrivate);
        var candidate = manager.GetUserData(user, item)!;
        candidate.PlaybackPositionTicks = 600;
        candidate.Played = true;
        candidate.PlayCount = 1;
        candidate.LastPlayedDate = DateTime.UtcNow;

        manager.SaveUserData(user, item, candidate, UserDataSaveReason.PlaybackFinished, CancellationToken.None);

        Assert.Equal(0, inner.SaveCount);
        Assert.False(inner.Persisted(user, item).Played);
    }

    [Fact]
    public void CustomPolicyProtectsProgressAndHistoryWhileAllowingWatchedState()
    {
        var previousDate = new DateTime(2025, 9, 8, 7, 6, 5, DateTimeKind.Utc);
        var policy = new PlaybackPolicy(false, true, false);
        var (manager, inner, _, user, item) = Create(
            policy,
            TestObjects.Data(250, false, 4, previousDate));
        var candidate = manager.GetUserData(user, item)!;
        candidate.PlaybackPositionTicks = 999;
        candidate.Played = true;
        candidate.PlayCount = 9;
        candidate.LastPlayedDate = previousDate.AddDays(2);

        manager.SaveUserData(user, item, candidate, UserDataSaveReason.TogglePlayed, CancellationToken.None);

        var persisted = inner.Persisted(user, item);
        Assert.Equal(250, persisted.PlaybackPositionTicks);
        Assert.True(persisted.Played);
        Assert.Equal(4, persisted.PlayCount);
        Assert.Equal(previousDate, persisted.LastPlayedDate);
    }

    [Fact]
    public void CustomPolicyCanAllowProgressButProtectWatchedState()
    {
        var policy = new PlaybackPolicy(true, false, true);
        var (manager, inner, _, user, item) = Create(policy);
        var candidate = manager.GetUserData(user, item)!;
        candidate.PlaybackPositionTicks = 777;
        candidate.Played = true;
        candidate.PlayCount = 1;
        candidate.LastPlayedDate = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);

        manager.SaveUserData(user, item, candidate, UserDataSaveReason.PlaybackProgress, CancellationToken.None);

        var persisted = inner.Persisted(user, item);
        Assert.Equal(777, persisted.PlaybackPositionTicks);
        Assert.False(persisted.Played);
        Assert.Equal(1, persisted.PlayCount);
        Assert.NotNull(persisted.LastPlayedDate);
    }

    [Fact]
    public void DirectUserDataDtoCannotBypassFullPrivatePolicy()
    {
        var previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var (manager, inner, _, user, item) = Create(
            PlaybackPolicy.FullPrivate,
            TestObjects.Data(55, false, 2, previousDate));

        manager.SaveUserData(
            user,
            item,
            new UpdateUserItemDataDto
            {
                PlaybackPositionTicks = 999,
                Played = true,
                PlayCount = 10,
                LastPlayedDate = previousDate.AddYears(1),
                IsFavorite = true,
                Rating = 9
            },
            UserDataSaveReason.UpdateUserData);

        var persisted = inner.Persisted(user, item);
        Assert.Equal(55, persisted.PlaybackPositionTicks);
        Assert.False(persisted.Played);
        Assert.Equal(2, persisted.PlayCount);
        Assert.Equal(previousDate, persisted.LastPlayedDate);
        Assert.True(persisted.IsFavorite);
        Assert.Equal(9, persisted.Rating);
    }

    [Fact]
    public void ProtectedUserDoesNotAffectNormalUser()
    {
        var protectedUser = TestObjects.User("private", 1);
        var normalUser = TestObjects.User("normal", 2);
        var item = TestObjects.Video();
        var inner = new FakeUserDataManager();
        inner.Seed(protectedUser, item, TestObjects.Data());
        inner.Seed(normalUser, item, TestObjects.Data());
        var registry = new PolicyRegistry();
        registry.Publish(new Dictionary<Guid, PlaybackPolicy>
        {
            [protectedUser.Id] = PlaybackPolicy.FullPrivate
        });
        var manager = new PolicyUserDataManager(
            inner,
            registry,
            NullLogger<PolicyUserDataManager>.Instance);

        var protectedData = manager.GetUserData(protectedUser, item)!;
        protectedData.Played = true;
        manager.SaveUserData(
            protectedUser,
            item,
            protectedData,
            UserDataSaveReason.TogglePlayed,
            CancellationToken.None);
        var normalData = manager.GetUserData(normalUser, item)!;
        normalData.Played = true;
        manager.SaveUserData(
            normalUser,
            item,
            normalData,
            UserDataSaveReason.TogglePlayed,
            CancellationToken.None);

        Assert.False(inner.Persisted(protectedUser, item).Played);
        Assert.True(inner.Persisted(normalUser, item).Played);
    }

    [Fact]
    public async Task ConcurrentProtectedPlaybackWritesRemainBlocked()
    {
        var (manager, inner, _, user, item) = Create(PlaybackPolicy.FullPrivate);
        var writes = Enumerable.Range(1, 128).Select(index => Task.Run(() =>
        {
            var data = manager.GetUserData(user, item)!;
            data.PlaybackPositionTicks = index * 100;
            data.Played = true;
            data.PlayCount = index;
            data.LastPlayedDate = DateTime.UtcNow.AddSeconds(index);
            manager.SaveUserData(
                user,
                item,
                data,
                UserDataSaveReason.PlaybackProgress,
                CancellationToken.None);
        }));

        await Task.WhenAll(writes);

        var persisted = inner.Persisted(user, item);
        Assert.Equal(0, inner.SaveCount);
        Assert.Equal(0, persisted.PlaybackPositionTicks);
        Assert.False(persisted.Played);
        Assert.Equal(0, persisted.PlayCount);
        Assert.Null(persisted.LastPlayedDate);
    }

    [Fact]
    public void PlaybackStateCalculationUsesCloneAndCannotMutateCoreBaseline()
    {
        var (manager, inner, _, user, item) = Create(PlaybackPolicy.FullPrivate);
        var data = manager.GetUserData(user, item)!;

        manager.UpdatePlayState(item, data, 321);

        Assert.Equal(321, data.PlaybackPositionTicks);
        Assert.Equal(0, inner.Persisted(user, item).PlaybackPositionTicks);
    }

    [Fact]
    public void PolicyChangeDuringSessionAppliesToTheNextProgressWrite()
    {
        var (manager, inner, registry, user, item) = Create(PlaybackPolicy.Normal);
        var initialProgress = manager.GetUserData(user, item)!;
        initialProgress.PlaybackPositionTicks = 100;
        manager.SaveUserData(
            user,
            item,
            initialProgress,
            UserDataSaveReason.PlaybackProgress,
            CancellationToken.None);

        registry.Publish(new Dictionary<Guid, PlaybackPolicy>
        {
            [user.Id] = PlaybackPolicy.FullPrivate
        });
        var laterProgress = manager.GetUserData(user, item)!;
        laterProgress.PlaybackPositionTicks = 900;
        laterProgress.Played = true;
        manager.SaveUserData(
            user,
            item,
            laterProgress,
            UserDataSaveReason.PlaybackProgress,
            CancellationToken.None);

        var persisted = inner.Persisted(user, item);
        Assert.Equal(100, persisted.PlaybackPositionTicks);
        Assert.False(persisted.Played);
        Assert.Equal(1, inner.SaveCount);
    }

    [Fact]
    public void ExplicitMaintenanceCleanupIsSafeAndIdempotent()
    {
        var previousDate = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var initial = TestObjects.Data(123, true, 4, previousDate);
        initial.IsFavorite = true;
        initial.Rating = 8;
        initial.AudioStreamIndex = 2;
        initial.SubtitleStreamIndex = 3;
        var (manager, inner, _, user, item) = Create(PlaybackPolicy.FullPrivate, initial);

        Assert.True(manager.HasPersistedPlaybackData(user, item, CancellationToken.None));
        Assert.True(manager.ClearPersistedPlaybackData(user, item, CancellationToken.None));
        Assert.False(manager.ClearPersistedPlaybackData(user, item, CancellationToken.None));
        Assert.False(manager.HasPersistedPlaybackData(user, item, CancellationToken.None));

        var persisted = inner.Persisted(user, item);
        Assert.Equal(0, persisted.PlaybackPositionTicks);
        Assert.False(persisted.Played);
        Assert.Equal(0, persisted.PlayCount);
        Assert.Null(persisted.LastPlayedDate);
        Assert.True(persisted.IsFavorite);
        Assert.Equal(8, persisted.Rating);
        Assert.Equal(2, persisted.AudioStreamIndex);
        Assert.Equal(3, persisted.SubtitleStreamIndex);
    }

    [Fact]
    public void MaintenanceHonoursCancellationBeforeAccessingUserData()
    {
        var (manager, _, _, user, item) = Create(PlaybackPolicy.FullPrivate);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            manager.HasPersistedPlaybackData(user, item, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() =>
            manager.ClearPersistedPlaybackData(user, item, cancellation.Token));
    }

    private static (
        PolicyUserDataManager Manager,
        FakeUserDataManager Inner,
        PolicyRegistry Registry,
        Jellyfin.Database.Implementations.Entities.User User,
        Video Item) Create(
            PlaybackPolicy policy,
            UserItemData? initial = null)
    {
        var user = TestObjects.User("test", 1);
        var item = TestObjects.Video();
        var inner = new FakeUserDataManager();
        inner.Seed(user, item, initial ?? TestObjects.Data());
        var registry = new PolicyRegistry();
        if (!policy.IsNormal)
        {
            registry.Publish(new Dictionary<Guid, PlaybackPolicy>
            {
                [user.Id] = policy
            });
        }

        return (
            new PolicyUserDataManager(
                inner,
                registry,
                NullLogger<PolicyUserDataManager>.Instance),
            inner,
            registry,
            user,
            item);
    }
}
