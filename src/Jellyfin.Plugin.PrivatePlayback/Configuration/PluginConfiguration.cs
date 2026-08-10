using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PrivatePlayback.Configuration;

/// <summary>
/// Persistent Private Playback configuration.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the configuration schema version.</summary>
    public int SchemaVersion { get; set; } = ConfigurationValidator.CurrentSchemaVersion;

    /// <summary>Gets or sets the user policies.</summary>
    public UserPlaybackPolicyConfiguration[] UserPolicies { get; set; } = [];
}
