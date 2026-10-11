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
        var instructions = new MenuItem { Header = "长按标旗/预览；双击数字连开；拖动/双指缩放" };
        help.Items.Add(instructions);
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
            var close = new Button { Content = "确认", HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(16, 8, 16, 16) };
            _field.CancelTouchGesture();
            var dialogContent = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
            dialogContent.Children.Add(new ScrollViewer { Content = view });
            Grid.SetRow(close, 1);
            dialogContent.Children.Add(close);
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(16),
                Margin = new Thickness(20), MaxWidth = 480, MaxHeight = 680,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = dialogContent };
            // Overlay the entire game, leaving the board attached and its state intact.
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
    }
}
