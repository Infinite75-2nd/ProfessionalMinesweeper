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
using Avalonia.Styling;

namespace ProfessionalMinesweeper.Android;

public sealed class MobileApp : Avalonia.Application
{
    public override void Initialize()
    {
        // The mobile UI uses light cards. Do not inherit white foregrounds
        // from a device's dark theme while keeping those cards white.
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

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
    // Marks surfaces that follow the active theme. Decorative frames (such as the
    // red-on-navy counters) deliberately keep their own colours.
    private static readonly object ThemeSurface = new();

    private readonly MinefieldControl _field = new();
    private BoardType _board = BoardType.Square;
    private int _difficulty;
    private readonly BoardViewport _viewport;
    private readonly Grid _pages = new();
    private readonly DockPanel _gamePanel = new();
    private Border? _aboutPage;
    private Grid? _drawer;
    private readonly TextBlock _status = new();
    private readonly TextBlock _mines = new() { FontSize = 24, Foreground = Brushes.Red, FontFamily = new FontFamily("monospace") };
    private readonly TextBlock _time = new() { FontSize = 24, Foreground = Brushes.Red, FontFamily = new FontFamily("monospace") };
    private readonly Button _restart = new() { Content = "☺", FontSize = 24 };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private MinesweeperGame? _game;

    public MobileGameView()
    {
        ApplyTheme();
        Avalonia.Application.Current!.ActualThemeVariantChanged += (_, _) => ApplyTheme();
        _field.QuickRestartRequested += Restart;
        _viewport = new BoardViewport(_field);
        var toolbar = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
        var navigation = new StackPanel { Spacing = 8, Margin = new Thickness(16, 24) };
        var brand = new StackPanel { Spacing = 6, Margin = new Thickness(4, 0, 4, 12) };
        brand.Children.Add(new TextBlock { Text = "专家扫雷", FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Brushes.White });
        brand.Children.Add(new TextBlock { Text = "18 种棋盘 · 无限探索", FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#C5DDEB")) });
        navigation.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#18354B")), CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Child = brand });
        Button NavigationButton(string symbol, string label)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*") };
            row.Children.Add(new TextBlock { Text = symbol, FontSize = 20, VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#287CA7")) });
            var text = new TextBlock { Text = label, FontSize = 17, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1); row.Children.Add(text);
            return new Button { Content = row, MinHeight = 54, Padding = new Thickness(16, 12), Tag = ThemeSurface,
                Background = Brushes.White, Foreground = new SolidColorBrush(Color.Parse("#18354B")),
                BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(10),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center };
        }
        var gameItems = new StackPanel { Spacing = 4, Margin = new Thickness(16, 4, 0, 8), IsVisible = false };
        var gameEntry = NavigationButton("▶", "游戏  ▸");
        gameEntry.Click += (_, _) =>
        {
            gameItems.IsVisible = !gameItems.IsVisible;
            if (gameEntry.Content is Grid row && row.Children[1] is TextBlock text)
                text.Text = gameItems.IsVisible ? "游戏  ▾" : "游戏  ▸";
        };
        navigation.Children.Add(gameEntry);
        navigation.Children.Add(gameItems);
        var newGame = new Button { Content = "新游戏", MinHeight = 48, HorizontalAlignment = HorizontalAlignment.Stretch, Tag = ThemeSurface,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        newGame.Click += (_, _) => { CloseDrawer(); Restart(); };
        gameItems.Children.Add(newGame);
        for (int level = 0; level < 3; level++)
        {
            int selection = level;
            var item = new Button { Content = BoardNames.Difficulty(level), MinHeight = 48, HorizontalAlignment = HorizontalAlignment.Stretch, Tag = ThemeSurface,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            item.Click += (_, _) => { CloseDrawer(); _difficulty = selection; Restart(); };
            gameItems.Children.Add(item);
        }
        var boardEntry = NavigationButton("▦", "棋盘  ›");
        boardEntry.Click += (_, _) =>
        {
            CloseDrawer();
            var choices = new StackPanel { Spacing = 8, Margin = new Thickness(12, 20) };
            choices.Children.Add(new TextBlock { Text = "选择棋盘", FontSize = 24, FontWeight = FontWeight.Bold });
            foreach (var type in Enum.GetValues<BoardType>())
            {
                var label = new TextBlock { Text = (_board == type ? "✓  " : "") + BoardNames.Display(type),
                    FontSize = 18, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var item = new Button { Content = label, Padding = new Thickness(10, 8), Tag = ThemeSurface,
                    MinHeight = 52, HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
                item.SizeChanged += (_, _) =>
                {
                    // Fit the full name on one line even on narrow screens.
                    label.FontSize = 18;
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double available = Math.Max(1, item.Bounds.Width - 24);
                    if (label.DesiredSize.Width > available)
                        label.FontSize = Math.Max(12, 18 * available / label.DesiredSize.Width);
                };
                item.Click += (_, _) => { CloseModal(); _board = type; Restart(); };
                choices.Children.Add(item);
            }
            ShowModal(choices);
        };
        navigation.Children.Add(boardEntry);
        var help = NavigationButton("?", "帮助");
        help.Click += (_, _) => { CloseDrawer(); ShowModal(new StackPanel
        {
            Margin = new Thickness(24), Spacing = 16,
            Children =
            {
                new TextBlock { Text = "操作指南", FontSize = 24, FontWeight = FontWeight.Bold },
                new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                    "默认操作\n• 单击未翻开格：翻开，首击安全。\n• 长按未翻开格约500毫秒：旗帜 → 问号 → 取消。\n• 双击同一已翻开数字格：连开。\n• 长按已翻开格：预览相关未翻开邻格；松手取消，不连开。\n\n模式开关\n• 标旗模式：点击格子切换标记。\n• 连开模式：点击已翻开数字格连开。\n• 两个开关互斥；关闭后恢复默认操作。\n\n浏览棋盘\n• 单指拖动移动，双指捏合缩放（30%～500%）。\n• 拖动或缩放会取消本次翻开/长按操作。\n• 新局自动调整初始缩放并居中。\n\n新局与菜单\n• 点击笑脸或游戏菜单中的新游戏重开。\n• 胜利/失败后双击棋盘砖块重开。\n• 游戏菜单选择难度，棋盘菜单选择18种模式。\n• 红色数字分别表示剩余雷数和用时（秒）；下方显示状态和翻开进度。\n\n连开规则\n周围旗帜数等于数字时翻开其余邻格；旗帜位置错误仍可能踩雷。" }
            }
        }); };
        var aboutItem = NavigationButton("i", "关于");
        aboutItem.Click += (_, _) =>
        {
            CloseDrawer();
            if (_aboutPage != null) return;
            var view = new AboutView();
            view.OpenRepositoryRequested += () =>
            {
                var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView,
                    global::Android.Net.Uri.Parse(AboutView.RepositoryUrl));
                intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
                global::Android.App.Application.Context.StartActivity(intent);
            };
            view.OpenAuthorRequested += () =>
            {
                var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView,
                    global::Android.Net.Uri.Parse(AboutView.AuthorUrl));
                intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
                global::Android.App.Application.Context.StartActivity(intent);
            };
            ShowModal(view);
        };
        navigation.Children.Add(help); navigation.Children.Add(aboutItem);
        navigation.Children.Add(new TextBlock { Text = "Infinite75 · GPL-3.0", FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#637E90")), Margin = new Thickness(8, 20, 0, 0) });
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var menuButton = new Button { Content = "☰", FontSize = 24, MinWidth = 48, MinHeight = 48 };
        var title = new TextBlock { Text = "专家扫雷", FontSize = 20, FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) };
        Grid.SetColumn(title, 1);
        header.Children.Add(menuButton); header.Children.Add(title);
        menuButton.Click += (_, _) =>
        {
            if (_drawer != null) return;
            _field.CancelTouchGesture();
            var overlay = new Grid { Background = new SolidColorBrush(Color.Parse("#88000000")) };
            var dismiss = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            dismiss.Click += (_, _) => CloseDrawer();
            overlay.Children.Add(dismiss);
            overlay.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#EDF3F7")), Tag = ThemeSurface, CornerRadius = new CornerRadius(0, 20, 20, 0), Width = 300, MaxWidth = Math.Max(160, Bounds.Width - 32),
                HorizontalAlignment = HorizontalAlignment.Left, Child = new ScrollViewer { Content = navigation } });
            _drawer = overlay;
            _gamePanel.IsHitTestVisible = false;
            Children.Add(overlay);
            ApplyTheme();
        };
        toolbar.Children.Add(header);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        _restart.Click += (_, _) => Restart();
        actions.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#18354B")), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6), Child = _mines });
        actions.Children.Add(_restart);
        actions.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#18354B")), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6), Child = _time });
        toolbar.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = actions, Tag = ThemeSurface });
        var switches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        var flagSwitch = new ModeCheckBox("标旗模式");
        var chordSwitch = new ModeCheckBox("连开模式");
        var themeSwitch = new ModeCheckBox("深色主题");
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
        themeSwitch.IsCheckedChanged += (_, _) =>
        {
            ((Avalonia.Application)Avalonia.Application.Current!).RequestedThemeVariant =
                themeSwitch.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;
            // Theme resources propagate asynchronously; refresh our own surfaces
            // after that so text and backgrounds never end up mismatched.
            ApplyTheme();
            Dispatcher.UIThread.Post(ApplyTheme, DispatcherPriority.Background);
        };
        switches.Children.Add(flagSwitch); switches.Children.Add(chordSwitch); switches.Children.Add(themeSwitch);
        toolbar.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Child = switches, Tag = ThemeSurface });
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
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        bool dark = Avalonia.Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;
        Background = new SolidColorBrush(Color.Parse(dark ? "#101820" : "#EDF2F6"));
        // Keep layout surfaces tied to the current variant. This also updates
        // an already-open drawer when the switch is changed.
        foreach (var control in Descendants(this))
        {
            // Only surfaces explicitly marked as theme panels are recoloured, so
            // decorative frames such as the counter boxes keep their own colour.
            if (control is Border border && ReferenceEquals(border.Tag, ThemeSurface))
                border.Background = new SolidColorBrush(Color.Parse(dark ? "#202B35" : "#FFFFFF"));
            if (control is Button button && ReferenceEquals(button.Tag, ThemeSurface))
            {
                button.Background = new SolidColorBrush(Color.Parse(dark ? "#263642" : "#FFFFFF"));
                button.Foreground = new SolidColorBrush(Color.Parse(dark ? "#EDF3F7" : "#18354B"));
            }
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        if (root is Panel panel)
            foreach (var child in panel.Children)
                foreach (var item in Descendants(child)) yield return item;
        if (root is ContentControl content && content.Content is Control childContent)
            foreach (var item in Descendants(childContent)) yield return item;
        if (root is Decorator decorator && decorator.Child is Control decoratorChild)
            foreach (var item in Descendants(decoratorChild)) yield return item;
    }

    private void CloseDrawer()
    {
        if (_drawer == null) return;
        // Detach navigation content before the next opening reuses it.
        if (_drawer.Children.Count > 1 && _drawer.Children[1] is Border border && border.Child is ScrollViewer scroll)
            scroll.Content = null;
        Children.Remove(_drawer);
        _drawer = null;
        _gamePanel.IsHitTestVisible = _aboutPage == null;
    }

    private void ShowModal(Control content)
    {
        if (_aboutPage != null) return;
        _field.CancelTouchGesture();
        var close = new Button { Content = "确认", HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(16, 8, 16, 16) };
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        layout.Children.Add(new ScrollViewer { Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        Grid.SetRow(close, 1);
        layout.Children.Add(close);
        var card = new Border { Background = Brushes.White, Tag = ThemeSurface, CornerRadius = new CornerRadius(16),
            Margin = new Thickness(12, 20), MaxWidth = 480, MaxHeight = 680,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, Child = layout };
        var overlay = new Border { Background = new SolidColorBrush(Color.Parse("#88000000")), Child = card };
        _aboutPage = overlay;
        _gamePanel.IsHitTestVisible = false;
        Children.Add(overlay);
        ApplyTheme();
        close.Click += (_, _) =>
        {
            if (_aboutPage != overlay) return;
            CloseModal();
        };
    }

    private void CloseModal()
    {
        if (_aboutPage == null) return;
        Children.Remove(_aboutPage);
        _aboutPage = null;
        _gamePanel.IsHitTestVisible = _drawer == null;
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
        _status.Text = $"{BoardNames.Display(_board)} · {BoardNames.Difficulty(_difficulty)} · {state}\n已翻开 {_game.RevealedCount}/{_game.Topology.TotalCells - _game.TotalMines}";
        if (_game.Status != GameStatus.NotStarted)
            _status.Text += _game.IsLogicallySolvable ? " · 逻辑可解" : " · 普通布局（可能需猜）";
    }
}
