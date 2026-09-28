using System.Collections.ObjectModel;
using System.IO;
using AudioMatcher.Core.Interfaces;
using AudioMatcher.Core.Models;
using AudioMatcher.App.Localization;
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
    private readonly ILocalizationService _localization;
    private readonly SynchronizationContext _ui;
    private readonly System.Timers.Timer _positionTimer;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _processCts;
    private bool _outputFollowsSearchFolder = true;
    private bool _updatingSampleText;
    private string? _statusKey;
    private object[] _statusArgs = [];
    private string? _progressKey;
    private object[] _progressArgs = [];
    private int _summaryMatched;
    private int _summaryTotal;
    private ProgressDisplay _progressDisplay = ProgressDisplay.None;
    private int _searchProcessed;
    private int _searchTotal;
    private int _searchMatched;
    private string? _processFileName;
    private ProcessingFileStatus _processFileStatus;
    private int _processCompleted;
    private int _processTotal;
    private static readonly TimeSpan MinSampleLength = TimeSpan.FromMilliseconds(20);

    public MainViewModel(
        IAudioDecoder decoder,
        IWaveformGenerator waveformGenerator,
        IAudioSearchService searchService,
        IAudioProcessingService processingService,
        IAudioPlaybackService playback,
        FFmpegLocator ffmpegLocator,
        IProcessingLogger logger,
        ILocalizationService localization)
    {
        _decoder = decoder;
        _waveformGenerator = waveformGenerator;
        _searchService = searchService;
        _processingService = processingService;
        _playback = playback;
        _ffmpegLocator = ffmpegLocator;
        _logger = logger;
        _localization = localization;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        HasFfmpeg = ffmpegLocator.IsAvailable;
        ApplyFfmpegStatus();
        SetStatus(ffmpegLocator.IsAvailable ? LocalizationKeys.StatusReadyHint : LocalizationKeys.FfmpegMissing);
        UpdateResultSummary();
        _localization.LanguageChanged += OnLanguageChanged;

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
                return _localization.GetString(LocalizationKeys.SearchNeedsFfmpeg);
            }

            if (string.IsNullOrWhiteSpace(SourceFilePath))
            {
                return _localization.GetString(LocalizationKeys.SelectSourceFirst);
            }

            if (SampleEnd <= SampleStart)
            {
                return _localization.GetString(LocalizationKeys.SampleEndAfterStart);
            }

            if (string.IsNullOrWhiteSpace(SearchFolder))
            {
                return _localization.GetString(LocalizationKeys.SelectSearchFolder);
            }

            if (IsProcessing)
            {
                return _localization.GetString(LocalizationKeys.WaitForProcessing);
            }

            return null;
        }
    }

    public bool HasSearchDisabledReason => !string.IsNullOrWhiteSpace(SearchDisabledReason);

    [ObservableProperty]
    private string resultSummary = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailedResults))]
    [NotifyPropertyChangedFor(nameof(FailedCountText))]
    [NotifyCanExecuteChangedFor(nameof(OpenLogsCommand))]
    private int lastFailedCount;

    public bool HasFailedResults => LastFailedCount > 0;

    public bool IsChinese => _localization.CurrentLanguage == AppLanguage.Chinese;

    public string FailedCountText => _localization.Format(LocalizationKeys.FailedCount, LastFailedCount);

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
            Title = _localization.GetString(LocalizationKeys.DialogSelectSource),
            Filter = $"{_localization.GetString(LocalizationKeys.AudioFilesFilter)}|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.wma|{_localization.GetString(LocalizationKeys.AllFilesFilter)}|*.*"
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
            SetStatus(LocalizationKeys.SourceNotFound);
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
                SetStatus(LocalizationKeys.LoadingWaveform);
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

            SetStatus(HasFfmpeg ? LocalizationKeys.SourceLoaded : LocalizationKeys.SourceLoadedNoFfmpeg);
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationKeys.OpenSourceFailed, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BrowseSearchFolder()
    {
        var dialog = new OpenFolderDialog { Title = _localization.GetString(LocalizationKeys.DialogSelectSearchFolder) };
        if (dialog.ShowDialog() == true)
        {
            SearchFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog { Title = _localization.GetString(LocalizationKeys.DialogSelectOutputFolder) };
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
        if (!TryCreateSample(out var sample, out var errorKey))
        {
            SetStatus(errorKey);
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        IsBusy = true;
        IsSearching = true;
        Results.Clear();
        ProgressValue = 0;
        SetProgress(LocalizationKeys.SearchStarting);
        SetStatus(LocalizationKeys.Searching);

        try
        {
            var progress = new Progress<SearchProgress>(report =>
            {
                ProgressValue = report.Fraction;
                ShowSearchProgress(report.Processed, report.Total, report.Matched);
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
                Results.Add(new SearchResultItemViewModel(result, result.HasMatch, _localization));
            }

            var matched = Results.Count(item => item.Result.HasMatch);
            _summaryMatched = matched;
            _summaryTotal = Results.Count;
            UpdateResultSummary();
            if (_searchCts.IsCancellationRequested)
            {
                SetStatus(LocalizationKeys.SearchCancelledKept, Results.Count);
            }
            else
            {
                SetStatus(LocalizationKeys.SearchCompleted, matched, Results.Count);
            }

            ProgressValue = 1;
        }
        catch (OperationCanceledException)
        {
            SetStatus(LocalizationKeys.SearchCancelled);
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationKeys.SearchFailed, ex.Message);
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
            Title = _localization.GetString(LocalizationKeys.DialogLocateFfmpeg),
            Filter = $"ffmpeg.exe|ffmpeg.exe|{_localization.GetString(LocalizationKeys.ExecutableFilter)}|*.exe|{_localization.GetString(LocalizationKeys.AllFilesFilter)}|*.*",
            FileName = "ffmpeg.exe"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!_ffmpegLocator.TryUse(dialog.FileName))
        {
            SetStatus(LocalizationKeys.InvalidFfmpeg);
            return;
        }

        HasFfmpeg = true;
        ApplyFfmpegStatus();
        SetStatus(LocalizationKeys.FfmpegLocated);
        SearchCommand.NotifyCanExecuteChanged();
        ProcessSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SearchDisabledReason));
        OnPropertyChanged(nameof(HasSearchDisabledReason));
    }

    [RelayCommand]
    private void ToggleLanguage() => _localization.ToggleLanguage();

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
            SetStatus(LocalizationKeys.SelectMatchedFile);
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            SetStatus(LocalizationKeys.ChooseOutputFolder);
            return;
        }

        _processCts?.Cancel();
        _processCts?.Dispose();
        _processCts = new CancellationTokenSource();
        IsBusy = true;
        IsProcessing = true;
        LastFailedCount = 0;
        ProgressValue = 0;
        SetStatus(LocalizationKeys.Processing);

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
                ShowProcessProgress(name, report.LastResult.Status, report.Completed, report.Total);
            });

            var batch = await _processingService.ProcessAsync(requests, progress, _processCts.Token);
            LastFailedCount = batch.FailedCount;
            if (_processCts.IsCancellationRequested)
            {
                SetStatus(LocalizationKeys.ProcessingCancelledSummary, batch.SuccessCount, batch.SkippedCount, batch.FailedCount);
            }
            else
            {
                SetStatus(LocalizationKeys.ProcessingCompleted, batch.SuccessCount, batch.SkippedCount);
            }

            ProgressValue = 1;
        }
        catch (OperationCanceledException)
        {
            SetStatus(LocalizationKeys.ProcessingCancelled);
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationKeys.ProcessingFailed, ex.Message);
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
        _localization.LanguageChanged -= OnLanguageChanged;
        _positionTimer.Dispose();
        _searchCts?.Dispose();
        _processCts?.Dispose();
        _playback.Dispose();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshLocalizedText();

    private void SetStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        StatusMessage = _localization.Format(key, args);
    }

    private void SetProgress(string key, params object[] args)
    {
        _progressDisplay = ProgressDisplay.Text;
        _progressKey = key;
        _progressArgs = args;
        ProgressText = _localization.Format(key, args);
    }

    private void ShowSearchProgress(int processed, int total, int matched)
    {
        _progressDisplay = ProgressDisplay.Search;
        _searchProcessed = processed;
        _searchTotal = total;
        _searchMatched = matched;
        ProgressText = _localization.Format(LocalizationKeys.SearchProgress, processed, total, matched);
    }

    private void ShowProcessProgress(string fileName, ProcessingFileStatus status, int completed, int total)
    {
        _progressDisplay = ProgressDisplay.Process;
        _processFileName = fileName;
        _processFileStatus = status;
        _processCompleted = completed;
        _processTotal = total;
        ProgressText = _localization.Format(
            LocalizationKeys.ProcessFileProgress,
            fileName,
            LocalizeFileStatus(status),
            completed,
            total);
    }

    private void ApplyFfmpegStatus()
    {
        FfmpegStatus = HasFfmpeg
            ? _localization.Format(LocalizationKeys.FfmpegReady, _ffmpegLocator.FfmpegPath ?? "")
            : _localization.GetString(LocalizationKeys.FfmpegMissing);
    }

    private void UpdateResultSummary()
        => ResultSummary = _localization.Format(LocalizationKeys.ResultSummary, _summaryMatched, _summaryTotal);

    private string LocalizeFileStatus(ProcessingFileStatus status) => status switch
    {
        ProcessingFileStatus.Success => _localization.GetString(LocalizationKeys.FileStatusSuccess),
        ProcessingFileStatus.Failed => _localization.GetString(LocalizationKeys.FileStatusFailed),
        ProcessingFileStatus.Skipped => _localization.GetString(LocalizationKeys.FileStatusSkipped),
        ProcessingFileStatus.Cancelled => _localization.GetString(LocalizationKeys.FileStatusCancelled),
        _ => status.ToString()
    };

    private void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(IsChinese));
        OnPropertyChanged(nameof(SearchDisabledReason));
        OnPropertyChanged(nameof(HasSearchDisabledReason));
        OnPropertyChanged(nameof(FailedCountText));
        ApplyFfmpegStatus();
        UpdateResultSummary();
        UpdateSampleWarning();

        if (_statusKey is not null)
        {
            StatusMessage = _localization.Format(_statusKey, _statusArgs);
        }

        switch (_progressDisplay)
        {
            case ProgressDisplay.Text when _progressKey is not null:
                ProgressText = _localization.Format(_progressKey, _progressArgs);
                break;
            case ProgressDisplay.Search:
                ProgressText = _localization.Format(
                    LocalizationKeys.SearchProgress,
                    _searchProcessed,
                    _searchTotal,
                    _searchMatched);
                break;
            case ProgressDisplay.Process when _processFileName is not null:
                ProgressText = _localization.Format(
                    LocalizationKeys.ProcessFileProgress,
                    _processFileName,
                    LocalizeFileStatus(_processFileStatus),
                    _processCompleted,
                    _processTotal);
                break;
        }

        foreach (var item in Results)
        {
            item.RefreshLocalization();
        }
    }

    private enum ProgressDisplay
    {
        None,
        Text,
        Search,
        Process
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
            ? _localization.GetString(LocalizationKeys.SampleTooShort)
            : null;
    }

    private bool TryCreateSample(out AudioSample sample, out string errorKey)
    {
        sample = null!;
        errorKey = "";
        if (string.IsNullOrWhiteSpace(SourceFilePath))
        {
            errorKey = LocalizationKeys.SelectSourceFirst;
            return false;
        }

        if (SampleEnd <= SampleStart)
        {
            errorKey = LocalizationKeys.SelectSampleRange;
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchFolder) || !Directory.Exists(SearchFolder))
        {
            errorKey = LocalizationKeys.SelectValidSearchFolder;
            return false;
        }

        sample = new AudioSample(SourceFilePath, SampleStart, SampleEnd);
        return true;
    }
}
