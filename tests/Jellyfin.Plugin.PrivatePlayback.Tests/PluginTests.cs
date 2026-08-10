using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.PrivatePlayback.Policies;
using Jellyfin.Plugin.PrivatePlayback.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

public sealed class PluginTests
{
    [Fact]
    public void PluginMetadataAndEmbeddedPagesAreComplete()
    {
        var plugin = CreatePlugin();

        Assert.Equal("Private Playback", plugin.Name);
        Assert.Contains("playback state", plugin.Description, StringComparison.Ordinal);
        Assert.Equal(Guid.Parse("bb23ffd1-026a-4598-8133-e77ae50ccad7"), plugin.Id);
        var pages = plugin.GetPages().ToArray();
        Assert.Equal(11, pages.Length);
        Assert.Equal(11, pages.Select(page => page.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Private Playback", pages[0].Name);
        Assert.Equal("Private Playback.js", pages[1].Name);
        Assert.All(pages, page => Assert.False(string.IsNullOrWhiteSpace(page.EmbeddedResourcePath)));
    }

    [Fact]
    public void DifferentServerVersionLeavesCoreServiceUntouched()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IUserDataManager, FakeUserDataManager>();
        var original = services.Single(descriptor => descriptor.ServiceType == typeof(IUserDataManager));

        new PluginServiceRegistrator().RegisterServices(
            services,
            CreateApplicationHost(new Version(10, 11, 12, 0)));

        Assert.Contains(original, services);
        var status = Assert.IsType<EnforcementStatus>(services.Single(
            descriptor => descriptor.ServiceType == typeof(EnforcementStatus)).ImplementationInstance);
        Assert.False(status.IsActive);
        Assert.Contains("requires Jellyfin 10.11.11.0", status.Reason, StringComparison.Ordinal);
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IPlaybackDataMaintenance)
            && descriptor.ImplementationType == typeof(UnavailablePlaybackDataMaintenance));
    }

    [Fact]
    public void UnexpectedExactVersionDescriptorRefusesDecoration()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IUserDataManager, FakeUserDataManager>();

        new PluginServiceRegistrator().RegisterServices(
            services,
            CreateApplicationHost(new Version(10, 11, 11, 0)));

        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IUserDataManager)
            && descriptor.ImplementationType == typeof(FakeUserDataManager));
        var status = Assert.IsType<EnforcementStatus>(services.Single(
            descriptor => descriptor.ServiceType == typeof(EnforcementStatus)).ImplementationInstance);
        Assert.False(status.IsActive);
        Assert.Contains("was not found", status.Reason, StringComparison.Ordinal);
    }

    private static Plugin CreatePlugin()
    {
        var uniquePath = Path.Combine(Path.GetTempPath(), $"private-playback-tests-{Guid.NewGuid():N}");
        var paths = CreateProxy<IApplicationPaths>((method, _) => method.Name switch
        {
            "get_PluginsPath" => uniquePath,
            "get_PluginConfigurationsPath" => uniquePath,
            _ => DefaultValue(method.ReturnType)
        });
        var serializer = CreateProxy<IXmlSerializer>((method, _) => DefaultValue(method.ReturnType));
        return new Plugin(
            paths,
            serializer,
            new PolicyRegistry(),
            NullLogger<Plugin>.Instance);
    }

    private static IServerApplicationHost CreateApplicationHost(Version version)
        => CreateProxy<IServerApplicationHost>((method, _) => method.Name switch
        {
            "get_ApplicationVersion" => version,
            _ => DefaultValue(method.ReturnType)
        });

    private static T CreateProxy<T>(Func<MethodInfo, object?[]?, object?> handler)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, TestDispatchProxy>();
        ((TestDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static object? DefaultValue(Type type)
        => type == typeof(void) || !type.IsValueType
            ? null
            : Activator.CreateInstance(type);

    [SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy generates a runtime subclass and rejects a sealed proxy base.")]
    private class TestDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);
            Assert.NotNull(Handler);
            return Handler(targetMethod, args);
        }
    }
}
