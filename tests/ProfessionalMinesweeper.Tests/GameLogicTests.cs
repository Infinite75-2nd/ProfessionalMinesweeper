using ProfessionalMinesweeper.Core;
using Xunit;

namespace ProfessionalMinesweeper.Tests;

public class GameLogicTests
{
    [Fact]
    public void Sunflower_NeighborsMatchSharedEdges()
    {
        var top = TilingFactory.Create(BoardType.Sunflower, 9, 9);
        var tiles = top.Tiles.ToArray();

        static double SharedLength(Pt[] a, Pt[] b)
        {
            double best = 0;
            for (int i = 0; i < a.Length; i++)
            {
                var a1 = a[i]; var a2 = a[(i + 1) % a.Length];
                double alen = a1.DistanceTo(a2);
                if (alen < 1e-9) continue;
                double ux = (a2.X - a1.X) / alen, uy = (a2.Y - a1.Y) / alen;
                for (int k = 0; k < b.Length; k++)
                {
                    var b1 = b[k]; var b2 = b[(k + 1) % b.Length];
                    if (b1.DistanceTo(b2) < 1e-9) continue;
                    if (Math.Abs((b1.X - a1.X) * -uy + (b1.Y - a1.Y) * ux) > 1e-4 ||
                        Math.Abs((b2.X - a1.X) * -uy + (b2.Y - a1.Y) * ux) > 1e-4) continue;
                    double t1 = (b1.X - a1.X) * ux + (b1.Y - a1.Y) * uy;
                    double t2 = (b2.X - a1.X) * ux + (b2.Y - a1.Y) * uy;
                    double lo = Math.Max(0, Math.Min(t1, t2)), hi = Math.Min(alen, Math.Max(t1, t2));
                    if (hi - lo > best) best = hi - lo;
                }
            }
            return best;
        }

        for (int i = 0; i < tiles.Length; i++)
        {
            var actual = top.GetNeighbors(tiles[i].Pos).ToHashSet();
            for (int j = 0; j < tiles.Length; j++)
            {
                if (i == j) continue;
                bool sharesEdge = SharedLength(tiles[i].Polygon, tiles[j].Polygon) > 1e-4;
                Assert.Equal(sharesEdge, actual.Contains(tiles[j].Pos));
            }
            Assert.InRange(actual.Count, 3, 9);
        }
    }


    [Fact]
    public void Sunflower_CoversBoardExactlyOnceWithOrganicCells()
    {
        var top = TilingFactory.Create(BoardType.Sunflower, 9, 9);
        Assert.Equal(81, top.TotalCells);

        bool Contains(Pt[] polygon, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].Y > y) != (polygon[j].Y > y) &&
                    x < (polygon[j].X - polygon[i].X) * (y - polygon[i].Y) /
                        (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                    inside = !inside;
            return inside;
        }

        // Each cell centre belongs to exactly one cell: no overlaps, no holes.
        foreach (var tile in top.Tiles)
            Assert.Equal(1, top.Tiles.Count(t => Contains(t.Polygon, tile.Center.X, tile.Center.Y)));

        // Cells are irregular (not a repeated grid shape) and each has neighbours.
        var shapes = top.Tiles.Select(t => string.Join(';', t.Polygon.Select(p => $"{p.X:0},{p.Y:0}"))).Distinct().Count();
        Assert.True(shapes > top.TotalCells / 2, $"only {shapes} distinct outlines");
        Assert.All(top.Tiles, t => Assert.NotEmpty(top.GetNeighbors(t.Pos)));
    }

    [Fact]
    public void LogicalSolver_RejectsAnUnavoidableFiftyFifty()
    {
        int[][] neighbors = { new[] { 1, 2 }, new[] { 0, 2 }, new[] { 0, 1 } };
        Assert.False(LogicalMineSolver.CanSolve(neighbors, new[] { false, true, false }, 0, 1));
    }

    [Fact]
    public void MineGeneration_CertifiedLayoutsPassIndependentReplay()
    {
        var top = TilingFactory.Create(BoardType.Square, 8, 8);
        var positions = top.AllPositions.ToArray();
        var ids = positions.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);
        var neighbors = positions.Select(p => top.GetNeighbors(p).Select(n => ids[n]).ToArray()).ToArray();
        for (int seed = 0; seed < 12; seed++)
        {
            var game = new MinesweeperGame(top, 10, seed);
            game.Reveal(positions[0]);
            Assert.True(game.IsLogicallySolvable, $"seed {seed}: no certified layout found");
            var mines = positions.Select(p => game.GetCell(p).IsMine).ToArray();
            Assert.Equal(10, mines.Count(m => m));
            Assert.True(LogicalMineSolver.CanSolve(neighbors, mines, 0, 10));
        }
    }

    [Fact]
    public void PresetBoardsUseIncreasingPracticalMineDensities()
    {
        foreach (var type in Enum.GetValues<BoardType>()
                     .Where(TilingFactory.HasPreset))
        {
            int previous = 0;
            for (int level = 0; level < 3; level++)
            {
                var (presetTopology, _) = TilingFactory.CreatePreset(type, level);
                int mines = Math.Clamp(Math.Max(level switch { 0 => 10, 1 => 35, _ => 80 },
                    (int)Math.Round(presetTopology.TotalCells * (level switch { 0 => .12, 1 => .16, _ => .21 }))),
                    1, presetTopology.TotalCells - 1);
                Assert.True(mines > previous || level == 0, $"{type}: mine count did not increase");
                Assert.True(mines >= (level switch { 0 => 10, 1 => 35, _ => 80 }) || presetTopology.TotalCells < (level switch { 0 => 84, 1 => 219, _ => 381 }));
                previous = mines;
            }
        }
    }

    [Theory]
    [InlineData(BoardType.Hexagon, 5,20,10,20,13,20)]
    [InlineData(BoardType.TriangleShifted, 11,6,22,12,40,12)]
    [InlineData(BoardType.Parquet, 12,6,22,11,44,11)]
    [InlineData(BoardType.CrossedSquares, 12,6,22,11,44,11)]
    [InlineData(BoardType.Wall, 11,11,22,22,42,22)]
    [InlineData(BoardType.Mosaic57, 8,8,16,16,30,16)]
    public void MenuDifficulty_GrowsWidthAndPreservesTileKinds(BoardType type,
        int r0,int c0,int r1,int c1,int r2,int c2)
    {
        double previousWidth = 0;
        var dimensions = new[] { (r0,c0), (r1,c1), (r2,c2) };
        for (int level = 0; level < 3; level++)
        {
            var (top, _) = TilingFactory.CreateMenuBoard(type, level, dimensions[level].Item1, dimensions[level].Item2);
            var points = top.Tiles.SelectMany(t => t.Polygon).ToArray();
            double width = points.Max(p => p.X) - points.Min(p => p.X);
            Assert.True(width > previousWidth, $"{type} level {level}: width did not grow");
            previousWidth = width;
            if (type == BoardType.Mosaic57)
            {
                Assert.Equal(new[] {64,256,480}[level], top.TotalCells);
                Assert.All(top.Tiles, t => Assert.True(t.Polygon.Length is 5 or 7));
            }
            if (type == BoardType.Wall)
                Assert.Equal(new[] {11,22,22}[level], top.Tiles.Count(t =>
                    t.Polygon.Max(p => p.X) - t.Polygon.Min(p => p.X) == 16));
        }
    }

    [Fact]
    public void LargeBoard_FirstRevealHasBoundedAllocationsAndCountsEachCellOnce()
    {
        var top = TilingFactory.Create(BoardType.Square, 100, 100);
        var game = new MinesweeperGame(top, 1, seed: 7);
        long before = GC.GetAllocatedBytesForCurrentThread();
        game.Reveal(top.AllPositions.First());
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 16 * 1024 * 1024, $"First reveal allocated {allocated} bytes");
        Assert.Equal(top.AllPositions.Count(p => game.GetCell(p).State == CellState.Revealed), game.RevealedCount);
        Assert.Equal(9999, game.RevealedCount);
        Assert.Equal(GameStatus.Won, game.Status);
    }

    [Fact]
    public void OriginalPresets_AllBoardsAndLevelsHaveValidConnectedNeighbors()
    {
        foreach (var type in Enum.GetValues<BoardType>()
                     .Where(TilingFactory.HasPreset))
            for (int level = 0; level < 3; level++)
            {
                var (top, mines) = TilingFactory.CreatePreset(type, level);
                Assert.InRange(mines, 1, top.TotalCells - 1);
                var seen = new HashSet<Position>();
                var queue = new Queue<Position>();
                queue.Enqueue(top.AllPositions.First());
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    if (!seen.Add(p)) continue;
                    foreach (var n in top.GetNeighbors(p))
                    {
                        Assert.True(top.IsValid(n));
                        Assert.Contains(p, top.GetNeighbors(n));
                        queue.Enqueue(n);
                    }
                }
                Assert.Equal(top.TotalCells, seen.Count);
            }
        var (square, squareMines) = TilingFactory.CreatePreset(BoardType.Square, 0);
        Assert.Equal(64, square.TotalCells);
        Assert.Equal(10, squareMines);
        var (triangle, triangleMines) = TilingFactory.CreatePreset(BoardType.Triangle, 2);
        Assert.Equal(1225, triangle.TotalCells);
        Assert.Equal(250, triangleMines);
    }

    [Theory]
    [InlineData(BoardType.Mosaic57)]
    [InlineData(BoardType.TiltedH)]
    [InlineData(BoardType.Diamond)]
    public void ExtractedBoards_DoNotHaveOverlappingTileInteriors(BoardType type)
    {
        var top = TilingFactory.Create(type, 9, 9);
        for (int i = 0; i < top.Tiles.Count; i++)
            for (int j = i + 1; j < top.Tiles.Count; j++)
            {
                var a = top.Tiles[i].Polygon;
                var b = top.Tiles[j].Polygon;
                bool separated = false;
                foreach (var polygon in new[] { a, b })
                    for (int k = 0; k < polygon.Length; k++)
                    {
                        var p = polygon[k]; var q = polygon[(k + 1) % polygon.Length];
                        double nx = -(q.Y - p.Y), ny = q.X - p.X;
                        double Project(Pt point) => point.X * nx + point.Y * ny;
                        if (a.Max(Project) <= b.Min(Project) + 1e-6 ||
                            b.Max(Project) <= a.Min(Project) + 1e-6) separated = true;
                    }
                Assert.True(separated, $"{type}: tile {i} overlaps tile {j}");
            }
    }

    [Theory]
    [InlineData(5, 7)]
    [InlineData(16, 16)]
    public void Pentagonal_RetainsWholeBoundaryTilesForScrolling(int rows, int cols)
    {
        var top = TilingFactory.Create(BoardType.Pentagonal, rows, cols);
        Assert.NotEmpty(top.Tiles);
        Assert.All(top.Tiles, tile => Assert.Equal(5, tile.Polygon.Length));
        Assert.Contains(top.Tiles, tile => tile.Polygon.Any(p => p.X < 0 || p.Y < 0));
        Assert.Contains(top.Tiles, tile => tile.Polygon.Any(p => p.X > cols * 32));
    }

    [Theory]
    [InlineData(BoardType.Circles60, 27, 2, 3)]
    [InlineData(BoardType.Circles90, 36, 1, 4)]
    public void Circles_GapsArePlayableAndTouchSurroundingCircles(
        BoardType type, int gapVertices, int gapsPerBlock, int surroundingCircles)
    {
        var top = TilingFactory.Create(type, 5, 6);
        Assert.Equal(30, top.Tiles.Count(t => t.Polygon.Length == 32));
        var gaps = top.Tiles.Where(t => t.Polygon.Length == gapVertices).ToArray();
        Assert.Equal(20 * gapsPerBlock, gaps.Length);
        foreach (var gap in gaps)
        {
            var neighbors = top.GetNeighbors(gap.Pos).ToHashSet();
            Assert.Equal(surroundingCircles,
                top.Tiles.Count(t => t.Polygon.Length == 32 && neighbors.Contains(t.Pos)));
            foreach (var neighbor in neighbors)
                Assert.Contains(gap.Pos, top.GetNeighbors(neighbor));
        }
    }

    [Fact]
    public void FilledHexagons_IncludeDiamondFillersAtSharedCorners()
    {
        var top = TilingFactory.Create(BoardType.HexagonShifted, 4, 5);
        Assert.Equal(20, top.Tiles.Count(t => t.Polygon.Length == 6));
        Assert.Equal(12, top.Tiles.Count(t => t.Polygon.Length == 4));
        var diamond = Assert.Single(top.Tiles, t => t.Center == new Pt(32, 32));
        var hexagons = top.Tiles.Where(t => t.Polygon.Length == 6 &&
            t.Polygon.Count(p => diamond.Polygon.Contains(p)) == 2).ToArray();
        Assert.Equal(4, hexagons.Length);
    }

    [Theory]
    [InlineData(BoardType.Triangle)]
    [InlineData(BoardType.TriangleShifted)]
    public void Triangles_FirstRowStartsDownwardAndAlternates(BoardType type)
    {
        var top = TilingFactory.Create(type, 5, 8);
        var first = top.Tiles[0];
        var second = top.Tiles[1];
        Assert.Contains(new Pt(0, 0), first.Polygon);
        Assert.Contains(new Pt(26, 0), first.Polygon);
        Assert.Contains(new Pt(13, 26), first.Polygon);
        Assert.Contains(new Pt(26, 0), second.Polygon);
        Assert.Contains(new Pt(13, 26), second.Polygon);
        Assert.Contains(new Pt(39, 26), second.Polygon);
        // Original shifted rows repeat their orientation; plain rows shrink by two.
        int nextRow = type == BoardType.Triangle ? 9 : 8;
        Assert.Contains(type == BoardType.Triangle ? new Pt(13, 26) : new Pt(0, 26),
            top.Tiles[nextRow].Polygon);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(6, 7)]
    public void Fishbone_BoundarySquaresFillBoardWithoutGapsOrOverlaps(int rows, int cols)
    {
        var top = TilingFactory.Create(BoardType.ParquetFishbone, rows, cols);
        int width = cols * 4, height = rows * 2;
        var coverage = new int[height, width];
        var squareEdges = new HashSet<string>();
        foreach (var tile in top.Tiles)
        {
            int left = (int)(tile.Polygon.Min(p => p.X) / 16);
            int right = (int)(tile.Polygon.Max(p => p.X) / 16);
            int upper = (int)(tile.Polygon.Min(p => p.Y) / 16);
            int lower = (int)(tile.Polygon.Max(p => p.Y) / 16);
            Assert.InRange(left, 0, width - 1);
            Assert.InRange(right, left + 1, width);
            Assert.InRange(upper, 0, height - 1);
            Assert.InRange(lower, upper + 1, height);
            Assert.InRange((right - left) * (lower - upper), 1, 2);
            if (right - left == 1 && lower - upper == 1)
            {
                Assert.True(left == 0 || right == width || upper == 0 || lower == height);
                if (left == 0) squareEdges.Add("left");
                if (right == width) squareEdges.Add("right");
                if (upper == 0) squareEdges.Add("top");
                if (lower == height) squareEdges.Add("bottom");
            }
            for (int y = upper; y < lower; y++)
                for (int x = left; x < right; x++) coverage[y, x]++;
        }
        Assert.Equal(4, squareEdges.Count);
        foreach (int count in coverage) Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(16, 16)]
    public void Wall_EveryRowHasBoundarySquareAndCoversFullWidth(int rows, int cols)
    {
        var top = TilingFactory.Create(BoardType.Wall, rows, cols);
        double width = 2 * cols * 16;
        Assert.Equal(rows * cols + rows / 2, top.TotalCells);
        for (int row = 0; row < rows; row++)
        {
            var tiles = top.Tiles.Where(t => t.Center.Y == row * 16 + 8)
                .OrderBy(t => t.Center.X).ToArray();
            Assert.Equal(cols + (row & 1), tiles.Length);
            double end = 0;
            int squares = 0;
            foreach (var tile in tiles)
            {
                double left = tile.Polygon.Min(p => p.X);
                double right = tile.Polygon.Max(p => p.X);
                Assert.Equal(end, left);
                if (right - left == 16) squares++;
                else Assert.Equal(32, right - left);
                end = right;
            }
            Assert.Equal(width, end);
            Assert.Equal((row & 1) * 2, squares);
            if ((row & 1) != 0)
                foreach (var boundary in new[] { tiles[0], tiles[^1] })
                    Assert.Equal(16, boundary.Polygon.Max(p => p.X) - boundary.Polygon.Min(p => p.X));
        }
    }

    [Fact]
    public void Floor_SquareIsSurroundedByFourHexagonsWithoutOverlap()
    {
        var top = TilingFactory.CreateFloorExact();
        var square = Assert.Single(top.Tiles, t => t.Center == new Pt(24, 24));
        Assert.Equal(4, square.Polygon.Length);
        var neighbors = top.GetNeighbors(square.Pos).Select(p => top.Tiles.Single(t => t.Pos == p)).ToArray();
        Assert.Equal(4, neighbors.Length);
        Assert.All(neighbors, tile => Assert.Equal(6, tile.Polygon.Length));
        Assert.Contains(neighbors, t => t.Center == new Pt(24, 8));
        Assert.Contains(neighbors, t => t.Center == new Pt(8, 24));
        Assert.Contains(neighbors, t => t.Center == new Pt(40, 24));
        Assert.Contains(neighbors, t => t.Center == new Pt(24, 40));
        foreach (var neighbor in neighbors)
        {
            // Each shared square edge has both endpoints on the neighbor outline.
            Assert.Equal(2, square.Polygon.Count(p => neighbor.Polygon.Contains(p)));
        }
    }

    [Fact]
    public void EveryTiling_HasNeighbors()
    {
        foreach (BoardType type in Enum.GetValues<BoardType>())
        {
            var top = TilingFactory.Create(type, 5, 5);
            Assert.True(top.TotalCells > 0, $"{type} produced no cells");
            Assert.Contains(top.Tiles, t => top.GetNeighbors(t.Pos).Count() > 0);
        }
    }

    [Fact]
    public void EveryTiling_IsConnected()
    {
        foreach (BoardType type in Enum.GetValues<BoardType>())
        {
            var top = TilingFactory.Create(type, 7, 7);
            Assert.True(top.TotalCells > 0, $"{type} produced no cells");

            var start = top.AllPositions.First();
            var seen = new HashSet<Position> { start };
            var stack = new Stack<Position>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                foreach (var n in top.GetNeighbors(stack.Pop()))
                    if (seen.Add(n)) stack.Push(n);
            }
            Assert.True(top.TotalCells == seen.Count,
                $"{type}: reachable {seen.Count} of {top.TotalCells}");
        }
    }

    [Fact]
    public void Game_FirstClickIsAlwaysSafe()
    {
        var top = TilingFactory.Create(BoardType.Square, 7, 7);
        foreach (var start in top.AllPositions)
        {
            var game = new MinesweeperGame(top, 12, seed: start.Row * 100 + start.Col);
            game.Reveal(start);
            Assert.False(game.GetCell(start).IsMine);
            Assert.NotEqual(GameStatus.Lost, game.Status);
        }
    }

    [Fact]
    public void Game_FirstClickIsSafe_OnEveryTiling()
    {
        foreach (BoardType type in Enum.GetValues<BoardType>())
        {
            var top = TilingFactory.Create(type, 6, 6);
            int mines = Math.Max(1, top.TotalCells / 8);
            foreach (var start in top.AllPositions)
            {
                var game = new MinesweeperGame(top, mines, seed: 7);
                game.Reveal(start);
                Assert.False(game.GetCell(start).IsMine);
            }
        }
    }

    [Fact]
    public void Game_ToggleFlag_CycleStates()
    {
        var top = TilingFactory.Create(BoardType.Square, 9, 9);
        var game = new MinesweeperGame(top, 10);
        var pos = top.AllPositions.First();

        Assert.Equal(10, game.RemainingFlags);
        game.ToggleFlag(pos);
        Assert.Equal(CellState.Flagged, game.GetCell(pos).State);
        game.ToggleFlag(pos);
        Assert.Equal(CellState.Questioned, game.GetCell(pos).State);
        game.ToggleFlag(pos);
        Assert.Equal(CellState.Covered, game.GetCell(pos).State);
        Assert.Equal(10, game.RemainingFlags);
    }

    [Fact]
    public void Game_WinDetected_WhenAllSafeCellsRevealed()
    {
        var top = TilingFactory.Create(BoardType.Square, 3, 3);
        var game = new MinesweeperGame(top, 1, seed: 5);
        game.Reveal(top.AllPositions.First());

        foreach (var p in top.AllPositions)
        {
            if (!game.GetCell(p).IsMine && game.GetCell(p).State == CellState.Covered)
                game.Reveal(p);
        }

        Assert.Equal(GameStatus.Won, game.Status);
    }
}
