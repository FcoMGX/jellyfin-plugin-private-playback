using Jellyfin.Plugin.PrivatePlayback.Configuration;
using Jellyfin.Plugin.PrivatePlayback.Policies;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PrivatePlayback;

/// <summary>
/// Private Playback plugin entry point.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;
    private readonly PolicyRegistry _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="xmlSerializer">Jellyfin XML serializer.</param>
    /// <param name="policies">The live policy registry.</param>
    /// <param name="logger">The plugin logger.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        PolicyRegistry policies,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _policies = policies;
        _logger = logger;
        Instance = this;
        LoadPolicySnapshot(xmlSerializer);
    }

    /// <summary>Gets the active plugin instance.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Private Playback";

    /// <inheritdoc />
    public override string Description
        => "Prevents selected users from persisting configured Jellyfin playback state.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("bb23ffd1-026a-4598-8133-e77ae50ccad7");

    /// <inheritdoc />
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        if (configuration is not PluginConfiguration typedConfiguration)
        {
            throw new ArgumentException("The configuration has the wrong type.", nameof(configuration));
        }

        var snapshot = ConfigurationValidator.ValidateAndBuild(typedConfiguration);
        base.UpdateConfiguration(typedConfiguration);
        _policies.Publish(snapshot);
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        const string resourcePrefix = "Jellyfin.Plugin.PrivatePlayback";
        yield return new PluginPageInfo
        {
            Name = Name,
            DisplayName = Name,
            EmbeddedResourcePath = resourcePrefix + ".Configuration.Web.config.html"
        };
        yield return new PluginPageInfo
        {
            Name = Name + ".js",
            EmbeddedResourcePath = resourcePrefix + ".Configuration.Web.config.js"
        };

        foreach (var culture in SupportedCultures.All)
        {
            yield return new PluginPageInfo
            {
                Name = SupportedCultures.ResourceName(culture),
                EmbeddedResourcePath = resourcePrefix + ".Localization.resources." + culture + ".json"
            };
        }
    }

    private void LoadPolicySnapshot(IXmlSerializer xmlSerializer)
    {
        try
        {
            PluginConfiguration configuration;
            if (File.Exists(ConfigurationFilePath))
            {
                configuration = (PluginConfiguration)xmlSerializer.DeserializeFromFile(
                    typeof(PluginConfiguration),
                    ConfigurationFilePath);
            }
            else
            {
                configuration = new PluginConfiguration();
            }

            var originalSchema = configuration.SchemaVersion;
            var snapshot = ConfigurationValidator.ValidateAndBuild(configuration);
            Configuration = configuration;
            _policies.Publish(snapshot);
            if (originalSchema != configuration.SchemaVersion)
            {
                SaveConfiguration(configuration);
            }
        }
        catch (Exception exception)
        {
            Configuration = new PluginConfiguration();
            _policies.PublishSafeDefault();
            _logger.LogError(
                exception,
                "Private Playback configuration is invalid; all users will retain normal Jellyfin behavior.");
        }
    }
}
