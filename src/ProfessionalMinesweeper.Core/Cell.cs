namespace ProfessionalMinesweeper.Core;

public sealed class Cell
{
    public Position Position { get; }
    public bool IsMine { get; internal set; }
    public CellState State { get; set; } = CellState.Covered;
    public int AdjacentMines { get; internal set; }
    public bool Exploded { get; internal set; }

    public Cell(Position position)
    {
        Position = position;
    }

    public void Reset()
    {
        IsMine = false;
        State = CellState.Covered;
        AdjacentMines = 0;
        Exploded = false;
    }
}
