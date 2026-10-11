using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ProfessionalMinesweeper.Core;

namespace ProfessionalMinesweeper.Desktop;

public partial class MainWindow : Window
{
    private readonly MinefieldControl MinefieldView = new();
    private MinesweeperGame? _game;
    private readonly DispatcherTimer _timer;

    private BoardType _currentType = BoardType.Square;
    private int _difficulty = 0; // 0 beginner, 1 intermediate, 2 expert

    public MainWindow()
    {
        InitializeComponent();
        MenuAbout.Click += async (_, _) =>
        {
            var about = new AboutView();
            about.OpenAuthorRequested += () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AboutView.AuthorUrl) { UseShellExecute = true });
            await new Window { Title = "关于", Width = 480, Height = 650, Content = new ScrollViewer { Content = about }, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(this);
        };
        BoardViewport.SetBoard(MinefieldView);
        ZoomSlider.ValueChanged += (_, _) => BoardViewport.SetZoom(ZoomSlider.Value / 100);
        BoardViewport.ZoomChanged += zoom => { ZoomText.Text = $"{zoom * 100:0}%"; if (Math.Abs(ZoomSlider.Value - zoom * 100) > .01) ZoomSlider.Value = zoom * 100; };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => UpdateTimerDisplay();

        ResetButton.Click += (s, e) => RestartGame();
        MinefieldView.QuickRestartRequested += RestartGame;

        SetupMenuEvents();
        StartNewGame(BoardType.Square, 0);
    }

    private void SetupMenuEvents()
    {
        MenuNewGame.Click += (s, e) => RestartGame();
        MenuExit.Click += (s, e) => Close();

        MenuBeginner.Click += (s, e) => StartNewGame(_currentType, 0);
        MenuIntermediate.Click += (s, e) => StartNewGame(_currentType, 1);
        MenuExpert.Click += (s, e) => StartNewGame(_currentType, 2);

        RegisterBoard(MenuBoard20, BoardType.Square);
        RegisterBoard(MenuBoard21, BoardType.Hexagon);
        RegisterBoard(MenuBoard28, BoardType.HexagonShifted);
        RegisterBoard(MenuBoard22, BoardType.OctagonSquare);
        RegisterBoard(MenuBoard23, BoardType.Triangle);
        RegisterBoard(MenuBoard26, BoardType.TriangleShifted);
        RegisterBoard(MenuBoard24, BoardType.Parquet);
        RegisterBoard(MenuBoard25, BoardType.ParquetFishbone);
        RegisterBoard(MenuBoard27, BoardType.Floor);
        RegisterBoard(MenuBoard31, BoardType.Wall);
        RegisterBoard(MenuBoard29, BoardType.CrossedSquares);
        RegisterBoard(MenuBoard30, BoardType.Circles60);
        RegisterBoard(MenuBoard32, BoardType.Circles90);
        RegisterBoard(MenuBoard33, BoardType.Pentagonal);
        RegisterBoard(MenuBoard34, BoardType.Mosaic57);
        RegisterBoard(MenuBoard35, BoardType.TiltedH);
        RegisterBoard(MenuBoard36, BoardType.RealPentagonal);
        RegisterBoard(MenuBoard37, BoardType.Diamond);

        MenuCustomBoard.Click += async (s, e) =>
        {
            var dialog = new CustomBoardDialog();
            await dialog.ShowDialog(this);
            if (dialog.Confirmed)
            {
                var topology = TopologyFactory.Create(dialog.SelectedType, dialog.CustomRows, dialog.CustomCols);
                BuildGame(topology, dialog.CustomMines,
                    $"{BoardName(dialog.SelectedType)} (自定义 {dialog.CustomRows}x{dialog.CustomCols}, {dialog.CustomMines}雷)");
            }
        };

        MenuHowToPlay.Click += async (s, e) =>
        {
            var msgBox = new Window
            {
                Title = "操作指南",
                Width = 400,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = new TextBlock
                {
                    Margin = new Avalonia.Thickness(20),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Foreground = Avalonia.Media.Brushes.Black,
                    Text = "• 左键：翻开方格（首击必定安全）\n• 右键：按下即插旗 / 问号\n• 左右双键 / 中键 / 左键双击：快速连开 (Chord)，按下立即预显示\n• 游戏结束后：右键任意位置直接开新局\n• 菜单「棋盘」内含本体全部 18 种棋盘"
                }
            };
            await msgBox.ShowDialog(this);
        };
    }

    private void RegisterBoard(MenuItem item, BoardType type)
        => item.Click += (s, e) => StartNewGame(type, _difficulty);

    private static (int rows, int cols, int mines) Difficulty(BoardType type, int level)
        => type switch
        {
            BoardType.Square => level switch { 0 => (8, 8, 10), 1 => (16, 16, 40), _ => (30, 16, 99) },
            BoardType.Hexagon => level switch { 0 => (5, 20, 10), 1 => (10, 20, 45), _ => (13, 20, 95) },
            BoardType.OctagonSquare => level switch { 0 => (11, 11, 10), 1 => (23, 23, 40), _ => (41, 23, 80) },
            BoardType.Triangle => level switch { 0 => (15, 8, 10), 1 => (35, 18, 60), _ => (35, 35, 250) },
            BoardType.Parquet => level switch { 0 => (12, 6, 10), 1 => (22, 11, 35), _ => (44, 11, 80) },
            BoardType.ParquetFishbone => level switch { 0 => (11, 11, 10), 1 => (22, 22, 40), _ => (42, 22, 99) },
            BoardType.TriangleShifted => level switch { 0 => (11, 6, 10), 1 => (22, 12, 40), _ => (40, 12, 99) },
            BoardType.Floor => level switch { 0 => (11, 9, 10), 1 => (21, 17, 40), _ => (39, 17, 99) },
            BoardType.HexagonShifted => level switch { 0 => (8, 9, 8), 1 => (14, 19, 35), _ => (22, 23, 80) },
            BoardType.CrossedSquares => level switch { 0 => (12, 6, 8), 1 => (22, 11, 30), _ => (44, 11, 60) },
            BoardType.Circles60 => level switch { 0 => (11, 9, 10), 1 => (19, 21, 45), _ => (35, 21, 99) },
            BoardType.Wall => level switch { 0 => (11, 11, 10), 1 => (22, 22, 40), _ => (42, 22, 99) },
            BoardType.Circles90 => level switch { 0 => (11, 11, 10), 1 => (23, 23, 40), _ => (41, 23, 80) },
            BoardType.Pentagonal => level switch { 0 => (9, 9, 10), 1 => (18, 18, 40), _ => (33, 18, 90) },
            BoardType.Mosaic57 => level switch { 0 => (8, 8, 10), 1 => (16, 16, 40), _ => (30, 16, 99) },
            BoardType.TiltedH => level switch { 0 => (8, 8, 10), 1 => (16, 16, 40), _ => (30, 16, 95) },
            BoardType.RealPentagonal => level switch { 0 => (11, 6, 10), 1 => (23, 12, 40), _ => (43, 12, 99) },
            BoardType.Diamond => level switch { 0 => (9, 9, 10), 1 => (17, 17, 35), _ => (33, 17, 90) },
            _ => (8, 8, 10)
        };

    private void StartNewGame(BoardType type, int difficulty)
    {
        _currentType = type;
        _difficulty = difficulty;
        var (rows, cols, mines) = Difficulty(type, difficulty);
        var (topology, _) = TilingFactory.CreateMenuBoard(type, difficulty, rows, cols);
        mines = CalculateMines(topology.TotalCells, difficulty);
        string diffName = difficulty switch { 0 => "初级", 1 => "中级", _ => "高级" };
        BuildGame(topology, mines, $"{BoardName(type)} ({diffName})");
    }

    private static int CalculateMines(int totalCells, int difficulty)
    {
        double density = difficulty switch
        {
            0 => 0.12,
            1 => 0.16,
            _ => 0.21
        };
        int minimum = difficulty switch
        {
            0 => 10,
            1 => 35,
            _ => 80
        };
        return Math.Clamp(Math.Max(minimum, (int)Math.Round(totalCells * density)), 1, totalCells - 1);
    }

    private void BuildGame(IBoardTopology topology, int mines, string modeName)
    {
        int maxMines = Math.Max(1, topology.TotalCells - 1);
        mines = Math.Clamp(mines, 1, maxMines);

        ModeLabelText.Text = $"模式: {modeName}";

        if (_game != null)
        {
            _game.StateChanged -= OnGameStateChanged;
            _game.GameOver -= OnGameOver;
        }

        _game = new MinesweeperGame(topology, mines);
        _game.StateChanged += OnGameStateChanged;
        _game.GameOver += OnGameOver;

        MinefieldView.Game = _game;
        MinefieldView.Width = double.NaN;
        MinefieldView.Height = double.NaN;
        BoardViewport.ResetView();
        RestartGame();
    }

    private void RestartGame()
    {
        if (_game == null) return;
        _game.Restart();
        _timer.Stop();
        ResetButton.Content = "😊";
        UpdateStatusDisplay();
        UpdateTimerDisplay();
    }

    private void OnGameStateChanged()
    {
        if (_game == null) return;
        if (_game.Status == GameStatus.InProgress && !_timer.IsEnabled) _timer.Start();
        UpdateStatusDisplay();
        UpdateTimerDisplay();
    }

    private void OnGameOver(GameStatus status)
    {
        _timer.Stop();
        ResetButton.Content = status == GameStatus.Won ? "😎" : "😵";
    }

    private void UpdateStatusDisplay()
    {
        if (_game == null) return;
        MinesCountText.Text = $"{Math.Max(0, _game.RemainingFlags):D3}";
        if (_game.Status == GameStatus.InProgress) ResetButton.Content = "😮";
        else if (_game.Status == GameStatus.NotStarted) ResetButton.Content = "😊";
    }

    private void UpdateTimerDisplay()
    {
        if (_game == null) return;
        int seconds = (int)_game.ElapsedTime.TotalSeconds;
        TimerText.Text = $"{Math.Min(999, seconds):D3}";
    }

    private static string BoardName(BoardType type) => type switch
    {
        BoardType.Square => "Classical",
        BoardType.Hexagon => "Hexagonal",
        BoardType.HexagonShifted => "Hexagonal Shifted",
        BoardType.OctagonSquare => "Octagonal",
        BoardType.Triangle => "Triangular",
        BoardType.TriangleShifted => "Triangular Shifted",
        BoardType.Parquet => "Parquet",
        BoardType.ParquetFishbone => "Parquet Fish Bone",
        BoardType.Floor => "Floor",
        BoardType.Wall => "Wall",
        BoardType.CrossedSquares => "Crossed Squares",
        BoardType.Circles60 => "60° Circles",
        BoardType.Circles90 => "90° Circles",
        BoardType.Pentagonal => "Pentagonal",
        BoardType.Mosaic57 => "Mosaic 5,7",
        BoardType.TiltedH => "Tilted H",
        BoardType.RealPentagonal => "Real Pentagonal",
        BoardType.Diamond => "Diamond",
        _ => type.ToString()
    };
}
