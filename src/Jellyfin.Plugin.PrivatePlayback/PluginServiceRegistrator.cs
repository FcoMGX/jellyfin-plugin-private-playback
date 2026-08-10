using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.PrivatePlayback;

/// <summary>
/// Installs the exact-version user-data decorator before Jellyfin builds its service provider.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    private const string ExpectedImplementationType = "Emby.Server.Implementations.Library.UserDataManager";
    private static readonly Version ExpectedServerVersion = new(10, 11, 11, 0);

    /// <inheritdoc />
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentNullException.ThrowIfNull(applicationHost);

        serviceCollection.AddSingleton<Policies.PolicyRegistry>();

        var serverVersion = applicationHost.ApplicationVersion;
        if (serverVersion != ExpectedServerVersion)
        {
            RegisterUnavailable(
                serviceCollection,
                serverVersion,
                $"This build requires Jellyfin {ExpectedServerVersion}; detected {serverVersion}.");
            return;
        }

        var registrations = serviceCollection
            .Where(descriptor => descriptor.ServiceType == typeof(IUserDataManager))
            .ToArray();
        if (registrations.Length != 1
            || !IsExpectedCoreRegistration(registrations[0]))
        {
            RegisterUnavailable(
                serviceCollection,
                serverVersion,
                "The exact Jellyfin core IUserDataManager registration was not found; enforcement was not installed.");
            return;
        }

        var originalRegistration = registrations[0];
        serviceCollection.Remove(originalRegistration);
        serviceCollection.AddSingleton(provider =>
            new Services.PolicyUserDataManager(
                (IUserDataManager)CreateOriginal(provider, originalRegistration),
                provider.GetRequiredService<Policies.PolicyRegistry>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Services.PolicyUserDataManager>>()));
        serviceCollection.AddSingleton<IUserDataManager>(provider =>
            provider.GetRequiredService<Services.PolicyUserDataManager>());
        serviceCollection.AddSingleton<Services.IPlaybackDataMaintenance, Services.PlaybackDataMaintenance>();
        serviceCollection.AddSingleton(new Services.EnforcementStatus(
            true,
            "The exact Jellyfin 10.11.11 user-data service is decorated.",
            serverVersion.ToString()));
    }

    private static bool IsExpectedCoreRegistration(ServiceDescriptor descriptor)
    {
        var implementationType = descriptor.ImplementationType;
        return descriptor.Lifetime == ServiceLifetime.Singleton
            && implementationType?.FullName == ExpectedImplementationType
            && implementationType.Assembly.GetName().Name == "Emby.Server.Implementations"
            && implementationType.Assembly.GetName().Version == ExpectedServerVersion;
    }

    private static object CreateOriginal(
        IServiceProvider provider,
        ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(provider);
        }

        return ActivatorUtilities.CreateInstance(
            provider,
            descriptor.ImplementationType
                ?? throw new InvalidOperationException("The core registration has no implementation type."));
    }

    private static void RegisterUnavailable(
        IServiceCollection serviceCollection,
        Version serverVersion,
        string reason)
    {
        serviceCollection.AddSingleton<Services.IPlaybackDataMaintenance, Services.UnavailablePlaybackDataMaintenance>();
        serviceCollection.AddSingleton(new Services.EnforcementStatus(
            false,
            reason,
            serverVersion.ToString()));
    }
}
