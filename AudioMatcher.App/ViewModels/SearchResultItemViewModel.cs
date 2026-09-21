using AudioMatcher.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioMatcher.App.ViewModels;

public sealed partial class SearchResultItemViewModel : ObservableObject
{
    public SearchResultItemViewModel(AudioMatchResult result, bool selected)
    {
        Result = result;
        IsSelected = selected;
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
        MatchStatus.Matched => Result.Matches.Count == 1 ? "Matched" : $"Matched ({Result.Matches.Count})",
        MatchStatus.Failed => "Failed",
        _ => "No Match"
    };

    public string ConfidenceText => Result.FirstMatch is { } match ? match.Confidence.ToString("0.00") : "--";
}
