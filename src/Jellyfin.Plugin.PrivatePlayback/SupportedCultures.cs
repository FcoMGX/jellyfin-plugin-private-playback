namespace Jellyfin.Plugin.PrivatePlayback;

internal static class SupportedCultures
{
    public static IReadOnlyList<string> All { get; } =
    [
        "en-GB",
        "es-ES",
        "pt-PT",
        "fr-FR",
        "it-IT",
        "zh-TW",
        "ja-JP",
        "ru-RU",
        "ko-KR"
    ];

    public static string ResourceName(string culture) => "PrivatePlayback.i18n." + culture + ".json";
}
