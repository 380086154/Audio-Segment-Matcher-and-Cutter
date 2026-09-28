namespace AudioMatcher.App.Localization;

public static class AppLanguage
{
    public const string Chinese = "zh-CN";

    public const string English = "en-US";

    public static bool IsSupported(string? language)
        => language is Chinese or English;
}
