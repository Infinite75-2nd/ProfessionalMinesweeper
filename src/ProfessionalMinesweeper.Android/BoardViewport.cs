using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ProfessionalMinesweeper.Desktop;

namespace ProfessionalMinesweeper.Android;

// Keep touch navigation independent from tile input. Movement and multi-touch
// cancel the tile gesture before release can reveal or flag a cell.
public sealed class BoardViewport : Canvas
{
    private readonly MinefieldControl _field;
    private readonly Dictionary<int, Point> _touches = new();
    private readonly Dictionary<int, IPointer> _pointers = new();
    private Point _lastCenter;
    private Point _start;
    private double _lastDistance;
    private double _scale = 1;
    private Vector _offset;
    private bool _navigating;
    private bool _initialized;

    public BoardViewport(MinefieldControl field)
    {
        _field = field;
        ClipToBounds = true;
        Background = Brushes.Transparent;
        Children.Add(field);
        field.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        AddHandler(PointerPressedEvent, Press, RoutingStrategies.Tunnel, true);
        AddHandler(PointerMovedEvent, Move, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, Release, RoutingStrategies.Tunnel, true);
        SizeChanged += (_, _) => { if (!_initialized) ResetView(); else Apply(); };
    }

    public void ResetView()
    {
        _field.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _field.DesiredSize;
        _field.Width = size.Width; _field.Height = size.Height;
        if (Bounds.Width <= 0 || Bounds.Height <= 0) { _initialized = false; return; }
        _scale = Math.Clamp(Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height), .5, 2);
        _offset = new Vector((Bounds.Width - size.Width * _scale) / 2,
            (Bounds.Height - size.Height * _scale) / 2);
        _initialized = true;
        Apply();
    }

    private void Apply()
    {
        _field.RenderTransform = new MatrixTransform(new Matrix(_scale, 0, 0, _scale, _offset.X, _offset.Y));
    }

    private (Point center, double distance) Gesture()
    {
        var points = _touches.Values.ToArray();
        if (points.Length < 2) return (points[0], 0);
        var a = points[0]; var b = points[1];
        return (new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), Distance(a, b));
    }

    private void Press(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch) return;
        var pos = e.GetPosition(this);
        _touches[e.Pointer.Id] = pos; _pointers[e.Pointer.Id] = e.Pointer;
        if (_touches.Count == 1) { _start = pos; _navigating = false; }
        else { _navigating = true; _field.CancelTouchGesture(); e.Handled = true; }
        (_lastCenter, _lastDistance) = Gesture();
    }

    private void Move(object? sender, PointerEventArgs e)
    {
        if (!_touches.ContainsKey(e.Pointer.Id)) return;
        var pos = e.GetPosition(this);
        _touches[e.Pointer.Id] = pos;
        var (center, distance) = Gesture();
        if (!_navigating && Distance(pos, _start) > 10)
        {
            _navigating = true; _field.CancelTouchGesture();
            foreach (var pointer in _pointers.Values) pointer.Capture(this);
        }
        if (_navigating)
        {
            double newScale = _lastDistance > 0 && distance > 0
                ? Math.Clamp(_scale * distance / _lastDistance, .3, 5) : _scale;
            double ratio = newScale / _scale;
            _offset = new Vector(center.X - (_lastCenter.X - _offset.X) * ratio,
                center.Y - (_lastCenter.Y - _offset.Y) * ratio);
            _scale = newScale;
            Apply(); e.Handled = true;
        }
        _lastCenter = center; _lastDistance = distance;
    }

    private void Release(object? sender, PointerReleasedEventArgs e)
    {
        if (!_touches.Remove(e.Pointer.Id)) return;
        _pointers.Remove(e.Pointer.Id);
        if (_navigating) { _field.CancelTouchGesture(); e.Handled = true; }
        if (_touches.Count > 0) (_lastCenter, _lastDistance) = Gesture();
        else { _lastDistance = 0; _navigating = false; }
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
