using Jellyfin.Plugin.PrivatePlayback.Configuration;
using Jellyfin.Plugin.PrivatePlayback.Policies;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

public sealed class ConfigurationValidatorTests
{
    [Fact]
    public void EmptyConfigurationProducesNormalBehaviour()
    {
        var policies = ConfigurationValidator.ValidateAndBuild(new PluginConfiguration());

        Assert.Empty(policies);
    }

    [Fact]
    public void LegacySchemaZeroMigratesToCurrentSchema()
    {
        var configuration = new PluginConfiguration { SchemaVersion = 0 };

        _ = ConfigurationValidator.ValidateAndBuild(configuration);

        Assert.Equal(ConfigurationValidator.CurrentSchemaVersion, configuration.SchemaVersion);
    }

    [Fact]
    public void FullPrivateModeIgnoresStaleIndividualSwitches()
    {
        var userId = Guid.NewGuid();
        var configuration = new PluginConfiguration
        {
            UserPolicies =
            [
                new UserPlaybackPolicyConfiguration
                {
                    UserId = userId,
                    Mode = PolicyMode.FullPrivate,
                    RememberProgress = true,
                    RememberWatched = true,
                    RecordHistory = true
                }
            ]
        };

        var policies = ConfigurationValidator.ValidateAndBuild(configuration);

        Assert.Equal(PlaybackPolicy.FullPrivate, policies[userId]);
    }

    [Fact]
    public void CustomModePreservesEverySwitch()
    {
        var userId = Guid.NewGuid();
        var configuration = new PluginConfiguration
        {
            UserPolicies =
            [
                new UserPlaybackPolicyConfiguration
                {
                    UserId = userId,
                    Mode = PolicyMode.Custom,
                    RememberProgress = false,
                    RememberWatched = true,
                    RecordHistory = false
                }
            ]
        };

        var policies = ConfigurationValidator.ValidateAndBuild(configuration);

        Assert.Equal(new PlaybackPolicy(false, true, false), policies[userId]);
    }

    [Fact]
    public void FutureSchemaFailsClosedToCaller()
    {
        var configuration = new PluginConfiguration
        {
            SchemaVersion = ConfigurationValidator.CurrentSchemaVersion + 1
        };

        Assert.Throws<InvalidDataException>(() =>
            ConfigurationValidator.ValidateAndBuild(configuration));
    }

    [Fact]
    public void DuplicateUserIdIsRejected()
    {
        var userId = Guid.NewGuid();
        var configuration = new PluginConfiguration
        {
            UserPolicies =
            [
                new UserPlaybackPolicyConfiguration { UserId = userId },
                new UserPlaybackPolicyConfiguration { UserId = userId }
            ]
        };

        Assert.Throws<InvalidDataException>(() =>
            ConfigurationValidator.ValidateAndBuild(configuration));
    }

    [Fact]
    public void EmptyUserIdIsRejected()
    {
        var configuration = new PluginConfiguration
        {
            UserPolicies = [new UserPlaybackPolicyConfiguration()]
        };

        Assert.Throws<InvalidDataException>(() =>
            ConfigurationValidator.ValidateAndBuild(configuration));
    }

    [Fact]
    public void UnknownModeIsRejected()
    {
        var configuration = new PluginConfiguration
        {
            UserPolicies =
            [
                new UserPlaybackPolicyConfiguration
                {
                    UserId = Guid.NewGuid(),
                    Mode = (PolicyMode)99
                }
            ]
        };

        Assert.Throws<InvalidDataException>(() =>
            ConfigurationValidator.ValidateAndBuild(configuration));
    }

    [Fact]
    public void ExcessiveLastKnownNameIsRejected()
    {
        var configuration = new PluginConfiguration
        {
            UserPolicies =
            [
                new UserPlaybackPolicyConfiguration
                {
                    UserId = Guid.NewGuid(),
                    LastKnownName = new string('x', 257)
                }
            ]
        };

        Assert.Throws<InvalidDataException>(() =>
            ConfigurationValidator.ValidateAndBuild(configuration));
    }

    [Fact]
    public void ExcessivePolicyCountIsRejected()
    {
        var configuration = new PluginConfiguration
        {
            UserPolicies = Enumerable.Range(0, 2049)
                .Select(_ => new UserPlaybackPolicyConfiguration { UserId = Guid.NewGuid() })
                .ToArray()
        };

        Assert.Throws<InvalidDataException>(() =>
            ConfigurationValidator.ValidateAndBuild(configuration));
    }

    [Fact]
    public void NullPolicyCollectionIsRejected()
    {
        var configuration = new PluginConfiguration { UserPolicies = null! };

        Assert.Throws<InvalidDataException>(() =>
            ConfigurationValidator.ValidateAndBuild(configuration));
    }

    [Fact]
    public void NullPolicyEntryIsRejected()
    {
        var configuration = new PluginConfiguration { UserPolicies = [null!] };

        Assert.Throws<InvalidDataException>(() =>
            ConfigurationValidator.ValidateAndBuild(configuration));
    }
}
