using System.Windows;
using AudioMatcher.App.Services;
using AudioMatcher.App.ViewModels;
using AudioMatcher.App.Views;
using AudioMatcher.Core.Matching;
using AudioMatcher.Core.Processing;
using AudioMatcher.Core.Services;
using AudioMatcher.Infrastructure.Audio;
using AudioMatcher.Infrastructure.FFmpeg;
using AudioMatcher.Infrastructure.Logging;

namespace AudioMatcher.App;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var locator = new FFmpegLocator();
        var runner = new FFmpegProcessRunner();
        var decoder = new FFmpegAudioDecoder(locator, runner);
        var matcher = new NormalizedCrossCorrelationMatcher();
        var scanner = new AudioFileScanner();
        var search = new AudioSearchService(decoder, matcher, scanner);
        var planner = new AudioSegmentPlanner();
        var paths = new OutputPathResolver();
        var processor = new FFmpegAudioProcessor(locator, runner, planner, paths);
        var logger = new FileProcessingLogger();
        var processing = new AudioProcessingService(processor, logger);
        var waveform = new StreamingWaveformGenerator(decoder, locator, runner);
        var player = new NAudioPlaybackService();

        _viewModel = new MainViewModel(
            decoder,
            waveform,
            search,
            processing,
            player,
            locator,
            logger);

        var window = new MainWindow
        {
            DataContext = _viewModel
        };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        base.OnExit(e);
    }
}
