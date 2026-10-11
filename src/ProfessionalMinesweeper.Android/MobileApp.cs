using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Themes.Fluent;
using ProfessionalMinesweeper.Core;
using ProfessionalMinesweeper.Desktop;
using Button = Avalonia.Controls.Button;
using CheckBox = Avalonia.Controls.CheckBox;
using Orientation = Avalonia.Layout.Orientation;
using Avalonia.Media;
using Avalonia.Threading;

namespace ProfessionalMinesweeper.Android;

public sealed class MobileApp : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IActivityApplicationLifetime activities)
            activities.MainViewFactory = () => new MobileGameView();
        else if (ApplicationLifetime is ISingleViewApplicationLifetime mobile)
            mobile.MainView = new MobileGameView();
        base.OnFrameworkInitializationCompleted();
    }
}

public sealed class MobileGameView : Grid
{
    private readonly MinefieldControl _field = new();
    private BoardType _board = BoardType.Square;
    private int _difficulty;
    private readonly BoardViewport _viewport;
    private readonly Grid _pages = new();
    private readonly DockPanel _gamePanel = new();
    private Border? _aboutPage;
    private readonly TextBlock _status = new();
    private readonly TextBlock _mines = new() { FontSize = 24, Foreground = Brushes.Red, FontFamily = new FontFamily("monospace") };
    private readonly TextBlock _time = new() { FontSize = 24, Foreground = Brushes.Red, FontFamily = new FontFamily("monospace") };
    private readonly Button _restart = new() { Content = "☺", FontSize = 24 };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private MinesweeperGame? _game;

    public MobileGameView()
    {
        Background = new SolidColorBrush(Color.Parse("#EDF2F6"));
        _field.QuickRestartRequested += Restart;
        _viewport = new BoardViewport(_field);
        var toolbar = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
        var menu = new Menu();
        var gameMenu = new MenuItem { Header = "游戏" };
        var newGame = new MenuItem { Header = "新游戏" };
        newGame.Click += (_, _) => Restart();
        gameMenu.Items.Add(newGame);
        for (int level = 0; level < 3; level++)
        {
            int selection = level;
            var item = new MenuItem { Header = new[] { "初级", "中级", "高级" }[level] };
            item.Click += (_, _) => { _difficulty = selection; Restart(); };
            gameMenu.Items.Add(item);
        }
        var boardMenu = new MenuItem { Header = "棋盘" };
        foreach (var type in Enum.GetValues<BoardType>())
        {
            var item = new MenuItem { Header = type.ToString() };
            item.Click += (_, _) => { _board = type; Restart(); };
            boardMenu.Items.Add(item);
        }
        var help = new MenuItem { Header = "帮助" };
        help.Click += (_, _) => ShowModal(new StackPanel
        {
            Margin = new Thickness(24), Spacing = 16,
            Children =
            {
                new TextBlock { Text = "操作指南", FontSize = 24, FontWeight = FontWeight.Bold },
                new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                    "默认操作\n• 单击未翻开格：翻开，首击安全。\n• 长按未翻开格约500毫秒：旗帜 → 问号 → 取消。\n• 双击同一已翻开数字格：连开。\n• 长按已翻开格：预览相关未翻开邻格；松手取消，不连开。\n\n模式开关\n• 标旗模式：点击格子切换标记。\n• 连开模式：点击已翻开数字格连开。\n• 两个开关互斥；关闭后恢复默认操作。\n\n浏览棋盘\n• 单指拖动移动，双指捏合缩放（30%～500%）。\n• 拖动或缩放会取消本次翻开/长按操作。\n• 新局自动调整初始缩放并居中。\n\n新局与菜单\n• 点击笑脸或游戏菜单中的新游戏重开。\n• 胜利/失败后双击棋盘砖块重开。\n• 游戏菜单选择难度，棋盘菜单选择18种模式。\n• 红色数字分别表示剩余雷数和用时（秒）；下方显示状态和翻开进度。\n\n连开规则\n周围旗帜数等于数字时翻开其余邻格；旗帜位置错误仍可能踩雷。" }
            }
        });
        var aboutItem = new MenuItem { Header = "关于" };
        aboutItem.Click += (_, _) =>
        {
            if (_aboutPage != null) return;
            var view = new AboutView();
            view.OpenAuthorRequested += () =>
            {
                var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView,
                    global::Android.Net.Uri.Parse(AboutView.AuthorUrl));
                intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
                global::Android.App.Application.Context.StartActivity(intent);
            };
            ShowModal(view);
        };
        menu.Items.Add(gameMenu); menu.Items.Add(boardMenu); menu.Items.Add(help); menu.Items.Add(aboutItem);
        menu.Background = Brushes.White;
        toolbar.Children.Add(menu);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        _restart.Click += (_, _) => Restart();
        actions.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#18354B")), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6), Child = _mines });
        actions.Children.Add(_restart);
        actions.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#18354B")), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6), Child = _time });
        toolbar.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = actions });
        var switches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        var flagSwitch = new CheckBox { Content = "标旗模式" };
        var chordSwitch = new CheckBox { Content = "连开模式" };
        flagSwitch.IsCheckedChanged += (_, _) =>
        {
            _field.TouchFlagMode = flagSwitch.IsChecked == true;
            if (_field.TouchFlagMode) chordSwitch.IsChecked = false;
        };
        chordSwitch.IsCheckedChanged += (_, _) =>
        {
            _field.TouchChordMode = chordSwitch.IsChecked == true;
            if (_field.TouchChordMode) flagSwitch.IsChecked = false;
        };
        switches.Children.Add(flagSwitch); switches.Children.Add(chordSwitch);
        toolbar.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Child = switches });
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.TextAlignment = TextAlignment.Center;
        toolbar.Children.Add(_status);
        DockPanel.SetDock(toolbar, Dock.Top); _gamePanel.Children.Add(toolbar);
        _pages.Children.Add(_viewport);
        _gamePanel.Children.Add(new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.Parse("#D4DFE8")), Margin = new Thickness(8, 0, 8, 8), Child = _pages });
        Children.Add(_gamePanel);
        _clock.Tick += (_, _) => UpdateStatus();
        AttachedToVisualTree += (_, _) => _clock.Start();
        DetachedFromVisualTree += (_, _) => _clock.Stop();
        Restart();
    }

    private void ShowModal(Control content)
    {
        if (_aboutPage != null) return;
        _field.CancelTouchGesture();
        var close = new Button { Content = "确认", HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(16, 8, 16, 16) };
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        layout.Children.Add(new ScrollViewer { Content = content });
        Grid.SetRow(close, 1);
        layout.Children.Add(close);
        var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(16),
            Margin = new Thickness(20), MaxWidth = 480, MaxHeight = 680,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = layout };
        var overlay = new Border { Background = new SolidColorBrush(Color.Parse("#88000000")), Child = card };
        _aboutPage = overlay;
        _gamePanel.IsHitTestVisible = false;
        Children.Add(overlay);
        close.Click += (_, _) =>
        {
            if (_aboutPage != overlay) return;
            Children.Remove(overlay);
            _aboutPage = null;
            _gamePanel.IsHitTestVisible = true;
        };
    }

    private void Restart()
    {
        var type = _board;
        int level = _difficulty;
        var (rows, cols) = TilingFactory.GetPresetDimensions(type, level);
        var (topology, _) = TilingFactory.CreateMenuBoard(type, level, rows, cols);
        int mines = Math.Clamp(Math.Max(level == 0 ? 10 : level == 1 ? 35 : 80,
            (int)Math.Round(topology.TotalCells * (level == 0 ? .12 : level == 1 ? .16 : .21))), 1, topology.TotalCells - 1);
        if (_game != null) _game.StateChanged -= UpdateStatus;
        _game = new MinesweeperGame(topology, mines);
        _game.StateChanged += UpdateStatus;
        _field.Game = _game;
        _field.Width = double.NaN; _field.Height = double.NaN;
        _viewport.ResetView();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_game == null) return;
        _mines.Text = _game.RemainingFlags.ToString("000");
        _time.Text = Math.Min(999, (int)_game.ElapsedTime.TotalSeconds).ToString("000");
        _restart.Content = _game.Status == GameStatus.Won ? "😎" : _game.Status == GameStatus.Lost ? "☹" : "☺";
        string state = _game.Status switch { GameStatus.Won => "胜利", GameStatus.Lost => "踩雷", GameStatus.InProgress => "进行中", _ => "等待开始" };
        _status.Text = $"{_board} · {new[] { "初级", "中级", "高级" }[_difficulty]} · {state}\n已翻开 {_game.RevealedCount}/{_game.Topology.TotalCells - _game.TotalMines}";
        if (_game.Status != GameStatus.NotStarted)
            _status.Text += _game.IsLogicallySolvable ? " · 逻辑可解" : " · 普通布局（可能需猜）";
    }
}
