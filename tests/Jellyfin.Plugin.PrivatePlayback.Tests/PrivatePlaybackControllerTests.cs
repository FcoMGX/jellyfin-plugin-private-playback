using System.Reflection;
using Jellyfin.Plugin.PrivatePlayback.Api;
using Jellyfin.Plugin.PrivatePlayback.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

public sealed class PrivatePlaybackControllerTests
{
    [Fact]
    public void ControllerRequiresJellyfinElevationPolicy()
    {
        var attribute = typeof(PrivatePlaybackController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(MediaBrowser.Common.Api.Policies.RequiresElevation, attribute.Policy);
    }

    [Fact]
    public void StatusReturnsExactEnforcementState()
    {
        var status = new EnforcementStatus(true, "active", "10.11.11.0");
        var controller = new PrivatePlaybackController(status, new FakeMaintenance());

        var result = Assert.IsType<OkObjectResult>(controller.GetStatus().Result);

        Assert.Same(status, result.Value);
    }

    [Fact]
    public void InactiveEnforcementRejectsMaintenance()
    {
        var controller = new PrivatePlaybackController(
            new EnforcementStatus(false, "unavailable", "10.11.12.0"),
            new FakeMaintenance());

        Assert.IsType<ConflictObjectResult>(controller.Preview(Guid.NewGuid(), CancellationToken.None).Result);
        Assert.IsType<ConflictObjectResult>(controller.Clear(
            Guid.NewGuid(),
            new PlaybackDataCleanupRequest(PrivatePlaybackController.CleanupConfirmation),
            CancellationToken.None).Result);
    }

    [Fact]
    public void CleanupRequiresTheExactConfirmation()
    {
        var maintenance = new FakeMaintenance();
        var controller = ActiveController(maintenance);

        var result = controller.Clear(
            Guid.NewGuid(),
            new PlaybackDataCleanupRequest("clear"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, maintenance.ClearCalls);
    }

    [Fact]
    public void PreviewAndCleanupReturnServiceResults()
    {
        var userId = Guid.NewGuid();
        var maintenance = new FakeMaintenance
        {
            PreviewResult = new PlaybackDataPreview(userId, 7),
            ClearResult = new PlaybackDataCleanupResult(userId, 6)
        };
        var controller = ActiveController(maintenance);

        var preview = Assert.IsType<OkObjectResult>(
            controller.Preview(userId, CancellationToken.None).Result);
        var clear = Assert.IsType<OkObjectResult>(
            controller.Clear(
                userId,
                new PlaybackDataCleanupRequest(PrivatePlaybackController.CleanupConfirmation),
                CancellationToken.None).Result);

        Assert.Equal(maintenance.PreviewResult, preview.Value);
        Assert.Equal(maintenance.ClearResult, clear.Value);
        Assert.Equal(userId, maintenance.LastUserId);
        Assert.Equal(1, maintenance.PreviewCalls);
        Assert.Equal(1, maintenance.ClearCalls);
    }

    [Fact]
    public void UnknownUsersReturnNotFound()
    {
        var maintenance = new FakeMaintenance { ThrowMissingUser = true };
        var controller = ActiveController(maintenance);

        Assert.IsType<NotFoundResult>(
            controller.Preview(Guid.NewGuid(), CancellationToken.None).Result);
        Assert.IsType<NotFoundResult>(
            controller.Clear(
                Guid.NewGuid(),
                new PlaybackDataCleanupRequest(PrivatePlaybackController.CleanupConfirmation),
                CancellationToken.None).Result);
    }

    private static PrivatePlaybackController ActiveController(IPlaybackDataMaintenance maintenance)
        => new(new EnforcementStatus(true, "active", "10.11.11.0"), maintenance);

    private sealed class FakeMaintenance : IPlaybackDataMaintenance
    {
        public PlaybackDataPreview PreviewResult { get; set; } = new(Guid.Empty, 0);

        public PlaybackDataCleanupResult ClearResult { get; set; } = new(Guid.Empty, 0);

        public bool ThrowMissingUser { get; set; }

        public Guid LastUserId { get; private set; }

        public int PreviewCalls { get; private set; }

        public int ClearCalls { get; private set; }

        public PlaybackDataPreview Preview(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreviewCalls++;
            LastUserId = userId;
            if (ThrowMissingUser)
            {
                throw new KeyNotFoundException();
            }

            return PreviewResult;
        }

        public PlaybackDataCleanupResult Clear(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearCalls++;
            LastUserId = userId;
            if (ThrowMissingUser)
            {
                throw new KeyNotFoundException();
            }

            return ClearResult;
        }
    }
}
