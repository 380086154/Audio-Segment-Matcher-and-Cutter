using AudioMatcher.App.Localization;
using AudioMatcher.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioMatcher.App.ViewModels;

public sealed partial class SearchResultItemViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    public SearchResultItemViewModel(AudioMatchResult result, bool selected, ILocalizationService localization)
    {
        Result = result;
        IsSelected = selected;
        _localization = localization;
    }

    public AudioMatchResult Result { get; }

    [ObservableProperty]
    private bool isSelected;

    public string FileName => Result.FileName;

    public string FilePath => Result.FilePath;

    public string DurationText => Result.FileDuration <= TimeSpan.Zero ? "--" : TimeText.Format(Result.FileDuration);

    public string MatchText => Result.FirstMatch is { } match ? TimeText.Format(match.Start) : "--";

    public string StatusText => Result.Status switch
    {
        MatchStatus.Matched => Result.Matches.Count == 1
            ? _localization.GetString(LocalizationKeys.StatusMatched)
            : _localization.Format(LocalizationKeys.StatusMatchedCount, Result.Matches.Count),
        MatchStatus.Failed => _localization.GetString(LocalizationKeys.StatusFailed),
        _ => _localization.GetString(LocalizationKeys.StatusNoMatch)
    };

    public void RefreshLocalization() => OnPropertyChanged(nameof(StatusText));

    public string ConfidenceText => Result.FirstMatch is { } match ? match.Confidence.ToString("0.00") : "--";
}
