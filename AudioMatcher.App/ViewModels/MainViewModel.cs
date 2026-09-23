using System.Collections.ObjectModel;
using System.IO;
using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;
using AudioMatcher.App.Services;
using AudioMatcher.Infrastructure.FFmpeg;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AudioMatcher.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IAudioDecoder _decoder;
    private readonly IWaveformGenerator _waveformGenerator;
    private readonly IAudioSearchService _searchService;
    private readonly IAudioProcessingService _processingService;
    private readonly IAudioPlaybackService _playback;
    private readonly FFmpegLocator _ffmpegLocator;
    private readonly IProcessingLogger _logger;
    private readonly SynchronizationContext _ui;
    private readonly System.Timers.Timer _positionTimer;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _processCts;
    private bool _outputFollowsSearchFolder = true;
    private bool _updatingSampleText;
    private static readonly TimeSpan MinSampleLength = TimeSpan.FromMilliseconds(20);

    public MainViewModel(
        IAudioDecoder decoder,
        IWaveformGenerator waveformGenerator,
        IAudioSearchService searchService,
        IAudioProcessingService processingService,
        IAudioPlaybackService playback,
        FFmpegLocator ffmpegLocator,
        IProcessingLogger logger)
    {
        _decoder = decoder;
        _waveformGenerator = waveformGenerator;
        _searchService = searchService;
        _processingService = processingService;
        _playback = playback;
        _ffmpegLocator = ffmpegLocator;
        _logger = logger;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        HasFfmpeg = ffmpegLocator.IsAvailable;
        FfmpegStatus = ffmpegLocator.IsAvailable
            ? $"FFmpeg: {ffmpegLocator.FfmpegPath}"
            : FFmpegLocator.MissingMessage;
        StatusMessage = ffmpegLocator.IsAvailable
            ? "Select a source audio file, mark a sample, then search a folder."
            : FFmpegLocator.MissingMessage;

        _playback.PlaybackStopped += (_, _) => _ui.Post(_ =>
        {
            IsPlaying = false;
            RefreshPosition();
        }, null);

        _positionTimer = new System.Timers.Timer(50);
        _positionTimer.Elapsed += (_, _) => _ui.Post(_ => RefreshPosition(), null);
    }

    public ObservableCollection<SearchResultItemViewModel> Results { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    [NotifyPropertyChangedFor(nameof(SearchDisabledReason))]
    [NotifyPropertyChangedFor(nameof(HasSearchDisabledReason))]
    private string? sourceFilePath;

    [ObservableProperty]
    private WaveformData waveform = WaveformData.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    private TimeSpan duration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    private TimeSpan position;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SampleDurationText))]
    [NotifyPropertyChangedFor(nameof(SearchDisabledReason))]
    [NotifyPropertyChangedFor(nameof(HasSearchDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private TimeSpan sampleStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SampleDurationText))]
    [NotifyPropertyChangedFor(nameof(SearchDisabledReason))]
    [NotifyPropertyChangedFor(nameof(HasSearchDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private TimeSpan sampleEnd;

    [ObservableProperty]
    private string sampleStartText = "00:00:00.000";

    [ObservableProperty]
    private string sampleEndText = "00:00:00.000";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSampleWarning))]
    private string? sampleWarning;

    public bool HasSampleWarning => !string.IsNullOrWhiteSpace(SampleWarning);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    [NotifyPropertyChangedFor(nameof(SearchDisabledReason))]
    [NotifyPropertyChangedFor(nameof(HasSearchDisabledReason))]
    private string? searchFolder;

    [ObservableProperty]
    private bool includeSubfolders = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProcessSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchDisabledReason))]
    [NotifyPropertyChangedFor(nameof(HasSearchDisabledReason))]
    private bool isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchDisabledReason))]
    [NotifyPropertyChangedFor(nameof(HasSearchDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private bool isProcessing;

    [ObservableProperty]
    private string statusMessage = "";

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private string progressText = "";

    [ObservableProperty]
    private ProcessingAction selectedAction = ProcessingAction.RemoveAllMatches;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessSelectedCommand))]
    private string? outputFolder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProcessSelectedCommand))]
    [NotifyPropertyChangedFor(nameof(SearchDisabledReason))]
    [NotifyPropertyChangedFor(nameof(HasSearchDisabledReason))]
    private bool hasFfmpeg;

    [ObservableProperty]
    private string ffmpegStatus = "";

    public string? SearchDisabledReason
    {
        get
        {
            if (IsSearching)
            {
                return null;
            }

            if (!HasFfmpeg)
            {
                return "Search 需要 FFmpeg。请点击 Locate FFmpeg，选择完整的 ffmpeg.exe。";
            }

            if (string.IsNullOrWhiteSpace(SourceFilePath))
            {
                return "请先选择源音频文件。";
            }

            if (SampleEnd <= SampleStart)
            {
                return "请设置 Sample 结束时间，且必须晚于开始时间。";
            }

            if (string.IsNullOrWhiteSpace(SearchFolder))
            {
                return "请选择 Search Folder。";
            }

            if (IsProcessing)
            {
                return "请等待当前处理完成。";
            }

            return null;
        }
    }

    public bool HasSearchDisabledReason => !string.IsNullOrWhiteSpace(SearchDisabledReason);

    [ObservableProperty]
    private string resultSummary = "Results: 0 / 0 matched";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailedResults))]
    [NotifyPropertyChangedFor(nameof(FailedCountText))]
    [NotifyCanExecuteChangedFor(nameof(OpenLogsCommand))]
    private int lastFailedCount;

    public bool HasFailedResults => LastFailedCount > 0;

    public string FailedCountText => $"Failed: {LastFailedCount}";

    public string PositionText => $"{TimeText.Format(Position)} / {TimeText.Format(Duration)}";

    public string SampleDurationText
        => SampleEnd > SampleStart ? TimeText.Format(SampleEnd - SampleStart) : "00:00:00.000";

    public bool IsRemoveAll
    {
        get => SelectedAction == ProcessingAction.RemoveAllMatches;
        set
        {
            if (value)
            {
                SelectedAction = ProcessingAction.RemoveAllMatches;
            }
        }
    }

    public bool IsCutBefore
    {
        get => SelectedAction == ProcessingAction.CutBeforeFirstMatch;
        set
        {
            if (value)
            {
                SelectedAction = ProcessingAction.CutBeforeFirstMatch;
            }
        }
    }

    public bool IsCutAfter
    {
        get => SelectedAction == ProcessingAction.CutAfterFirstMatch;
        set
        {
            if (value)
            {
                SelectedAction = ProcessingAction.CutAfterFirstMatch;
            }
        }
    }

    partial void OnSelectedActionChanged(ProcessingAction value)
    {
        OnPropertyChanged(nameof(IsRemoveAll));
        OnPropertyChanged(nameof(IsCutBefore));
        OnPropertyChanged(nameof(IsCutAfter));
    }

    partial void OnSampleStartChanged(TimeSpan value)
    {
        if (_updatingSampleText)
        {
            return;
        }

        SampleStartText = TimeText.Format(value);
        UpdateSampleWarning();
    }

    partial void OnSampleEndChanged(TimeSpan value)
    {
        if (_updatingSampleText)
        {
            return;
        }

        SampleEndText = TimeText.Format(value);
        UpdateSampleWarning();
    }

    partial void OnSampleStartTextChanged(string value) => TryApplySampleText();

    partial void OnSampleEndTextChanged(string value) => TryApplySampleText();

    partial void OnSearchFolderChanged(string? value)
    {
        if (_outputFollowsSearchFolder && !string.IsNullOrWhiteSpace(value))
        {
            OutputFolder = Path.Combine(value, "Processed");
            _outputFollowsSearchFolder = true;
        }
    }

    partial void OnOutputFolderChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(SearchFolder) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var auto = Path.Combine(SearchFolder, "Processed");
        _outputFollowsSearchFolder = string.Equals(Path.GetFullPath(value), Path.GetFullPath(auto), StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select source audio",
            Filter = "Audio files|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.wma|All files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadSourceAsync(dialog.FileName);
        }
    }

    public async Task LoadSourceAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            StatusMessage = "Source audio file was not found.";
            return;
        }

        try
        {
            IsBusy = true;
            _playback.Stop();
            _playback.Open(filePath);
            SourceFilePath = filePath;
            Duration = _playback.Duration;
            Position = TimeSpan.Zero;
            SampleStart = TimeSpan.Zero;
            SampleEnd = TimeSpan.Zero;
            Waveform = WaveformData.Empty;

            if (HasFfmpeg)
            {
                StatusMessage = "Loading waveform...";
                Waveform = await _waveformGenerator.GenerateAsync(filePath, 2400);
                if (Duration <= TimeSpan.Zero)
                {
                    Duration = Waveform.Duration;
                }
            }

            if (string.IsNullOrWhiteSpace(SearchFolder))
            {
                SearchFolder = Path.GetDirectoryName(filePath);
            }

            StatusMessage = HasFfmpeg
                ? "Source loaded. Drag on the waveform or use Set Start / Set End."
                : "Source loaded for playback. Install FFmpeg to search and process files.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open source audio: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BrowseSearchFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select search folder" };
        if (dialog.ShowDialog() == true)
        {
            SearchFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select output folder" };
        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
            _outputFollowsSearchFolder = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlayPause))]
    private void PlayPause()
    {
        if (!_playback.HasAudio)
        {
            return;
        }

        if (IsPlaying)
        {
            _playback.Pause();
            IsPlaying = false;
            _positionTimer.Stop();
            return;
        }

        _playback.Play();
        IsPlaying = true;
        _positionTimer.Start();
    }

    private bool CanPlayPause() => !string.IsNullOrWhiteSpace(SourceFilePath);

    [RelayCommand]
    private void StopPlayback()
    {
        _playback.Stop();
        IsPlaying = false;
        _positionTimer.Stop();
        Position = TimeSpan.Zero;
    }

    [RelayCommand]
    private void SetStartFromPlayhead()
    {
        SampleStart = Position;
        if (SampleEnd <= SampleStart)
        {
            SampleEnd = TimeSpan.FromMilliseconds(Math.Min(Duration.TotalMilliseconds, Position.TotalMilliseconds + 3000));
        }
    }

    [RelayCommand]
    private void SetEndFromPlayhead()
    {
        SampleEnd = Position;
        if (SampleEnd <= SampleStart)
        {
            SampleStart = TimeSpan.FromMilliseconds(Math.Max(0, Position.TotalMilliseconds - 3000));
        }
    }

    [RelayCommand]
    private void ClearSample()
    {
        SampleStart = TimeSpan.Zero;
        SampleEnd = TimeSpan.Zero;
        SampleWarning = null;
    }

    [RelayCommand]
    private void SeekBy(double seconds)
    {
        if (!_playback.HasAudio)
        {
            return;
        }

        _playback.Position = _playback.Position + TimeSpan.FromSeconds(seconds);
        RefreshPosition();
    }

    [RelayCommand]
    private void NudgeSampleStart(double seconds)
    {
        if (SampleEnd <= SampleStart)
        {
            SeekBy(seconds);
            return;
        }

        var start = SampleStart + TimeSpan.FromSeconds(seconds);
        if (start < TimeSpan.Zero)
        {
            start = TimeSpan.Zero;
        }

        var maxStart = SampleEnd - MinSampleLength;
        if (start > maxStart)
        {
            start = maxStart > TimeSpan.Zero ? maxStart : TimeSpan.Zero;
        }

        SampleStart = start;
        SeekTo(start);
    }

    [RelayCommand]
    private void NudgeSampleEnd(double seconds)
    {
        if (SampleEnd <= SampleStart)
        {
            SeekBy(seconds);
            return;
        }

        var end = SampleEnd + TimeSpan.FromSeconds(seconds);
        var minEnd = SampleStart + MinSampleLength;
        if (end < minEnd)
        {
            end = minEnd;
        }

        if (Duration > TimeSpan.Zero && end > Duration)
        {
            end = Duration;
        }

        if (end <= SampleStart)
        {
            return;
        }

        SampleEnd = end;
        SeekTo(end);
    }

    public void SeekTo(TimeSpan time)
    {
        if (!_playback.HasAudio)
        {
            Position = time;
            return;
        }

        _playback.Position = time;
        RefreshPosition();
    }

    public void ApplySelection(TimeSpan start, TimeSpan end)
    {
        SampleStart = start;
        SampleEnd = end;
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        if (!TryCreateSample(out var sample, out var error))
        {
            StatusMessage = error;
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        IsBusy = true;
        IsSearching = true;
        Results.Clear();
        ProgressValue = 0;
        ProgressText = "Starting search...";
        StatusMessage = "Searching...";

        try
        {
            var progress = new Progress<SearchProgress>(report =>
            {
                ProgressValue = report.Fraction;
                ProgressText = $"Processed: {report.Processed} / {report.Total}    Matched: {report.Matched}";
            });

            var results = await _searchService.SearchAsync(
                sample,
                new SearchRequest
                {
                    FolderPath = SearchFolder!,
                    IncludeSubfolders = IncludeSubfolders,
                    MatchOptions = MatchOptions.Normal
                },
                progress,
                _searchCts.Token);

            foreach (var result in results)
            {
                Results.Add(new SearchResultItemViewModel(result, result.HasMatch));
            }

            var matched = Results.Count(item => item.Result.HasMatch);
            ResultSummary = $"Results: {matched} / {Results.Count} matched";
            StatusMessage = _searchCts.IsCancellationRequested
                ? $"Search cancelled. Kept {Results.Count} completed files."
                : $"Search completed. Matched {matched} of {Results.Count} files.";
            ProgressValue = 1;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Search cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            IsBusy = false;
            SearchCommand.NotifyCanExecuteChanged();
            ProcessSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSearch()
        => HasFfmpeg
           && !IsSearching
           && !IsProcessing
           && !string.IsNullOrWhiteSpace(SourceFilePath)
           && !string.IsNullOrWhiteSpace(SearchFolder)
           && SampleEnd > SampleStart;

    [RelayCommand]
    private void LocateFfmpeg()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Locate ffmpeg.exe",
            Filter = "ffmpeg.exe|ffmpeg.exe|Executable|*.exe|All files|*.*",
            FileName = "ffmpeg.exe"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!_ffmpegLocator.TryUse(dialog.FileName))
        {
            StatusMessage = "The selected file is not a valid ffmpeg.exe.";
            return;
        }

        HasFfmpeg = true;
        FfmpegStatus = $"FFmpeg: {_ffmpegLocator.FfmpegPath}";
        StatusMessage = "FFmpeg located. You can search now.";
        SearchCommand.NotifyCanExecuteChanged();
        ProcessSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SearchDisabledReason));
        OnPropertyChanged(nameof(HasSearchDisabledReason));
    }

    [RelayCommand]
    private void CancelSearch() => _searchCts?.Cancel();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Results)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectMatched()
    {
        foreach (var item in Results)
        {
            item.IsSelected = item.Result.HasMatch;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in Results)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanProcess))]
    private async Task ProcessSelectedAsync()
    {
        var selected = Results.Where(item => item.IsSelected && item.Result.HasMatch).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Select at least one matched file.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            StatusMessage = "Choose an output folder first.";
            return;
        }

        _processCts?.Cancel();
        _processCts?.Dispose();
        _processCts = new CancellationTokenSource();
        IsBusy = true;
        IsProcessing = true;
        LastFailedCount = 0;
        ProgressValue = 0;
        StatusMessage = "Processing...";

        var requests = selected.Select(item => new ProcessingRequest
        {
            SourceFilePath = item.FilePath,
            SearchRoot = SearchFolder ?? Path.GetDirectoryName(item.FilePath) ?? "",
            OutputFolder = OutputFolder!,
            Action = SelectedAction,
            FileDuration = item.Result.FileDuration,
            Matches = item.Result.Matches,
            ConflictPolicy = OutputConflictPolicy.Skip
        }).ToArray();

        try
        {
            var progress = new Progress<ProcessingProgress>(report =>
            {
                ProgressValue = report.Total == 0 ? 0 : report.Completed / (double)report.Total;
                var name = Path.GetFileName(report.LastResult.SourceFilePath);
                ProgressText = $"{name}    {report.LastResult.Status}    {report.Completed} / {report.Total}";
            });

            var batch = await _processingService.ProcessAsync(requests, progress, _processCts.Token);
            LastFailedCount = batch.FailedCount;
            StatusMessage = _processCts.IsCancellationRequested
                ? $"Processing cancelled. Success: {batch.SuccessCount}    Skipped: {batch.SkippedCount}    Failed: {batch.FailedCount}"
                : $"Completed    Success: {batch.SuccessCount}    Skipped: {batch.SkippedCount}";
            ProgressValue = 1;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Processing cancelled. Completed files were kept. Incomplete output was discarded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Processing failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            IsBusy = false;
        }
    }

    private bool CanProcess()
        => HasFfmpeg && !IsBusy && Results.Count > 0 && !string.IsNullOrWhiteSpace(OutputFolder);

    [RelayCommand]
    private void CancelProcess() => _processCts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanOpenLogs))]
    private void OpenLogs()
    {
        Directory.CreateDirectory(_logger.DirectoryPath);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _logger.DirectoryPath,
            UseShellExecute = true
        });
    }

    private bool CanOpenLogs() => HasFailedResults;

    public void Dispose()
    {
        _positionTimer.Dispose();
        _searchCts?.Dispose();
        _processCts?.Dispose();
        _playback.Dispose();
    }

    private void RefreshPosition()
    {
        if (_playback.HasAudio)
        {
            Position = _playback.Position;
            IsPlaying = _playback.IsPlaying;
        }
    }

    private void TryApplySampleText()
    {
        if (!TimeText.TryParse(SampleStartText, out var start) || !TimeText.TryParse(SampleEndText, out var end))
        {
            return;
        }

        _updatingSampleText = true;
        SampleStart = start;
        SampleEnd = end;
        _updatingSampleText = false;
        UpdateSampleWarning();
        SearchCommand.NotifyCanExecuteChanged();
    }

    private void UpdateSampleWarning()
    {
        if (SampleEnd <= SampleStart)
        {
            SampleWarning = null;
            return;
        }

        var duration = SampleEnd - SampleStart;
        SampleWarning = duration < TimeSpan.FromSeconds(3)
            ? "Sample is very short and may cause false matches."
            : null;
    }

    private bool TryCreateSample(out AudioSample sample, out string error)
    {
        sample = null!;
        error = "";
        if (string.IsNullOrWhiteSpace(SourceFilePath))
        {
            error = "Select a source audio file first.";
            return false;
        }

        if (SampleEnd <= SampleStart)
        {
            error = "Select a sample range on the waveform.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchFolder) || !Directory.Exists(SearchFolder))
        {
            error = "Select a valid search folder.";
            return false;
        }

        sample = new AudioSample(SourceFilePath, SampleStart, SampleEnd);
        return true;
    }
}
