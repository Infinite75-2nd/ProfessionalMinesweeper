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
    RealPentagonal,

    /// <summary>Original design: organic cells from a golden-angle spiral Voronoi diagram.</summary>
    Sunflower,

    /// <summary>Original design: concentric rings twisted into a vortex, one cell per arc sector.</summary>
    Vortex,

    /// <summary>Original design: honeycomb shrunk on its lattice with triangular gaps.</summary>
    Kagome
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
