using System.Net.Mime;
using Jellyfin.Plugin.PrivatePlayback.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PrivatePlayback.Api;

/// <summary>
/// Exposes elevated administrative status and playback-data cleanup endpoints.
/// </summary>
[ApiController]
[Authorize(Policy = MediaBrowser.Common.Api.Policies.RequiresElevation)]
[Route("PrivatePlayback")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class PrivatePlaybackController : ControllerBase
{
    internal const string CleanupConfirmation = "CLEAR_PLAYBACK_DATA";
    private readonly EnforcementStatus _status;
    private readonly IPlaybackDataMaintenance _maintenance;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivatePlaybackController"/> class.
    /// </summary>
    /// <param name="status">The exact-version enforcement status.</param>
    /// <param name="maintenance">The playback-data maintenance service.</param>
    public PrivatePlaybackController(
        EnforcementStatus status,
        IPlaybackDataMaintenance maintenance)
    {
        _status = status;
        _maintenance = maintenance;
    }

    /// <summary>Gets the current enforcement status.</summary>
    /// <returns>The enforcement status.</returns>
    [HttpGet("Status")]
    [ProducesResponseType<EnforcementStatus>(StatusCodes.Status200OK)]
    public ActionResult<EnforcementStatus> GetStatus() => Ok(_status);

    /// <summary>Previews an explicit playback-data cleanup.</summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of items that contain supported playback data.</returns>
    [HttpGet("Users/{userId:guid}/PlaybackData/Preview")]
    [ProducesResponseType<PlaybackDataPreview>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<PlaybackDataPreview> Preview(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        if (!_status.IsActive)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Private Playback enforcement is not active."
            });
        }

        try
        {
            return Ok(_maintenance.Preview(userId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Clears existing supported playback data after explicit confirmation.</summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="request">The destructive-operation confirmation.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of items changed.</returns>
    [HttpPost("Users/{userId:guid}/PlaybackData/Clear")]
    [ProducesResponseType<PlaybackDataCleanupResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<PlaybackDataCleanupResult> Clear(
        [FromRoute] Guid userId,
        [FromBody] PlaybackDataCleanupRequest request,
        CancellationToken cancellationToken)
    {
        if (!_status.IsActive)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Private Playback enforcement is not active."
            });
        }

        if (!string.Equals(
                request.Confirmation,
                CleanupConfirmation,
                StringComparison.Ordinal))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "The destructive cleanup confirmation is invalid."
            });
        }

        try
        {
            return Ok(_maintenance.Clear(userId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
