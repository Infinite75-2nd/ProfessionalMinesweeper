using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ProfessionalMinesweeper.Core;

namespace ProfessionalMinesweeper.Desktop;

public sealed class MinefieldControl : Control
{
    public bool TouchFlagMode { get; set; }
    public bool TouchChordMode { get; set; }
    public void CancelTouchGesture()
    {
        _touchConsumed = true;
        ResetInputState();
        InvalidateVisual();
    }
    private readonly DispatcherTimer _touchHold = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Point _touchOrigin;
    private bool _touchConsumed;
    private Position? _lastTouchTap;
    private DateTime _lastTouchTapTime;
    private const int WH_MOUSE_LL = 14;
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private static IntPtr _hook = IntPtr.Zero;
    private static readonly LowLevelMouseProc _hookProc = HookCallback;
    private static volatile bool _hLeft, _hRight, _hMiddle;

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            switch ((int)wParam)
            {
                case 0x0201: _hLeft = true; break;   // WM_LBUTTONDOWN
                case 0x0202: _hLeft = false; break;  // WM_LBUTTONUP
                case 0x0204: _hRight = true; break;  // WM_RBUTTONDOWN
                case 0x0205: _hRight = false; break; // WM_RBUTTONUP
                case 0x0207: _hMiddle = true; break; // WM_MBUTTONDOWN
                case 0x0208: _hMiddle = false; break;// WM_MBUTTONUP
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private readonly DispatcherTimer _poll;
    private bool _pointerInside;

    private MinesweeperGame? _game;
    private Position? _hoveredPos;

    private bool _leftDown;
    private bool _rightDown;
    private bool _middleDown;
    private bool _chordArmed;
    private bool _chordConsumed;
    private Position? _pressedPos;
    private readonly HashSet<Position> _sunken = new();
    private readonly Dictionary<Position, StreamGeometry> _geometryCache = new();
    private static readonly Dictionary<(string Text, IBrush Brush, double Size, FontWeight Weight), FormattedText> TextCache = new();
    public bool HasTileAt(Point point) => HitTest(point).HasValue;

    private readonly List<(Tile Tile, double MinX, double MinY, double MaxX, double MaxY)> _lookup = new();
    private Rect _bounds;

    private static readonly IBrush CoveredBrush = new SolidColorBrush(Color.FromRgb(206, 206, 206));
    private static readonly IBrush CoveredHoverBrush = new SolidColorBrush(Color.FromRgb(228, 228, 228));
    private static readonly IBrush SunkenBrush = new SolidColorBrush(Color.FromRgb(178, 178, 178));
    private static readonly IBrush RevealedBrush = new SolidColorBrush(Color.FromRgb(238, 238, 238));
    private static readonly IBrush ExplodedBrush = new SolidColorBrush(Color.FromRgb(226, 88, 88));

    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.FromRgb(105, 105, 105)), 1);
    private static readonly IPen SunkenPen = new Pen(new SolidColorBrush(Color.FromRgb(135, 135, 135)), 1);
    private static readonly IPen HighlightPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 2);
    private static readonly IPen ShadowPen = new Pen(new SolidColorBrush(Color.FromRgb(112, 112, 112)), 2);

    private static readonly Dictionary<int, IBrush> NumberBrushes = new()
    {
        { 1, new SolidColorBrush(Color.FromRgb(25, 25, 200)) },
        { 2, new SolidColorBrush(Color.FromRgb(20, 120, 20)) },
        { 3, new SolidColorBrush(Color.FromRgb(200, 30, 30)) },
        { 4, new SolidColorBrush(Color.FromRgb(20, 20, 140)) },
        { 5, new SolidColorBrush(Color.FromRgb(140, 20, 20)) },
        { 6, new SolidColorBrush(Color.FromRgb(20, 120, 120)) },
        { 7, new SolidColorBrush(Color.FromRgb(20, 20, 20)) },
        { 8, new SolidColorBrush(Color.FromRgb(90, 90, 90)) },
        { 9, new SolidColorBrush(Color.FromRgb(120, 40, 120)) },
        { 10, new SolidColorBrush(Color.FromRgb(170, 90, 0)) },
        { 11, new SolidColorBrush(Color.FromRgb(40, 120, 40)) },
        { 12, new SolidColorBrush(Color.FromRgb(60, 60, 60)) }
    };

    public event Action? QuickRestartRequested;

    public MinesweeperGame? Game
    {
        get => _game;
        set
        {
            if (_game != null) _game.StateChanged -= OnGameStateChanged;
            _game = value;
            if (_game != null) _game.StateChanged += OnGameStateChanged;
            RebuildLookup();
            ResetInputState();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public MinefieldControl()
    {
        _touchHold.Tick += (_, _) =>
        {
            _touchHold.Stop();
            if (_game == null || !_pressedPos.HasValue) return;
            _touchConsumed = true;
            var pos = _pressedPos.Value;
            if (_game.GetCell(pos).State == CellState.Revealed)
            {
                _sunken.Clear();
                foreach (var n in _game.Topology.GetNeighbors(pos))
                    if (_game.GetCell(n).State == CellState.Covered) _sunken.Add(n);
                InvalidateVisual();
                return;
            }
            else _game.ToggleFlag(pos);
            _sunken.Clear();
            InvalidateVisual();
        };
        ClipToBounds = true;
        Focusable = true;

        // Avalonia does not raise PointerPressed for a second mouse button pressed
        // while the first is held, so a low-level mouse hook tracks the real button
        // state and a poll timer turns a left+right (or middle) hold into a chord.
        if (OperatingSystem.IsWindows() && _hook == IntPtr.Zero)
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, GetModuleHandle(null), 0);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        _poll.Tick += (_, _) => PollButtons();
        _poll.Start();
    }

    private void PollButtons()
    {
        if (_game == null) return;

        // The hook is global. Clear its stale state whenever the pointer is not
        // over this control; otherwise a later right click can be interpreted as
        // the release half of an old chord.
        if (!_pointerInside)
        {
            _chordArmed = false;
            return;
        }

        bool dual = _hMiddle || (_hLeft && _hRight);
        if (_chordConsumed)
        {
            _leftDown = _hLeft;
            _rightDown = _hRight;
            _middleDown = _hMiddle;
            if (!_hLeft && !_hRight && !_hMiddle) _chordConsumed = false;
            return;
        }

        if (_game.Status is GameStatus.Won or GameStatus.Lost)
        {
            _chordArmed = false;
            return;
        }

        if (dual)
        {
            // Both buttons held: arm the gesture and show the pressed preview. The
            // actual reveal happens on release (mouseup), like classic Minesweeper.
            if (!_chordArmed)
            {
                _chordArmed = true;
            }
            RefreshSunken();
            return;
        }

        if (_chordArmed)
        {
            // A button was released while the gesture was armed -> fire the chord.
            _chordArmed = false;
            _chordConsumed = true;
            _leftDown = _rightDown = _middleDown = false;
            _pressedPos = null;
            var pos = _hoveredPos;
            if (pos.HasValue)
            {
                var cell = _game.GetCell(pos.Value);
                if (cell.State == CellState.Revealed) _game.Chord(pos.Value);
                else if (cell.State == CellState.Covered) _game.Reveal(pos.Value);
            }
            RefreshSunken();
        }
    }

    private void OnGameStateChanged() => InvalidateVisual();

    private void ResetInputState()
    {
        _touchHold.Stop();
        _leftDown = _rightDown = _middleDown = false;
        _chordArmed = false;
        _chordConsumed = false;
        _pressedPos = null;
        _sunken.Clear();
    }

    private void RebuildLookup()
    {
        _lookup.Clear();
        _geometryCache.Clear();
        if (_game == null) return;

        const double pad = 12;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var tile in _game.Topology.Tiles)
        {
            double tminX = double.MaxValue, tminY = double.MaxValue, tmaxX = double.MinValue, tmaxY = double.MinValue;
            foreach (var p in tile.Polygon)
            {
                tminX = Math.Min(tminX, p.X); tminY = Math.Min(tminY, p.Y);
                tmaxX = Math.Max(tmaxX, p.X); tmaxY = Math.Max(tmaxY, p.Y);
            }
            _lookup.Add((tile, tminX, tminY, tmaxX, tmaxY));
            _geometryCache[tile.Pos] = BuildGeometry(tile.Polygon);
            minX = Math.Min(minX, tminX); minY = Math.Min(minY, tminY);
            maxX = Math.Max(maxX, tmaxX); maxY = Math.Max(maxY, tmaxY);
        }
        _bounds = _lookup.Count == 0 ? new Rect(0, 0, 100, 100)
            : new Rect(minX - pad, minY - pad, maxX - minX + pad * 2, maxY - minY + pad * 2);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_lookup.Count == 0) return new Size(120, 120);
        return new Size(_bounds.Width, _bounds.Height);
    }

    private Position? HitTest(Point pt)
    {
        pt = new Point(pt.X + _bounds.X, pt.Y + _bounds.Y);
        foreach (var (tile, minX, minY, maxX, maxY) in _lookup)
        {
            if (pt.X < minX || pt.X > maxX || pt.Y < minY || pt.Y > maxY) continue;
            if (PointInPolygon(pt, tile.Polygon)) return tile.Pos;
        }
        return null;
    }

    private static bool PointInPolygon(Point test, Pt[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].Y > test.Y) != (poly[j].Y > test.Y)) &&
                (test.X < (poly[j].X - poly[i].X) * (test.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    // ---------- Input ----------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_game == null) return;
        if (e.Pointer.Type == PointerType.Touch && _pressedPos.HasValue)
        {
            var movement = e.GetPosition(this) - _touchOrigin;
            if (Math.Abs(movement.X) > 10 || Math.Abs(movement.Y) > 10)
            {
                _touchConsumed = true;
                ResetInputState();
                InvalidateVisual();
            }
            return;
        }
        _pointerInside = true;

        var pos = HitTest(e.GetPosition(this));
        if (pos != _hoveredPos)
        {
            _hoveredPos = pos;
            RefreshSunken();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _pointerInside = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerInside = false;
        _hoveredPos = null;
        ResetInputState();
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_game == null) return;
        Focus();
        if (e.Pointer.Type == PointerType.Touch)
        {
            ResetInputState();
            _touchOrigin = e.GetPosition(this);
            _pressedPos = HitTest(_touchOrigin);
            _hoveredPos = _pressedPos;
            _touchConsumed = false;
            if (_pressedPos.HasValue)
            {
                _touchHold.Start();
                _leftDown = true;
                RefreshSunken();
            }
            return;
        }

        var props = e.GetCurrentPoint(this).Properties;
        var kind = props.PointerUpdateKind;
        var pos = HitTest(e.GetPosition(this));

        // A right press is an independent flag action unless the low-level hook
        // has observed a real simultaneous gesture. Never let a stale left state
        // turn it into a reveal.
        if (kind == PointerUpdateKind.RightButtonPressed && !_hLeft && !_hMiddle)
        {
            _leftDown = false;
            _chordArmed = false;
            _chordConsumed = false;
            _pressedPos = null;
        }

        // Track each button from the button that triggered THIS event. Reading the
        // aggregate IsLeftButtonPressed is unreliable for the other button, so OR it
        // in only as a fallback for the button that changed.
        if (kind == PointerUpdateKind.LeftButtonPressed) _leftDown = true;
        if (kind == PointerUpdateKind.RightButtonPressed) _rightDown = true;
        if (kind == PointerUpdateKind.MiddleButtonPressed) _middleDown = true;

        if (pos.HasValue) { _pressedPos = pos; _hoveredPos = pos; }

        if (_game.Status is GameStatus.Won or GameStatus.Lost && kind == PointerUpdateKind.RightButtonPressed)
        {
            QuickRestartRequested?.Invoke();
            return;
        }

        bool dual = OperatingSystem.IsWindows()
            ? _hMiddle || (_hLeft && _hRight)
            : _middleDown || (_leftDown && _rightDown);
        if (dual)
        {
            _chordArmed = true;
            RefreshSunken();
            return;
        }

        if (kind == PointerUpdateKind.RightButtonPressed && !_hLeft && !_hMiddle && pos.HasValue)
            _game.ToggleFlag(pos.Value);

        RefreshSunken();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_game == null) return;

        var props = e.GetCurrentPoint(this).Properties;
        var kind = props.PointerUpdateKind;
        var pos = HitTest(e.GetPosition(this));
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touchHold.Stop();
            if (!_touchConsumed && pos.HasValue && pos == _pressedPos)
            {
                if (_game.Status is GameStatus.Won or GameStatus.Lost)
                {
                    if (_lastTouchTap.HasValue && DateTime.UtcNow - _lastTouchTapTime < TimeSpan.FromMilliseconds(350))
                    {
                        _lastTouchTap = null;
                        QuickRestartRequested?.Invoke();
                    }
                    else
                    {
                        _lastTouchTap = pos;
                        _lastTouchTapTime = DateTime.UtcNow;
                    }
                }
                else if (TouchChordMode) _game.Chord(pos.Value);
                else if (TouchFlagMode) _game.ToggleFlag(pos.Value);
                else if (_game.GetCell(pos.Value).State == CellState.Revealed)
                {
                    if (_lastTouchTap == pos && DateTime.UtcNow - _lastTouchTapTime < TimeSpan.FromMilliseconds(350))
                    {
                        _game.Chord(pos.Value);
                        _lastTouchTap = null;
                    }
                    else
                    {
                        _lastTouchTap = pos;
                        _lastTouchTapTime = DateTime.UtcNow;
                    }
                }
                else if (TouchFlagMode) _game.ToggleFlag(pos.Value);
                else if (TouchChordMode) _game.Chord(pos.Value);
                else _game.Reveal(pos.Value);
            }
            ResetInputState();
            InvalidateVisual();
            return;
        }

        if (kind == PointerUpdateKind.LeftButtonReleased) _leftDown = false;
        if (kind == PointerUpdateKind.RightButtonReleased) _rightDown = false;
        if (kind == PointerUpdateKind.MiddleButtonReleased) _middleDown = false;

        if (_chordArmed || _chordConsumed)
        {
            // The armed chord is resolved by the poll timer on release; swallow the
            // normal left/right handling so it does not double-fire.
            RefreshSunken();
            return;
        }

        if (kind == PointerUpdateKind.LeftButtonReleased)
        {
            if (pos.HasValue && pos == _pressedPos && !_rightDown && !_hRight && !_hMiddle)
                _game.Reveal(pos.Value);
            _pressedPos = null;
        }

        RefreshSunken();
    }

    private void RefreshSunken()
    {
        _sunken.Clear();
        if (_game != null && _hoveredPos.HasValue)
        {
            var pos = _hoveredPos.Value;
            bool chord = _chordArmed || _middleDown || (_leftDown && _rightDown);
            if (chord)
            {
                if (_game.GetCell(pos).State == CellState.Covered) _sunken.Add(pos);
                foreach (var n in _game.Topology.GetNeighbors(pos))
                    if (_game.GetCell(n).State == CellState.Covered) _sunken.Add(n);
            }
            else if (_leftDown)
            {
                if (_game.GetCell(pos).State == CellState.Covered) _sunken.Add(pos);
            }
        }
        InvalidateVisual();
    }

    // ---------- Rendering ----------

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_game == null) return;

        using var boardTransform = context.PushTransform(Matrix.CreateTranslation(-_bounds.X, -_bounds.Y));
        foreach (var (tile, _, _, _, _) in _lookup)
        {
            var cell = _game.GetCell(tile.Pos);
            bool hovered = _hoveredPos == tile.Pos;
            bool sunken = _sunken.Contains(tile.Pos);


            IBrush fill;
            if (sunken) fill = SunkenBrush;
            else if (cell.State == CellState.Revealed) fill = cell.Exploded ? ExplodedBrush : RevealedBrush;
            else if (hovered) fill = CoveredHoverBrush;
            else fill = CoveredBrush;

            var geom = _geometryCache[tile.Pos];

            if (sunken)
            {
                context.DrawGeometry(fill, BorderPen, geom);
            }
            else
            {
                context.DrawGeometry(fill, BorderPen, geom);
            }

            DrawContent(context, cell, tile.Center);
        }
    }

    private static StreamGeometry BuildGeometry(Pt[] pts)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(pts[0].X, pts[0].Y), true);
            for (int i = 1; i < pts.Length; i++) ctx.LineTo(new Point(pts[i].X, pts[i].Y));
            ctx.EndFigure(true);
        }
        return g;
    }

    private static void DrawBevel(DrawingContext context, Pt[] pts)
    {
        double area = 0;
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Length];
            area += a.X * b.Y - b.X * a.Y;
        }
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Length];
            double light = (b.Y - a.Y) - (b.X - a.X);
            if (area < 0) light = -light;
            context.DrawLine(light < 0 ? HighlightPen : ShadowPen,
                new Point(a.X, a.Y), new Point(b.X, b.Y));
        }
    }

    private static void DrawContent(DrawingContext context, Cell cell, Pt center)
    {
        var pt = new Point(center.X, center.Y);
        switch (cell.State)
        {
            case CellState.Flagged:
                DrawText(context, "⚑", pt, Brushes.Firebrick, 15);
                break;
            case CellState.Questioned:
                DrawText(context, "?", pt, Brushes.Black, 15, FontWeight.Bold);
                break;
            case CellState.Revealed:
                if (cell.IsMine)
                    DrawText(context, "✱", pt, Brushes.Black, 16);
                else if (cell.AdjacentMines > 0)
                {
                    var brush = NumberBrushes.TryGetValue(cell.AdjacentMines, out var b) ? b : Brushes.Black;
                    DrawText(context, cell.AdjacentMines.ToString(), pt, brush, 13, FontWeight.Bold);
                }
                break;
        }
    }

    private static void DrawText(DrawingContext context, string text, Point center, IBrush brush, double size, FontWeight weight = FontWeight.Normal)
    {
        var key = (text, brush, size, weight);
        if (!TextCache.TryGetValue(key, out var ft))
        {
            ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);
            TextCache[key] = ft;
        }
        context.DrawText(ft, new Point(center.X - ft.Width / 2.0, center.Y - ft.Height / 2.0));
    }
}
