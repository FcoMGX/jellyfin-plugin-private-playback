using Jellyfin.Plugin.PrivatePlayback.Policies;

namespace Jellyfin.Plugin.PrivatePlayback.Configuration;

internal static class ConfigurationValidator
{
    public const int CurrentSchemaVersion = 1;
    private const int MaximumUserPolicies = 2048;
    private const int MaximumLastKnownNameLength = 256;

    public static IReadOnlyDictionary<Guid, PlaybackPolicy> ValidateAndBuild(PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.SchemaVersion == 0)
        {
            configuration.SchemaVersion = CurrentSchemaVersion;
        }

        if (configuration.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported configuration schema {configuration.SchemaVersion}.");
        }

        var entries = configuration.UserPolicies
            ?? throw new InvalidDataException("UserPolicies cannot be null.");
        if (entries.Length > MaximumUserPolicies)
        {
            throw new InvalidDataException("The configuration contains too many user policies.");
        }

        var result = new Dictionary<Guid, PlaybackPolicy>(entries.Length);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new InvalidDataException("A user policy cannot be null.");
            }

            if (entry.UserId == Guid.Empty)
            {
                throw new InvalidDataException("A user policy has an empty user id.");
            }

            if (entry.LastKnownName?.Length > MaximumLastKnownNameLength)
            {
                throw new InvalidDataException("A last-known user name is too long.");
            }

            if (!Enum.IsDefined(entry.Mode))
            {
                throw new InvalidDataException("A user policy has an unknown mode.");
            }

            var policy = entry.Mode switch
            {
                PolicyMode.Normal => PlaybackPolicy.Normal,
                PolicyMode.FullPrivate => PlaybackPolicy.FullPrivate,
                PolicyMode.Custom => new PlaybackPolicy(
                    entry.RememberProgress,
                    entry.RememberWatched,
                    entry.RecordHistory),
                _ => throw new InvalidDataException("A user policy has an unknown mode.")
            };

            if (!result.TryAdd(entry.UserId, policy))
            {
                throw new InvalidDataException("The configuration contains a duplicate user id.");
            }
        }

        return result;
    }
}
