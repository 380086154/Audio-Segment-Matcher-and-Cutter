using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AudioMatcher.Core.Models;

namespace AudioMatcher.App.Controls;

public sealed class WaveformControl : FrameworkElement
{
    public static readonly DependencyProperty WaveformProperty = DependencyProperty.Register(
        nameof(Waveform), typeof(WaveformData), typeof(WaveformControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SampleStartProperty = DependencyProperty.Register(
        nameof(SampleStart), typeof(TimeSpan), typeof(WaveformControl),
        new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SampleEndProperty = DependencyProperty.Register(
        nameof(SampleEnd), typeof(TimeSpan), typeof(WaveformControl),
        new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position), typeof(TimeSpan), typeof(WaveformControl),
        new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration), typeof(TimeSpan), typeof(WaveformControl),
        new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _dragging;
    private TimeSpan _dragStart;
    private TimeSpan _viewStart;
    private double _zoom = 1;
    private bool _updatingScroll;

    public WaveformData? Waveform
    {
        get => (WaveformData?)GetValue(WaveformProperty);
        set => SetValue(WaveformProperty, value);
    }

    public TimeSpan SampleStart
    {
        get => (TimeSpan)GetValue(SampleStartProperty);
        set => SetValue(SampleStartProperty, value);
    }

    public TimeSpan SampleEnd
    {
        get => (TimeSpan)GetValue(SampleEndProperty);
        set => SetValue(SampleEndProperty, value);
    }

    public TimeSpan Position
    {
        get => (TimeSpan)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public TimeSpan Duration
    {
        get => (TimeSpan)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public event EventHandler<TimeSpan>? SeekRequested;

    public event EventHandler<(TimeSpan Start, TimeSpan End)>? SelectionChanged;

    public event EventHandler? ViewportChanged;

    public bool IsZoomed => _zoom > 1.001;

    public double ScrollMinimum => 0;

    public double ScrollMaximum
    {
        get
        {
            var duration = GetDuration();
            return Math.Max(0, duration.TotalSeconds - VisibleDuration(duration).TotalSeconds);
        }
    }

    public double ScrollViewportSize => Math.Max(0.001, VisibleDuration(GetDuration()).TotalSeconds);

    public double ScrollValue
    {
        get => _viewStart.TotalSeconds;
        set
        {
            if (_updatingScroll)
            {
                return;
            }

            _updatingScroll = true;
            _viewStart = TimeSpan.FromSeconds(Math.Max(0, value));
            EnsureViewport(GetDuration());
            InvalidateVisual();
            ViewportChanged?.Invoke(this, EventArgs.Empty);
            _updatingScroll = false;
        }
    }

    public void Fit()
    {
        _zoom = 1;
        _viewStart = TimeSpan.Zero;
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomIn() => ZoomAt(0.5, 1.25);

    public void ZoomOut() => ZoomAt(0.5, 0.8);

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) => new PointHitTestResult(this, hitTestParameters.HitPoint);

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Duration <= TimeSpan.Zero || ActualWidth <= 0)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && IsZoomed)
        {
            var duration = GetDuration();
            var pan = VisibleDuration(duration).TotalSeconds * (e.Delta > 0 ? -0.15 : 0.15);
            ScrollValue = ScrollValue + pan;
            e.Handled = true;
            return;
        }

        ZoomAt(e.GetPosition(this).X / ActualWidth, e.Delta > 0 ? 1.25 : 0.8);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
        _dragging = true;
        _dragStart = TimeFromX(e.GetPosition(this).X);
        Position = _dragStart;
        SeekRequested?.Invoke(this, _dragStart);
        SampleStart = _dragStart;
        SampleEnd = _dragStart;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
        {
            return;
        }

        var current = TimeFromX(e.GetPosition(this).X);
        if (current < _dragStart)
        {
            SampleStart = current;
            SampleEnd = _dragStart;
        }
        else
        {
            SampleStart = _dragStart;
            SampleEnd = current;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();
        if (SampleEnd - SampleStart > TimeSpan.FromMilliseconds(20))
        {
            SelectionChanged?.Invoke(this, (SampleStart, SampleEnd));
        }
        else
        {
            SeekRequested?.Invoke(this, TimeFromX(e.GetPosition(this).X));
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = new Rect(RenderSize);
        drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(24, 28, 34)), null, bounds, 8, 8);

        var duration = GetDuration();
        if (duration <= TimeSpan.Zero || ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        EnsureViewport(duration);
        var viewDuration = VisibleDuration(duration);
        if (SampleEnd > SampleStart)
        {
            var x1 = TimeToX(SampleStart, duration);
            var x2 = TimeToX(SampleEnd, duration);
            drawingContext.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(70, 232, 176, 70)),
                null,
                new Rect(x1, 0, Math.Max(1, x2 - x1), ActualHeight));
        }

        var peaks = Waveform?.Peaks;
        if (peaks is { Length: > 1 })
        {
            var mid = ActualHeight / 2;
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(126, 200, 227)), 1);
            pen.Freeze();
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                for (var i = 0; i < peaks.Length; i++)
                {
                    var time = TimeSpan.FromSeconds(duration.TotalSeconds * i / (peaks.Length - 1));
                    if (time < _viewStart || time > _viewStart + viewDuration)
                    {
                        continue;
                    }

                    var x = TimeToX(time, duration);
                    var amp = peaks[i] * (mid - 6);
                    context.BeginFigure(new Point(x, mid - amp), false, false);
                    context.LineTo(new Point(x, mid + amp), true, false);
                }
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
        }

        var playX = TimeToX(Position, duration);
        drawingContext.DrawLine(
            new Pen(new SolidColorBrush(Color.FromRgb(255, 107, 107)), 1.5),
            new Point(playX, 4),
            new Point(playX, ActualHeight - 4));
    }

    private void ZoomAt(double relativeX, double factor)
    {
        var duration = Duration > TimeSpan.Zero ? Duration : Waveform?.Duration ?? TimeSpan.Zero;
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var center = _viewStart + TimeSpan.FromSeconds(VisibleDuration(duration).TotalSeconds * relativeX);
        _zoom = Math.Clamp(_zoom * factor, 1, 80);
        var view = VisibleDuration(duration);
        _viewStart = center - TimeSpan.FromSeconds(view.TotalSeconds * relativeX);
        EnsureViewport(duration);
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    private TimeSpan GetDuration()
        => Duration > TimeSpan.Zero ? Duration : Waveform?.Duration ?? TimeSpan.Zero;

    private void EnsureViewport(TimeSpan duration)
    {
        var view = VisibleDuration(duration);
        if (_viewStart < TimeSpan.Zero)
        {
            _viewStart = TimeSpan.Zero;
        }

        if (_viewStart + view > duration)
        {
            _viewStart = duration - view;
            if (_viewStart < TimeSpan.Zero)
            {
                _viewStart = TimeSpan.Zero;
            }
        }
    }

    private TimeSpan VisibleDuration(TimeSpan duration) => TimeSpan.FromSeconds(duration.TotalSeconds / _zoom);

    private double TimeToX(TimeSpan time, TimeSpan duration)
    {
        var view = VisibleDuration(duration);
        if (view <= TimeSpan.Zero)
        {
            return 0;
        }

        return (time - _viewStart).TotalSeconds / view.TotalSeconds * ActualWidth;
    }

    private TimeSpan TimeFromX(double x)
    {
        var duration = Duration > TimeSpan.Zero ? Duration : Waveform?.Duration ?? TimeSpan.Zero;
        if (duration <= TimeSpan.Zero || ActualWidth <= 0)
        {
            return TimeSpan.Zero;
        }

        var view = VisibleDuration(duration);
        var time = _viewStart + TimeSpan.FromSeconds(Math.Clamp(x, 0, ActualWidth) / ActualWidth * view.TotalSeconds);
        if (time < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return time > duration ? duration : time;
    }
}
