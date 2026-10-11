using System;
using Avalonia.Controls;
using ProfessionalMinesweeper.Core;

namespace ProfessionalMinesweeper.Desktop;

public partial class CustomBoardDialog : Window
{
    public BoardType SelectedType { get; private set; } = BoardType.Square;
    public int CustomRows { get; private set; } = 16;
    public int CustomCols { get; private set; } = 30;
    public int CustomMines { get; private set; } = 40;
    public bool Confirmed { get; private set; }

    public CustomBoardDialog()
    {
        InitializeComponent();

        OkBtn.Click += (s, e) =>
        {
            SelectedType = ShapeCombo.SelectedIndex switch
            {
                1 => BoardType.Hexagon,
                2 => BoardType.Triangle,
                3 => BoardType.OctagonSquare,
                4 => BoardType.Diamond,
                5 => BoardType.Pentagonal,
                _ => BoardType.Square
            };
            CustomRows = (int)(RowsInput.Value ?? 16);
            CustomCols = (int)(ColsInput.Value ?? 30);
            int maxMines = Math.Max(1, CustomRows * CustomCols - 1);
            CustomMines = Math.Min(maxMines, (int)(MinesInput.Value ?? 40));
            Confirmed = true;
            Close();
        };

        CancelBtn.Click += (s, e) =>
        {
            Confirmed = false;
            Close();
        };
    }
}
