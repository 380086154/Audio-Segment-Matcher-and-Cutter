namespace AudioMatcher.App.Localization;

public interface ILocalizationService
{
    string CurrentLanguage { get; }

    event EventHandler? LanguageChanged;

    string GetString(string key);

    string Format(string key, params object[] args);

    void ToggleLanguage();
}
