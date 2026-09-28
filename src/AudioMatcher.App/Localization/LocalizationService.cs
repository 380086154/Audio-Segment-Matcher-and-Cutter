using System.Globalization;
using System.IO;
using System.Windows;

namespace AudioMatcher.App.Localization;

public sealed class LocalizationService : ILocalizationService
{
    public const string DictionaryIdKey = "LocalizationDictionaryId";

    private const string SettingsDirectoryName = "Audio Segment Matcher";
    private const string SettingsFileName = "language.txt";

    public static string SettingsFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        SettingsDirectoryName,
        SettingsFileName);

    public string CurrentLanguage { get; private set; } = AppLanguage.Chinese;

    public event EventHandler? LanguageChanged;

    public LocalizationService()
    {
        Apply(ReadSavedLanguage(), persist: false);
    }

    public string GetString(string key)
    {
        if (Application.Current?.TryFindResource(key) is string value && value.Length > 0)
        {
            return value;
        }

        return key;
    }

    public string Format(string key, params object[] args)
    {
        var template = GetString(key);
        return args.Length == 0
            ? template
            : string.Format(CultureInfo.CurrentCulture, template, args);
    }

    public void ToggleLanguage()
    {
        Apply(
            CurrentLanguage == AppLanguage.Chinese ? AppLanguage.English : AppLanguage.Chinese,
            persist: true);
    }

    private void Apply(string language, bool persist)
    {
        if (!AppLanguage.IsSupported(language))
        {
            language = AppLanguage.Chinese;
        }

        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Strings.{language}.xaml", UriKind.Absolute)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        var previous = merged.FirstOrDefault(item => item.Contains(DictionaryIdKey));
        merged.Add(dictionary);
        if (previous is not null)
        {
            merged.Remove(previous);
        }

        CurrentLanguage = language;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);

        if (persist)
        {
            Save(language);
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string ReadSavedLanguage()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return AppLanguage.Chinese;
            }

            var language = File.ReadAllText(SettingsFilePath).Trim();
            return AppLanguage.IsSupported(language) ? language : AppLanguage.Chinese;
        }
        catch
        {
            return AppLanguage.Chinese;
        }
    }

    private static void Save(string language)
    {
        var directory = Path.GetDirectoryName(SettingsFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsFilePath, language);
    }
}
