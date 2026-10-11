using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ProfessionalMinesweeper.Desktop;

public sealed class DesktopBoardViewport : Canvas
{
    private MinefieldControl? _field;
    private double _zoom = 1;
    private Vector _offset;
    private Point? _drag;
    private Point _dragStart;
    private bool _dragging;
    private bool _center = true;
    public event Action<double>? ZoomChanged;
    public double Zoom => _zoom;

    public DesktopBoardViewport()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent;
        AddHandler(PointerWheelChangedEvent, Wheel, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, Press, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, Move, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, Release, RoutingStrategies.Tunnel);
        SizeChanged += (_, _) => Apply();
    }
    public void SetBoard(MinefieldControl field)
    {
        _field = field;
        Children.Add(field);
        field.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
    }
    public void ResetView() { _center = true; Apply(); }
    public void SetZoom(double zoom) => ZoomAt(zoom, new Point(Bounds.Width / 2, Bounds.Height / 2));
    private void ZoomAt(double zoom, Point anchor)
    {
        double next = Math.Clamp(zoom, .25, 4), ratio = next / _zoom;
        _offset = new Vector(anchor.X - (anchor.X - _offset.X) * ratio, anchor.Y - (anchor.Y - _offset.Y) * ratio);
        _zoom = next; _center = false; Apply(); ZoomChanged?.Invoke(next);
    }
    private void Apply()
    {
        if (_field == null) return;
        _field.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _field.DesiredSize;
        _field.Width = size.Width; _field.Height = size.Height;
        if (_center) _offset = new Vector(Math.Max(0, (Bounds.Width - size.Width * _zoom) / 2), Math.Max(0, (Bounds.Height - size.Height * _zoom) / 2));
        _field.RenderTransform = new MatrixTransform(new Matrix(_zoom, 0, 0, _zoom, _offset.X, _offset.Y));
    }
    private void Wheel(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != 0) ZoomAt(_zoom * Math.Pow(1.15, e.Delta.Y), e.GetPosition(this));
        else
        {
            _center = false;
            _offset += (e.KeyModifiers & KeyModifiers.Shift) != 0 ? new Vector(e.Delta.Y * 48, 0) : new Vector(e.Delta.X * 48, e.Delta.Y * 48);
            Apply();
        }
        e.Handled = true;
    }
    private void Press(object? sender, PointerPressedEventArgs e)
    {
        if (_field == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _drag = _dragStart = e.GetPosition(this);
        _dragging = false;
    }
    private void Move(object? sender, PointerEventArgs e)
    {
        if (!_drag.HasValue) return;
        var point = e.GetPosition(this);
        if (!_dragging)
        {
            if (Math.Abs(point.X - _dragStart.X) + Math.Abs(point.Y - _dragStart.Y) < 8) return;
            _dragging = true;
            _field?.CancelTouchGesture();
            e.Pointer.Capture(this);
        }
        _offset += new Vector(point.X - _drag.Value.X, point.Y - _drag.Value.Y);
        _drag = point; _center = false; Apply(); e.Handled = true;
    }
    private void Release(object? sender, PointerReleasedEventArgs e)
    {
        if (!_drag.HasValue) return;
        _drag = null;
        if (_dragging) { _field?.CancelTouchGesture(); e.Pointer.Capture(null); e.Handled = true; }
        _dragging = false;
    }
}
