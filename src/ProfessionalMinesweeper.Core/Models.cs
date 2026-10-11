namespace ProfessionalMinesweeper.Core;

public enum BoardType
{
    Square,
    Triangle,
    TriangleShifted,
    Hexagon,
    HexagonShifted,
    OctagonSquare,
    Diamond,
    Parquet,
    ParquetFishbone,
    Floor,
    Wall,
    CrossedSquares,
    Circles60,
    Circles90,
    Pentagonal,
    Mosaic57,
    TiltedH,
    RealPentagonal
}

public enum GameStatus
{
    NotStarted,
    InProgress,
    Won,
    Lost
}

public enum CellState
{
    Covered,
    Revealed,
    Flagged,
    Questioned
}

public readonly record struct Position(int Row, int Col)
{
    public override string ToString() => $"({Row}, {Col})";
}
