using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AudioMatcher.App.ViewModels;
using AudioMatcher.Core.Models;

namespace AudioMatcher.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void OnSeekRequested(object? sender, TimeSpan time) => Vm?.SeekTo(time);

    private void OnSelectionChanged(object? sender, (TimeSpan Start, TimeSpan End) range)
        => Vm?.ApplySelection(range.Start, range.End);

    private void OnZoomIn(object sender, RoutedEventArgs e) => Waveform.ZoomIn();

    private void OnZoomOut(object sender, RoutedEventArgs e) => Waveform.ZoomOut();

    private void OnFit(object sender, RoutedEventArgs e) => Waveform.Fit();

    private bool _updatingWaveformScroll;

    private void OnWaveformViewportChanged(object? sender, EventArgs e) => SyncWaveformScrollBar();

    private void SyncWaveformScrollBar()
    {
        _updatingWaveformScroll = true;
        try
        {
            WaveformScroll.Minimum = Waveform.ScrollMinimum;
            WaveformScroll.Maximum = Waveform.ScrollMaximum;
            WaveformScroll.ViewportSize = Waveform.ScrollViewportSize;
            WaveformScroll.Value = Waveform.ScrollValue;
            WaveformScroll.Visibility = Waveform.IsZoomed ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _updatingWaveformScroll = false;
        }
    }

    private void OnWaveformScroll(object sender, ScrollEventArgs e)
    {
        if (_updatingWaveformScroll)
        {
            return;
        }

        Waveform.ScrollValue = WaveformScroll.Value;
    }

    private void OnWaveformScrollValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingWaveformScroll)
        {
            return;
        }

        Waveform.ScrollValue = e.NewValue;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox)
        {
            return;
        }

        var vm = Vm;
        if (vm is null)
        {
            return;
        }

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 0.01 : 0.05;
        var nudgeStart = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            case Key.Space:
                vm.PlayPauseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.I:
                vm.SetStartFromPlayheadCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.O:
                vm.SetEndFromPlayheadCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left:
                if (nudgeStart)
                {
                    vm.NudgeSampleStartCommand.Execute(-step);
                }
                else
                {
                    vm.NudgeSampleEndCommand.Execute(-step);
                }

                e.Handled = true;
                break;
            case Key.Right:
                if (nudgeStart)
                {
                    vm.NudgeSampleStartCommand.Execute(step);
                }
                else
                {
                    vm.NudgeSampleEndCommand.Execute(step);
                }

                e.Handled = true;
                break;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (Vm is null || e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            return;
        }

        var path = paths[0];
        if (Directory.Exists(path))
        {
            Vm.SearchFolder = path;
            return;
        }

        if (File.Exists(path) && AudioFileFormats.IsSupported(path))
        {
            await Vm.LoadSourceAsync(path);
        }
    }
}
