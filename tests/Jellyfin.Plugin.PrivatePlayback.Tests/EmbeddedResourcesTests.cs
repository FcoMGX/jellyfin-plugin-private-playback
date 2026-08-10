using System.Text.Json;

namespace Jellyfin.Plugin.PrivatePlayback.Tests;

public sealed class EmbeddedResourcesTests
{
    private static readonly string[] Cultures = SupportedCultures.All.ToArray();

    [Fact]
    public void EveryLocaleHasTheSameNonEmptyKeys()
    {
        var assembly = typeof(Plugin).Assembly;
        HashSet<string>? expectedKeys = null;
        foreach (var culture in Cultures)
        {
            var resourceName = $"Jellyfin.Plugin.PrivatePlayback.Localization.resources.{culture}.json";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            Assert.NotNull(stream);
            using var document = JsonDocument.Parse(stream);
            var values = document.RootElement.EnumerateObject().ToArray();
            Assert.All(values, property => Assert.False(string.IsNullOrWhiteSpace(property.Value.GetString())));
            var keys = values.Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            expectedKeys ??= keys;
            Assert.True(expectedKeys.SetEquals(keys), $"Locale {culture} has a different key set.");
        }
    }

    [Theory]
    [InlineData("zh-TW")]
    [InlineData("ja-JP")]
    [InlineData("ru-RU")]
    [InlineData("ko-KR")]
    public void NonLatinLocalesContainTranslatedCharacters(string culture)
    {
        var text = ReadResource($"Jellyfin.Plugin.PrivatePlayback.Localization.resources.{culture}.json");

        Assert.Contains(text, character => character > 127);
    }

    [Fact]
    public void WebResourcesAvoidDynamicHtmlInjection()
    {
        var script = ReadResource("Jellyfin.Plugin.PrivatePlayback.Configuration.Web.config.js");
        var page = ReadResource("Jellyfin.Plugin.PrivatePlayback.Configuration.Web.config.html");

        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
        Assert.DoesNotContain("eval(", script, StringComparison.Ordinal);
        Assert.Contains("textContent", script, StringComparison.Ordinal);
        Assert.Contains("data-controller=\"__plugin/Private Playback.js\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void SupportedCultureResourceNamesAreStableAndUnique()
    {
        Assert.Equal(9, Cultures.Length);
        Assert.Equal(9, Cultures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Cultures, culture =>
            Assert.Equal($"PrivatePlayback.i18n.{culture}.json", SupportedCultures.ResourceName(culture)));
    }

    private static string ReadResource(string resourceName)
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
