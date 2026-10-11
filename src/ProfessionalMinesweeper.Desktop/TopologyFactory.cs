using ProfessionalMinesweeper.Core;

namespace ProfessionalMinesweeper.Desktop;

public static class TopologyFactory
{
    public static IBoardTopology Create(BoardType type, int rows, int cols)
        => TilingFactory.Create(type, rows, cols);
}
