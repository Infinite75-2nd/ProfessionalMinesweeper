using System;
using System.Collections.Generic;

namespace ProfessionalMinesweeper.Core;

/// <summary>
/// Builds a real tessellation for every board type. Adjacency is always derived
/// from the generated polygons, so each board is a genuine tiling rather than a
/// grid with a cosmetic shape.
/// </summary>
public static class TilingFactory
{
    public static (int rows, int cols) GetPresetDimensions(BoardType type, int level)
    {
        var preset = Array.Find(OriginalPresets.All, p => p.Type == type && p.Level == level);
        // Boards added after the original have no captured dimensions; fall back to
        // the same sizes the desktop difficulty table uses so both ends agree.
        if (preset is null)
            return level switch { 0 => (9, 9), 1 => (16, 16), _ => (16, 30) };
        // The procedural triangular generator takes the number of triangle rows.
        return (type == BoardType.Triangle ? preset.Columns : preset.Rows, preset.Columns);
    }

    public static (IBoardTopology topology, int mines) CreatePreset(BoardType type, int level)
    {
        var preset = Array.Find(OriginalPresets.All, p => p.Type == type && p.Level == level)
            ?? throw new ArgumentOutOfRangeException(nameof(level));
        var polygons = new List<Pt[]>();
        var neighbors = new List<int[]>();
        foreach (var cell in preset.Cells)
        {
            var shape = TilingData.Shapes[cell.Sprite];
            var outline = OffsetConvex(shape, 1);
            for (int i = 0; i < outline.Length; i++)
                outline[i] = new Pt(cell.X + outline[i].X, cell.Y + outline[i].Y);
            polygons.Add(outline);
            neighbors.Add(cell.Neighbors);
        }
        WeldVertices(polygons, 3);
        return (new PolygonTilingTopology(type, polygons, neighbors), preset.Mines);
    }

    public static IBoardTopology Create(BoardType type, int rows, int cols)
    {
        if (type == BoardType.Mosaic57) return CreateMosaicLayout();
        if (type == BoardType.RealPentagonal) return CreateRealPentagonalLayout();
        if (type == BoardType.TiltedH) return CreateTiltedHLayout();
        if (type == BoardType.Diamond) return CreateDiamondLayout();
        if (type == BoardType.Floor) return CreateFloorExact();
        return CreateProcedural(type, rows, cols);
    }

    /// <summary>True when captured original presets exist (all 18 original boards).</summary>
    public static bool HasPreset(BoardType type) => Array.Exists(OriginalPresets.All, p => p.Type == type);

    // Menu presets use captured positions, but retain the repaired vector shapes.
    public static (IBoardTopology topology, int mines) CreateMenuBoard(BoardType type, int level, int rows, int cols)
    {
        var preset = Array.Find(OriginalPresets.All, p => p.Type == type && p.Level == level);
        // Boards designed after the original (such as Sunflower) have no captured
        // preset; generate them procedurally instead of failing.
        if (preset is null)
        {
            var fresh = Create(type, rows, cols);
            return (fresh, Math.Max(1, fresh.TotalCells / 8));
        }
        if (type is BoardType.Mosaic57 or BoardType.RealPentagonal or BoardType.TiltedH or BoardType.Diamond or BoardType.Floor)
        {
            var reference = Create(type, rows, cols);
            var source = TilingData.Cells[DataName(type)];
            var shapes = new Dictionary<int, Pt[]>();
            for (int i = 0; i < source.Length; i++)
            {
                var (sprite, vertical, horizontal) = source[i];
                if (shapes.ContainsKey(sprite)) continue;
                var polygon = reference.Tiles[i].Polygon;
                var local = new Pt[polygon.Length];
                for (int k = 0; k < local.Length; k++) local[k] = new Pt(polygon[k].X - horizontal, polygon[k].Y - vertical);
                shapes.Add(sprite, local);
            }
            return (BuildCapturedLayout(type, preset, shapes), preset.Mines);
        }
        if (type == BoardType.Wall || type == BoardType.Parquet)
        {
            var shapes = new Dictionary<int, Pt[]>
            {
                [0x421490] = new[] { new Pt(0,0), new Pt(16,0), new Pt(16,16), new Pt(0,16) },
                [0x421970] = new[] { new Pt(0,0), new Pt(32,0), new Pt(32,16), new Pt(0,16) },
                [0x421910] = new[] { new Pt(0,0), new Pt(16,0), new Pt(16,32), new Pt(0,32) }
            };
            return (BuildCapturedLayout(type, preset, shapes), preset.Mines);
        }
        if (type == BoardType.Hexagon)
            return (CreateProcedural(type, 2 * preset.Rows - 1, 2 * preset.Rows - 1), preset.Mines);
        if (type is BoardType.TriangleShifted or BoardType.CrossedSquares)
            return (CreateProcedural(type, cols, rows), preset.Mines);
        return (Create(type, rows, cols), preset.Mines);
    }

    private static IBoardTopology BuildCapturedLayout(BoardType type, OriginalPreset preset, Dictionary<int, Pt[]> shapes)
    {
        var polygons = new List<Pt[]>();
        foreach (var cell in preset.Cells)
        {
            var shape = shapes[cell.Sprite];
            var polygon = new Pt[shape.Length];
            for (int i = 0; i < shape.Length; i++) polygon[i] = new Pt(cell.X + shape[i].X, cell.Y + shape[i].Y);
            polygons.Add(polygon);
        }
        return new PolygonTilingTopology(type, polygons, AdjacencyMode.EdgesAndCorners);
    }

    private static string DataName(BoardType t) => t switch
    {
        BoardType.Square => "Classical",
        BoardType.Hexagon => "Hexagonal",
        BoardType.HexagonShifted => "HexShifted",
        BoardType.OctagonSquare => "Octagonal",
        BoardType.Triangle => "Triangular",
        BoardType.TriangleShifted => "TriShifted",
        BoardType.Parquet => "Parquet",
        BoardType.ParquetFishbone => "Fishbone",
        BoardType.Floor => "Floor",
        BoardType.Wall => "Wall",
        BoardType.CrossedSquares => "CrossedSquares",
        BoardType.Circles60 => "Circles60",
        BoardType.Circles90 => "Circles90",
        BoardType.Pentagonal => "Pentagonal",
        BoardType.Mosaic57 => "Mosaic57",
        BoardType.TiltedH => "TiltedH",
        BoardType.RealPentagonal => "RealPentagonal",
        BoardType.Diamond => "Diamond",
        _ => "Classical"
    };

    private static IBoardTopology CreateTiltedHLayout()
    {
        var polygons = new List<Pt[]>();
        foreach (var (sprite, vertical, horizontal) in TilingData.Cells["TiltedH"])
        {
            // Original layout repeats every 36 pixels, with corners on a
            // 12-pixel lattice. Fill-pixel contours round these corners inward.
            // Restore shared lattice coordinates rather than offsetting fills.
            Pt[] polygon = sprite switch
            {
                0x421FD0 => new[] { new Pt(0, 0), new Pt(12, 0), new Pt(24, 12), new Pt(12, 24), new Pt(0, 12) },
                0x422030 => new[] { new Pt(0, 12), new Pt(12, 0), new Pt(24, 12), new Pt(24, 24), new Pt(12, 24) },
                0x422090 => new[] { new Pt(0, 0), new Pt(24, 0), new Pt(24, 24) },
                0x4220F0 => new[] { new Pt(0, 0), new Pt(24, 24), new Pt(0, 24) },
                _ => throw new InvalidOperationException($"Unknown Tilted H sprite: {sprite:X}")
            };
            for (int i = 0; i < polygon.Length; i++)
                polygon[i] = new Pt(horizontal + polygon[i].X, vertical + polygon[i].Y);
            polygons.Add(polygon);
        }
        return new PolygonTilingTopology(BoardType.TiltedH, polygons, AdjacencyMode.EdgesAndCorners);
    }

    private static IBoardTopology CreateDiamondLayout()
    {
        var polygons = new List<Pt[]>();
        foreach (var (sprite, vertical, horizontal) in TilingData.Cells["Diamond"])
        {
            // All original Diamond corners lie on the 13-pixel lattice.
            // Preserve the six distinct tile orientations and their placement.
            Pt[] polygon = sprite switch
            {
                0x4222D0 => new[] { new Pt(0, 13), new Pt(26, 0), new Pt(26, 26) },
                0x422330 => new[] { new Pt(0, 0), new Pt(26, 13), new Pt(0, 26) },
                0x422390 => new[] { new Pt(0, 13), new Pt(26, 0), new Pt(39, 26), new Pt(13, 39) },
                0x4223F0 => new[] { new Pt(0, 26), new Pt(13, 0), new Pt(39, 13), new Pt(26, 39) },
                0x421850 => new[] { new Pt(13, 0), new Pt(26, 26), new Pt(0, 26) },
                0x4218B0 => new[] { new Pt(0, 0), new Pt(26, 0), new Pt(13, 26) },
                _ => throw new InvalidOperationException($"Unknown Diamond sprite: {sprite:X}")
            };
            for (int i = 0; i < polygon.Length; i++)
                polygon[i] = new Pt(horizontal + polygon[i].X, vertical + polygon[i].Y);
            polygons.Add(polygon);
        }
        return new PolygonTilingTopology(BoardType.Diamond, polygons, AdjacencyMode.EdgesAndCorners);
    }

    private static IBoardTopology CreateMosaicLayout()
    {
        var polygons = new List<Pt[]>();
        foreach (var (sprite, vertical, horizontal) in TilingData.Cells["Mosaic57"])
        {
            // Five- and seven-sided tiles share corners on a 6-pixel lattice.
            // Reconstruct the complete boundary, not the inset sprite fill.
            Pt[] polygon = sprite switch
            {
                0x421E50 => new[] { new Pt(0, 6), new Pt(12, 0), new Pt(24, 6), new Pt(18, 18), new Pt(6, 18) },
                0x421EB0 => new[] { new Pt(0, 12), new Pt(6, 0), new Pt(18, 0), new Pt(24, 12), new Pt(12, 18) },
                0x421F10 => new[] { new Pt(0, 18), new Pt(6, 6), new Pt(18, 0), new Pt(30, 6), new Pt(36, 18), new Pt(24, 24), new Pt(12, 24) },
                0x421F70 => new[] { new Pt(0, 6), new Pt(12, 0), new Pt(24, 0), new Pt(36, 6), new Pt(30, 18), new Pt(18, 24), new Pt(6, 18) },
                _ => throw new InvalidOperationException($"Unknown Mosaic 5,7 sprite: {sprite:X}")
            };
            for (int i = 0; i < polygon.Length; i++)
                polygon[i] = new Pt(horizontal + polygon[i].X, vertical + polygon[i].Y);
            polygons.Add(polygon);
        }
        return new PolygonTilingTopology(BoardType.Mosaic57, polygons, AdjacencyMode.EdgesAndCorners);
    }

    private static IBoardTopology CreateRealPentagonalLayout()
    {
        var polygons = new List<Pt[]>();
        foreach (var (sprite, vertical, horizontal) in TilingData.Cells["RealPentagonal"])
        {
            // Original placement uses 28-pixel periods and 7-pixel offsets.
            // The four pentagon orientations must share these exact corners;
            // expanding the inset bitmap fill cannot recover the shared edges.
            Pt[] polygon = sprite switch
            {
                0x422150 => new[] { new Pt(0, 7), new Pt(14, 0), new Pt(28, 7), new Pt(21, 21), new Pt(7, 21) },
                0x4221B0 => new[] { new Pt(0, 14), new Pt(7, 0), new Pt(21, 0), new Pt(28, 14), new Pt(14, 21) },
                0x422210 => new[] { new Pt(0, 14), new Pt(7, 0), new Pt(21, 7), new Pt(21, 21), new Pt(7, 28) },
                0x422270 => new[] { new Pt(0, 7), new Pt(14, 0), new Pt(21, 14), new Pt(14, 28), new Pt(0, 21) },
                _ => throw new InvalidOperationException($"Unknown Real Pentagonal sprite: {sprite:X}")
            };
            for (int i = 0; i < polygon.Length; i++)
                polygon[i] = new Pt(horizontal + polygon[i].X, vertical + polygon[i].Y);
            polygons.Add(polygon);
        }
        return new PolygonTilingTopology(BoardType.RealPentagonal, polygons, AdjacencyMode.EdgesAndCorners);
    }

    public static IBoardTopology CreateFloorExact()
    {
        var cells = FloorData.Cells;
        var polys = new List<Pt[]>(cells.Length);
        var neighborLists = new List<int[]>(cells.Length);

        foreach (var (sprite, x, y, neighbors) in cells)
        {
            // KVD_PLJ contains a square and two elongated hexagons.
            // The dump named +0x20 "x" and +0x1c "y", but the original
            // drawing routine uses +0x1c horizontally and +0x20 vertically.
            Pt[] outline = sprite switch
            {
                0x421550 => new[] { new Pt(8, 0), new Pt(24, 0), new Pt(32, 8), new Pt(24, 16), new Pt(8, 16), new Pt(0, 8) },
                0x4214F0 => new[] { new Pt(8, 0), new Pt(16, 8), new Pt(16, 24), new Pt(8, 32), new Pt(0, 24), new Pt(0, 8) },
                0x421490 => new[] { new Pt(0, 0), new Pt(16, 0), new Pt(16, 16), new Pt(0, 16) },
                _ => throw new InvalidOperationException($"Unknown Floor sprite: {sprite:X}")
            };
            for (int i = 0; i < outline.Length; i++)
                outline[i] = new Pt(y + outline[i].X, x + outline[i].Y);
            polys.Add(outline);
            neighborLists.Add(neighbors);
        }

        return new PolygonTilingTopology(BoardType.Floor, polys, neighborLists);
    }


    /// <summary>
    /// Builds the tiling exactly as the original does, from the cell layout read
    /// out of profmine.exe's memory (sprite + position per cell) and the sprite
    /// polygons extracted from the *_PLJ atlases.
    /// </summary>
    public static IBoardTopology CreateFromData(BoardType type)
    {
        var name = DataName(type);
        if (!TilingData.Cells.TryGetValue(name, out var cells))
            return CreateProcedural(BoardType.Square, 9, 9);

        var list = new List<Pt[]>(cells.Length);
        foreach (var (sprite, x, y) in cells)
        {
            if (!TilingData.Shapes.TryGetValue(sprite, out var shape) || shape.Length < 3) continue;
            if (!TilingData.Sizes.TryGetValue(sprite, out var sz) || sz.w <= 0 || sz.h <= 0) continue;

            // Fill outlines retain the original slopes. A bounding-box stretch
            // changes those slopes and makes neighboring tiles overlap.
            var outline = OffsetConvex(shape, 1);
            var poly = new Pt[shape.Length];
            for (int k = 0; k < shape.Length; k++)
                poly[k] = new Pt(y + outline[k].X, x + outline[k].Y);
            list.Add(poly);
        }
        WeldVertices(list, 3);
        return new PolygonTilingTopology(type, list, AdjacencyMode.EdgesAndCorners);
    }

    // Sprite fill extraction rounds the two sides of a shared corner differently.
    // Reconcile those corners instead of expanding whole tiles into each other.
    private static void WeldVertices(List<Pt[]> polygons, double tolerance)
    {
        var vertices = new List<(int polygon, int vertex, Pt point)>();
        for (int i = 0; i < polygons.Count; i++)
            for (int j = 0; j < polygons[i].Length; j++)
                vertices.Add((i, j, polygons[i][j]));
        var used = new bool[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            if (used[i]) continue;
            var group = new List<int> { i };
            used[i] = true;
            for (int j = i + 1; j < vertices.Count; j++)
                if (!used[j] && vertices[j].polygon != vertices[i].polygon &&
                    vertices[i].point.DistanceTo(vertices[j].point) <= tolerance)
                {
                    group.Add(j);
                    used[j] = true;
                }
            double x = 0, y = 0;
            foreach (int j in group) { x += vertices[j].point.X; y += vertices[j].point.Y; }
            var shared = new Pt(x / group.Count, y / group.Count);
            foreach (int j in group)
                polygons[vertices[j].polygon][vertices[j].vertex] = shared;
        }
    }

    // Offsets each edge of a convex polygon outward by d (vertices are the
    // intersections of consecutive offset edges), so neighbouring tiles meet at
    // their true shared edge instead of leaving the sprite's border as a gap.
    private static Pt[] OffsetConvex(Pt[] shape, double d)
    {
        int n = shape.Length;
        double cx = 0, cy = 0;
        foreach (var v in shape) { cx += v.X; cy += v.Y; }
        cx /= n; cy /= n;
        var lines = new (double a, double b, double c)[n];
        for (int i = 0; i < n; i++)
        {
            var p = shape[i]; var q = shape[(i + 1) % n];
            double ex = q.X - p.X, ey = q.Y - p.Y;
            double len = Math.Sqrt(ex * ex + ey * ey);
            if (len < 1e-9) len = 1e-9;
            double nx = ey / len, ny = -ex / len;
            double mx = (p.X + q.X) / 2 - cx, my = (p.Y + q.Y) / 2 - cy;
            if (nx * mx + ny * my < 0) { nx = -nx; ny = -ny; } // point away from centre
            lines[i] = (nx, ny, nx * p.X + ny * p.Y + d);
        }
        var res = new Pt[n];
        for (int i = 0; i < n; i++)
        {
            var l1 = lines[(i - 1 + n) % n];
            var l2 = lines[i];
            double det = l1.a * l2.b - l2.a * l1.b;
            if (Math.Abs(det) < 1e-9) { res[i] = new Pt(shape[i].X, shape[i].Y); continue; }
            double px = (l1.c * l2.b - l2.c * l1.b) / det;
            double py = (l1.a * l2.c - l2.a * l1.c) / det;
            res[i] = new Pt(px, py);
        }
        return res;
    }

    public static IBoardTopology CreateProcedural(BoardType type, int rows, int cols)
    {
        rows = Math.Max(2, rows);
        cols = Math.Max(2, cols);
        return type switch
        {
            BoardType.Square => Build(BoardType.Square, Square(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.CrossedSquares => Build(BoardType.CrossedSquares, CrossedSquares(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.Diamond => Build(BoardType.Diamond, Diamond(rows, cols), AdjacencyMode.Edges),
            BoardType.Triangle => Build(BoardType.Triangle, Triangle(rows, cols, false), AdjacencyMode.EdgesAndCorners),
            BoardType.TriangleShifted => Build(BoardType.TriangleShifted, Triangle(rows, cols, true), AdjacencyMode.EdgesAndCorners),
            BoardType.Hexagon => Build(BoardType.Hexagon, Hexagon(rows, cols, false), AdjacencyMode.Edges),
            BoardType.HexagonShifted => Build(BoardType.HexagonShifted, FilledHexagons(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.OctagonSquare => Build(BoardType.OctagonSquare, Octagon(rows, cols), AdjacencyMode.Edges),
            BoardType.Floor => Build(BoardType.Floor, RunningBond(rows, cols, horizontal: true), AdjacencyMode.EdgesAndCorners),
            BoardType.Wall => Build(BoardType.Wall, Wall(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.Parquet => Build(BoardType.Parquet, BasketWeave(rows, cols, 2), AdjacencyMode.EdgesAndCorners),
            BoardType.ParquetFishbone => Build(BoardType.ParquetFishbone, Herringbone(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.Circles60 => Build(BoardType.Circles60, Circles(rows, cols, hexPack: true), AdjacencyMode.EdgesAndCorners),
            BoardType.Circles90 => Build(BoardType.Circles90, Circles(rows, cols, hexPack: false), AdjacencyMode.EdgesAndCorners),
            BoardType.Pentagonal => Build(BoardType.Pentagonal, Cairo(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.RealPentagonal => Build(BoardType.RealPentagonal, Floret(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.Mosaic57 => Build(BoardType.Mosaic57, MosaicExact(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.TiltedH => Build(BoardType.TiltedH, TiltedH(rows, cols), AdjacencyMode.EdgesAndCorners),
            BoardType.Sunflower => new PolygonTilingTopology(BoardType.Sunflower,
                // Voronoi edges are exactly collinear, so a tight tolerance only
                // rejects point-touching cells while keeping genuinely short edges.
                SunflowerVoronoi(rows, cols), AdjacencyMode.Edges, 1e-4),
            BoardType.Vortex => new PolygonTilingTopology(BoardType.Vortex,
                Vortex(rows, cols), AdjacencyMode.Edges, 1e-3),
            BoardType.Kagome => new PolygonTilingTopology(BoardType.Kagome,
                Kagome(rows, cols), AdjacencyMode.EdgesAndCorners, 1e-3),
            _ => Build(BoardType.Square, Square(rows, cols), AdjacencyMode.EdgesAndCorners)
        };
    }

    private static IBoardTopology Build(BoardType type, List<Pt[]> polys, AdjacencyMode mode)
        => new PolygonTilingTopology(type, polys, mode);

    // 13. Sunflower: an original tiling built from a golden-angle spiral of sites.
    //
    // Cells are Voronoi regions of that point set, clipped to the board rectangle, so
    // the plane is covered exactly once by construction -- there are no hand-tuned
    // offsets that could leave seams, and every cell is a different organic polygon.
    private static List<Pt[]> SunflowerVoronoi(int rows, int cols)
    {
        const double cell = 34;
        double width = Math.Max(2, cols) * cell;
        double height = Math.Max(2, rows) * cell;
        int count = Math.Clamp(rows * cols, 12, 600);

        // Vogel / sunflower distribution: even area coverage with a visible spiral.
        const double goldenAngle = 2.39996322972865332;
        var sites = new Pt[count];
        for (int i = 0; i < count; i++)
        {
            double r = Math.Sqrt((i + 0.5) / count);
            double a = i * goldenAngle;
            sites[i] = new Pt(width * 0.5 + Math.Cos(a) * r * width * 0.5,
                              height * 0.5 + Math.Sin(a) * r * height * 0.5);
        }

        var cells = new List<Pt[]>(count);
        foreach (var site in sites)
        {
            var polygon = new List<Pt>
            {
                new(0, 0), new(width, 0), new(width, height), new(0, height)
            };
            foreach (var other in sites)
            {
                if (other.Equals(site)) continue;
                polygon = ClipHalfPlane(polygon, site, other);
                if (polygon.Count < 3) break;
            }
            if (polygon.Count >= 3) cells.Add(polygon.ToArray());
        }
        return cells;
    }

    // Keeps the part of a convex polygon closer to `keep` than to `other`.
    private static List<Pt> ClipHalfPlane(List<Pt> polygon, Pt keep, Pt other)
    {
        double nx = other.X - keep.X, ny = other.Y - keep.Y;
        double mx = (keep.X + other.X) * 0.5, my = (keep.Y + other.Y) * 0.5;
        double Side(Pt p) => (p.X - mx) * nx + (p.Y - my) * ny;

        var result = new List<Pt>(polygon.Count + 1);
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            double da = Side(a), db = Side(b);
            if (da <= 0) result.Add(a);
            if ((da < 0 && db > 0) || (da > 0 && db < 0))
            {
                double t = da / (da - db);
                result.Add(new Pt(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
            }
        }
        return result;
    }

    // 14. Vortex: concentric rings, each rotated a little further than the one
    // inside it so the board reads as a spiral.
    //
    // All boundaries live on one angular grid of 720 steps, and every ring's cell
    // count divides that grid. A circle shared by two rings is therefore carved by
    // integer indices only, so adjacent rings reuse identical arcs and the disk is
    // covered exactly once with no floating-point seams.
    private static List<Pt[]> Vortex(int rows, int cols)
    {
        const int Grid = 720;
        const int TwistSteps = 12;   // rotation per ring, in grid steps
        const double dr = 30;

        // Cell counts per ring: growing, and each divides Grid so span stays integral.
        int[] ringCounts = { 6, 12, 18, 24, 30, 36, 48, 60, 72, 90, 120, 144 };

        int target = Math.Clamp(rows * cols, 12, 900);
        int rings = 1, total = 1;
        while (rings <= ringCounts.Length)
        {
            total += ringCounts[rings - 1];
            if (total >= target || rings == ringCounts.Length) break;
            rings++;
        }

        double step = 2 * Math.PI / Grid;
        var cells = new List<Pt[]>(total);

        // Centre disk: every grid point of the inner circle.
        var centre = new Pt[Grid];
        for (int k = 0; k < Grid; k++) centre[k] = OnCircle(dr, k * step);
        cells.Add(centre);

        for (int r = 1; r <= rings; r++)
        {
            int count = ringCounts[r - 1];
            int span = Grid / count;
            int twist = (r * TwistSteps) % Grid;

            for (int j = 0; j < count; j++)
            {
                int startIndex = twist + j * span;
                int endIndex = startIndex + span;

                var polygon = new List<Pt>(span * 4);
                for (int i = startIndex; i <= endIndex; i++)
                    polygon.Add(OnCircle(r * dr, i * step));
                for (int i = endIndex; i >= startIndex; i--)
                    polygon.Add(OnCircle((r + 1) * dr, i * step));

                cells.Add(polygon.ToArray());
            }
        }
        return cells;
    }

    private static Pt OnCircle(double radius, double angle) =>
        new(Math.Cos(angle) * radius, Math.Sin(angle) * radius);

    // 15. Kagome: the medial honeycomb. Each hexagon is replaced by the polygon
    // through the midpoints of its edges; the three midpoints meeting at every
    // original vertex bound an equilateral triangle. Triangles and hexagons share
    // whole edges, so the patch is covered exactly once.
    private static List<Pt[]> Kagome(int rows, int cols)
    {
        const double s = 26;                    // hexagon circumradius
        double h = s * Math.Sqrt(3) / 2;
        var cells = new List<Pt[]>();
        var gaps = new Dictionary<(long, long), List<Pt>>();

        for (int j = -1; j <= rows; j++)
            for (int i = -1; i <= cols; i++)
            {
                double cx = i * s * Math.Sqrt(3) + (j & 1) * s * Math.Sqrt(3) / 2;
                double cy = j * s * 1.5;

                var corner = new Pt[6];
                for (int k = 0; k < 6; k++)
                {
                    double angle = Math.PI / 6 + k * Math.PI / 3;
                    corner[k] = new Pt(cx + s * Math.Cos(angle), cy + s * Math.Sin(angle));
                }

                // Medial hexagon: one vertex per original edge midpoint.
                var tile = new Pt[6];
                for (int k = 0; k < 6; k++)
                {
                    var a = corner[k];
                    var b = corner[(k + 1) % 6];
                    tile[k] = new Pt((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                }
                cells.Add(tile);

                // Two edge midpoints meet at each corner; the third belongs to the
                // neighbouring hexagon and joins through the shared vertex key.
                for (int k = 0; k < 6; k++)
                {
                    var p = corner[k];
                    var key = (Key(p.X), Key(p.Y));
                    if (!gaps.TryGetValue(key, out var list)) gaps[key] = list = new List<Pt>(3);
                    AddUnique(list, tile[(k + 5) % 6]);
                    AddUnique(list, tile[k]);
                }
            }

        foreach (var list in gaps.Values)
            if (list.Count == 3) cells.Add(list.ToArray());

        return cells;
    }

    private static void AddUnique(List<Pt> list, Pt point)
    {
        foreach (var existing in list)
            if (existing.DistanceTo(point) < 1e-6) return;
        list.Add(point);
    }

    private static long Key(double value) => (long)Math.Round(value * 1000);
    private static void Add(List<Pt[]> list, params (double x, double y)[] pts)
    {
        var poly = new Pt[pts.Length];
        for (int i = 0; i < pts.Length; i++) poly[i] = new Pt(pts[i].x, pts[i].y);
        list.Add(poly);
    }

    private static (double x, double y) Rot(double x, double y, double deg)
    {
        double r = deg * Math.PI / 180.0;
        double c = Math.Cos(r), s = Math.Sin(r);
        return (x * c - y * s, x * s + y * c);
    }

    // 1. Classical / Crossed squares.
    private static List<Pt[]> Square(int rows, int cols)
    {
        const double s = 32, o = 0;
        var list = new List<Pt[]>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                double x = o + c * s, y = o + r * s;
                Add(list, (x, y), (x + s, y), (x + s, y + s), (x, y + s));
            }
        return list;
    }

    // 2. Diamond: squares rotated 45deg on a checkerboard lattice.
    private static List<Pt[]> Diamond(int rows, int cols)
    {
        const double h = 18, o = h;
        var list = new List<Pt[]>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (((r + c) & 1) != 0) continue;
                double cx = o + c * h, cy = o + r * h;
                Add(list, (cx, cy - h), (cx + h, cy), (cx, cy + h), (cx - h, cy));
            }
        return list;
    }

    // 3. Triangle. Plain = big downward-triangle outline; shifted = rectangular
    // block of up/down triangles (matches the original boards 23 and 26).
    private static List<Pt[]> Triangle(int rows, int cols, bool shifted)
    {
        const double side = 26, h = 26;
        var list = new List<Pt[]>();
        if (shifted)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    double x = c * side / 2.0, y = r * h;
                    bool up = (c & 1) != 0;
                    if (up) Add(list, (x + side / 2.0, y), (x + side, y + h), (x, y + h));
                    else Add(list, (x, y), (x + side, y), (x + side / 2.0, y + h));
                }
            return list;
        }
        for (int r = 0; r < rows; r++)
        {
            int count = 2 * (rows - r) - 1;
            for (int c = 0; c < count; c++)
            {
                double x = (c + r) * side / 2.0, y = r * h;
                bool up = (c & 1) != 0;
                if (up) Add(list, (x + side / 2.0, y), (x + side, y + h), (x, y + h));
                else Add(list, (x, y), (x + side, y), (x + side / 2.0, y + h));
            }
        }
        return list;
    }

    // 4. Hexagon (pointy-top). Plain = big hexagon outline; shifted = rectangular
    // block (matches the original boards 21 and 28).
    private static List<Pt[]> Hexagon(int rows, int cols, bool shifted)
    {
        const double R = 19;
        double w = Math.Sqrt(3) * R, v = 1.5 * R, o = R;
        var list = new List<Pt[]>();
        int mid = (rows - 1) / 2;
        for (int r = 0; r < rows; r++)
        {
            int d = shifted ? 0 : Math.Abs(r - mid);
            int count = shifted ? cols : Math.Max(1, cols - d);
            int startc = shifted ? 0 : d / 2;
            for (int k = 0; k < count; k++)
            {
                int c = startc + k;
                bool odd = (r & 1) != 0;
                double cx = o + c * w + ((shifted ? !odd : odd) ? w / 2 : 0);
                double cy = o + r * v;
                Add(list, Ring(cx, cy, R, 6, 30));
            }
        }
        return list;
    }

    // 5. Octagon (truncated square tiling 4.8.8).
    private static List<Pt[]> Octagon(int rows, int cols)
    {
        const double W = 40;
        double e = W / (1 + Math.Sqrt(2));
        double hw = W / 2, he = e / 2, o = W / 2;
        var list = new List<Pt[]>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if ((r & 1) != (c & 1)) continue;
                double cx = o + c * W / 2.0, cy = o + r * W / 2.0;
                if ((r & 1) == 0)
                    Add(list,
                        (cx - hw, cy - he), (cx - he, cy - hw), (cx + he, cy - hw), (cx + hw, cy - he),
                        (cx + hw, cy + he), (cx + he, cy + hw), (cx - he, cy + hw), (cx - hw, cy + he));
                else
                {
                    double d = (W - e) / 2.0;
                    Add(list, (cx, cy - d), (cx + d, cy), (cx, cy + d), (cx - d, cy));
                }
            }
        return list;
    }

    // Wall alternates full-brick rows and rows with half bricks at both ends.
    // The original uses 32x16 bricks and 16x16 boundary squares (cells.txt).
    private static List<Pt[]> Wall(int rows, int cols)
    {
        const double unit = 16;
        double width = 2 * cols * unit;
        var list = new List<Pt[]>();
        for (int row = 0; row < rows; row++)
        {
            double y = row * unit;
            double offset = (row & 1) == 0 ? 0 : unit;
            if (offset > 0)
                Add(list, (0, y), (unit, y), (unit, y + unit), (0, y + unit));
            int fullBricks = offset > 0 ? cols - 1 : cols;
            for (int col = 0; col < fullBricks; col++)
            {
                double x = offset + col * 2 * unit;
                Add(list, (x, y), (x + 2 * unit, y), (x + 2 * unit, y + unit), (x, y + unit));
            }
            if (offset > 0)
                Add(list, (width - unit, y), (width, y), (width, y + unit), (width - unit, y + unit));
        }
        return list;
    }

    // Original HX2_PLJ: hexagons on a square lattice with diamond fillers.
    private static List<Pt[]> FilledHexagons(int rows, int cols)
    {
        var list = new List<Pt[]>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                double x = c * 32, y = r * 32;
                Add(list, (x + 16, y), (x + 32, y + 8), (x + 32, y + 24),
                    (x + 16, y + 32), (x, y + 24), (x, y + 8));
                if (r + 1 < rows && c + 1 < cols)
                    Add(list, (x + 32, y + 24), (x + 48, y + 32),
                        (x + 32, y + 40), (x + 16, y + 32));
            }
        return list;
    }

    // Running bond planks.
    private static List<Pt[]> RunningBond(int rows, int cols, bool horizontal)
    {
        var list = new List<Pt[]>();
        if (horizontal)
        {
            const double u = 15, L = 3 * u;
            for (int r = 0; r < rows; r++)
            {
                double y = r * u;
                double off = (r & 1) != 0 ? L / 2 : 0;
                for (int c = 0; c < cols; c++)
                {
                    double x = c * L + off;
                    Add(list, (x, y), (x + L, y), (x + L, y + u), (x, y + u));
                }
            }
        }
        else
        {
            const double u = 15, L = 3 * u;
            for (int c = 0; c < cols; c++)
            {
                double x = c * u;
                double off = (c & 1) != 0 ? L / 2 : 0;
                for (int r = 0; r < rows; r++)
                {
                    double y = r * L + off;
                    Add(list, (x, y), (x + u, y), (x + u, y + L), (x, y + L));
                }
            }
        }
        return list;
    }

    // 6b. Herringbone (fish-bone) tiling of 2:1 bricks, coordinates verified
    // against the original screenshot: vertical brick at (u*rb + 4u*k, u*rb),
    // horizontal brick one unit to its right.
    private static List<Pt[]> Herringbone(int rows, int cols)
    {
        const double u = 16;
        var list = new List<Pt[]>();
        double W = cols * 4 * u, H = rows * 2 * u;
        // Clip boundary bricks instead of dropping them: a clipped half-brick
        // is the original game's square boundary cell.
        void AddClipped(double x, double y, double width, double height)
        {
            double left = Math.Max(0, x), top = Math.Max(0, y);
            double right = Math.Min(W, x + width), bottom = Math.Min(H, y + height);
            if (right <= left || bottom <= top) return;
            Add(list, (left, top), (right, top), (right, bottom), (left, bottom));
        }
        int bands = rows * 2 + 8;
        for (int rb = -1; rb < bands; rb++)
        {
            double y = u * rb;
            if (y >= H) break;
            for (int k = -1; k <= cols + 2; k++)
            {
                double x = u * ((rb % 4 + 4) % 4) + 4 * u * k;
                // vertical 1x2
                AddClipped(x, y, u, 2 * u);
                // horizontal 2x1
                AddClipped(x + u, y, 2 * u, u);
            }
        }
        return list;
    }

    // 6c. Crossed squares: every square is split by both diagonals into four
    // right triangles (verified against the original screenshot).
    private static List<Pt[]> CrossedSquares(int rows, int cols)
    {
        const double s = 44;
        var list = new List<Pt[]>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                double x = c * s, y = r * s;
                double mx = x + s / 2, my = y + s / 2;
                Add(list, (x, y), (x + s, y), (mx, my));                 // top
                Add(list, (x + s, y), (x + s, y + s), (mx, my));         // right
                Add(list, (x + s, y + s), (x, y + s), (mx, my));         // bottom
                Add(list, (x, y + s), (x, y), (mx, my));                 // left
            }
        return list;
    }

    // 7. Parquet / Fish bone: basket-weave blocks of plank pairs.
    private static List<Pt[]> BasketWeave(int rows, int cols, int planksPerBlock)
    {
        var list = new List<Pt[]>();
        double u = 16;
        double block = planksPerBlock * u;
        int nbx = (rows + planksPerBlock - 1) / planksPerBlock;
        int nby = (cols + planksPerBlock - 1) / planksPerBlock;
        for (int bx = 0; bx < nbx; bx++)
            for (int by = 0; by < nby; by++)
            {
                bool horiz = ((bx + by) & 1) == 0;
                double x0 = by * block, y0 = bx * block;
                if (horiz)
                {
                    for (int k = 0; k < planksPerBlock; k++)
                        Add(list, (x0, y0 + k * u), (x0 + block, y0 + k * u), (x0 + block, y0 + (k + 1) * u), (x0, y0 + (k + 1) * u));
                }
                else
                {
                    for (int k = 0; k < planksPerBlock; k++)
                        Add(list, (x0 + k * u, y0), (x0 + (k + 1) * u, y0), (x0 + (k + 1) * u, y0 + block), (x0 + k * u, y0 + block));
                }
            }
        return list;
    }

    // 8. Circles: hexagonal (60deg) or square (90deg) packing.
    private static List<Pt[]> Circles(int rows, int cols, bool hexPack)
    {
        // Original circle sprite is 20x20. Centers are spaced 32x28 in
        // the staggered board and 28x28 in the square board.
        const double radius = 10;
        var list = new List<Pt[]>();
        var centers = new Pt[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                double cx = radius + c * (hexPack ? 32 : 28) + (hexPack && (r & 1) == 0 ? 16 : 0);
                double cy = radius + r * 28;
                centers[r, c] = new Pt(cx, cy);
                Add(list, Ring(cx, cy, radius, 32, 0));
            }

        // Gap cells have concave circular sides. Follow the actual circular arc
        // around each surrounding circle, not a straight chamfer across it.
        void AddGap(params Pt[] corners)
        {
            var outline = new List<Pt>();
            for (int i = 0; i < corners.Length; i++)
            {
                var p = corners[i];
                var previous = corners[(i + corners.Length - 1) % corners.Length];
                var next = corners[(i + 1) % corners.Length];
                double start = Math.Atan2(previous.Y - p.Y, previous.X - p.X);
                double end = Math.Atan2(next.Y - p.Y, next.X - p.X);
                double sweep = Math.Atan2(Math.Sin(end - start), Math.Cos(end - start));
                for (int step = 0; step <= 8; step++)
                {
                    double angle = start + sweep * step / 8;
                    outline.Add(new Pt(p.X + radius * Math.Cos(angle), p.Y + radius * Math.Sin(angle)));
                }
            }
            list.Add(outline.ToArray());
        }

        for (int r = 0; r + 1 < rows; r++)
            for (int c = 0; c + 1 < cols; c++)
            {
                var a = centers[r, c];
                var b = centers[r, c + 1];
                var d = centers[r + 1, c];
                var e = centers[r + 1, c + 1];
                if (!hexPack) AddGap(a, b, e, d);
                else if ((r & 1) == 0)
                {
                    AddGap(a, b, e);
                    AddGap(a, e, d);
                }
                else
                {
                    AddGap(a, b, d);
                    AddGap(b, e, d);
                }
            }
        return list;
    }

    // 9. Cairo pentagonal tiling (Wikipedia collinear coordinates).
    private static List<Pt[]> Cairo(int rows, int cols)
    {
        double s = 6.5;
        var baseP = new (double x, double y)[] { (-2, 0), (2, 0), (3, 3), (0, 4), (-3, 3) };
        double w = 12 * s, h = 6 * s;
        var unique = new Dictionary<(long, long), Pt[]>();

        for (int i = 0; i <= cols; i++)
        {
            double x = i * w;
            for (int j = 0; j <= rows; j++)
            {
                double y = j * h;
                double mx = (j % 2 == 1) ? x : x + w / 2.0;
                foreach (var (rot, ox) in new (int, double)[] { (0, 0), (2, 0), (1, 6 * s), (3, -6 * s) })
                {
                    var pts = new Pt[5];
                    double sx = 0, sy = 0;
                    for (int k = 0; k < 5; k++)
                    {
                        var (rx, ry) = Rot(baseP[k].x * s, baseP[k].y * s, rot * 90);
                        pts[k] = new Pt(mx + ox + rx, y + ry);
                        sx += pts[k].X; sy += pts[k].Y;
                    }
                    long key = (long)Math.Round(sx / 5.0) * 100000 + (long)Math.Round(sy / 5.0);
                    unique[(key, 0)] = pts;
                }
            }
        }
        return new List<Pt[]>(unique.Values);
    }

    private static Pt[] ClipToRectangle(Pt[] polygon, double width, double height)
    {
        var points = new List<Pt>(polygon);
        for (int edge = 0; edge < 4; edge++)
        {
            var output = new List<Pt>();
            double Coordinate(Pt p) => edge < 2 ? p.X : p.Y;
            double boundary = edge == 1 ? width : edge == 3 ? height : 0;
            bool Inside(Pt p) => edge == 0 || edge == 2
                ? Coordinate(p) >= boundary : Coordinate(p) <= boundary;
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count];
                bool aInside = Inside(a), bInside = Inside(b);
                if (aInside != bInside)
                {
                    double t = (boundary - Coordinate(a)) / (Coordinate(b) - Coordinate(a));
                    output.Add(new Pt(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                }
                if (bInside) output.Add(b);
            }
            points = output;
        }
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(area) < 1e-6 ? Array.Empty<Pt>() : points.ToArray();
    }

    // 10. Real pentagonal: Durer's tiling of regular pentagons + 36-degree rhombi.
    // Built by edge-following: every pentagon edge gets a rhombus whose far edge
    // is the base of the next pentagon.
    private static List<Pt[]> Durer(int rows, int cols)
    {
        const double s = 40;
        var pentagons = new List<Pt[]>();
        var rhombi = new List<Pt[]>();
        var seenPent = new HashSet<(long, long)>();
        var seenRhombi = new HashSet<(long, long)>();
        var queue = new Queue<(Pt a, Pt b, Pt away)>();

        Pt[] BuildPentagon(Pt a, Pt b, Pt away)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            Pt[] best = null!;
            double bestD = -1;
            foreach (double sign in new[] { 72.0, -72.0 })
            {
                var V = new Pt[5];
                V[0] = a; V[1] = b;
                double ex = dx / len, ey = dy / len;
                for (int i = 2; i < 5; i++)
                {
                    var (rx, ry) = Rot(ex, ey, sign);
                    V[i] = new Pt(V[i - 1].X + rx * len, V[i - 1].Y + ry * len);
                    ex = rx; ey = ry;
                }
                double cx = 0, cy = 0;
                foreach (var v in V) { cx += v.X; cy += v.Y; }
                cx /= 5; cy /= 5;
                double d = (cx - away.X) * (cx - away.X) + (cy - away.Y) * (cy - away.Y);
                if (d > bestD) { bestD = d; best = V; }
            }
            return best;
        }

        void Process(Pt a, Pt b, Pt away)
        {
            var poly = BuildPentagon(a, b, away);
            double cx = 0, cy = 0;
            foreach (var v in poly) { cx += v.X; cy += v.Y; }
            cx /= 5; cy /= 5;
            var key = ((long)Math.Round(cx / 4), (long)Math.Round(cy / 4));
            if (!seenPent.Add(key)) return;
            pentagons.Add(poly);

            for (int k = 0; k < 5; k++)
            {
                var A = poly[k];
                var B = poly[(k + 1) % 5];
                double ex = (B.X - A.X) / s, ey = (B.Y - A.Y) / s;
                var (r1x, r1y) = Rot(ex, ey, 144);
                var (r2x, r2y) = Rot(ex, ey, -144);
                double mx = (A.X + B.X) / 2 - cx, my = (A.Y + B.Y) / 2 - cy;
                var (e2x, e2y) = (r1x * mx + r1y * my) > (r2x * mx + r2y * my) ? (r1x, r1y) : (r2x, r2y);
                var D = new Pt(A.X + s * e2x, A.Y + s * e2y);
                var C = new Pt(B.X + s * e2x, B.Y + s * e2y);
                var rk = ((long)Math.Round((A.X + C.X) / 8), (long)Math.Round((A.Y + C.Y) / 8));
                if (seenRhombi.Add(rk))
                    rhombi.Add(new[] { A, B, C, D });
                queue.Enqueue((C, D, new Pt(cx, cy)));
            }
        }

        // Start from a flat-bottom pentagon's bottom edge; interior is upward.
        Process(new Pt(-s / 2, 0), new Pt(s / 2, 0), new Pt(0, -10000));

        int target = Math.Max(8, rows * cols);
        int guard = 0;
        while (queue.Count > 0 && pentagons.Count < target && guard++ < 200000)
        {
            var (a, b, away) = queue.Dequeue();
            Process(a, b, away);
        }

        var result = new List<Pt[]>(pentagons.Count + rhombi.Count);
        result.AddRange(pentagons);
        result.AddRange(rhombi);
        return result;
    }

    // 11. Mosaic 5,7: pentagons + heptagons. Reconstructed from the original: its
    // 4 tile sprites (2 pentagons 24x18, 2 heptagons 36x24) were extracted from
    // M57_PLJ, and the (edi&7, esi&1) placement table was decoded from ctor
    // 0x408104. x-step 36, y-step 24.
    private static List<Pt[]> MosaicExact(int rows, int cols)
    {
        var s1 = new (double x, double y)[] { (2, 6), (11, 1), (20, 6), (15, 15), (6, 15) };      // pentagon 24x18
        var s2 = new (double x, double y)[] { (2, 9), (6, 1), (15, 1), (20, 10), (10, 14) };      // pentagon 24x18
        var s3 = new (double x, double y)[] { (2, 15), (7, 6), (17, 1), (27, 6), (32, 16), (22, 21), (12, 21) }; // heptagon 36x24
        var s4 = new (double x, double y)[] { (2, 6), (11, 1), (23, 1), (32, 6), (28, 14), (18, 20), (5, 16) };   // heptagon 36x24
        var shapes = new[] { s1, s2, s3, s4 };

        // [case][esi even?] -> (dx, dy, spriteIndex)
        var table = new (double dx, double dy, int sp)[][]
        {
            new[] { (6.0, 30.0, 1), (0.0, 36.0, 3) },  // case 0
            new[] { (0.0, 0.0, 2), (0.0, -6.0, 0) },   // case 1
            new[] { (6.0, -6.0, 1), (0.0, 0.0, 3) },   // case 2
            new[] { (0.0, 30.0, 0), (0.0, 36.0, 2) },  // case 3
            new[] { (0.0, 36.0, 3), (6.0, 30.0, 1) },  // case 4
            new[] { (0.0, -6.0, 0), (0.0, 0.0, 2) },   // case 5
            new[] { (0.0, 0.0, 3), (6.0, -6.0, 1) },   // case 6
            new[] { (0.0, 36.0, 2), (0.0, 30.0, 0) },  // case 7
        };

        var list = new List<Pt[]>();
        for (int edi = 0; edi < rows; edi++)
            for (int esi = 0; esi < cols; esi++)
            {
                var (dx, dy, sp) = table[edi & 7][esi & 1];
                var shape = shapes[sp];
                double bx = 36.0 * edi + dx, by = 24.0 * esi + dy;
                double sx = 0, sy = 0;
                foreach (var v in shape) { sx += v.x; sy += v.y; }
                sx /= shape.Length; sy /= shape.Length;
                const double grow = 1.5;
                var poly = new Pt[shape.Length];
                for (int k = 0; k < shape.Length; k++)
                    poly[k] = new Pt(bx + sx + (shape[k].x - sx) * grow,
                                     by + sy + (shape[k].y - sy) * grow);
                list.Add(poly);
            }
        return list;
    }

    // 11b. Mosaic 5,7 fallback: elongated triangular tiling.
    private static List<Pt[]> ElongatedTriangular(int rows, int cols)
    {
        const double L = 34;
        double th = L * Math.Sqrt(3) / 2.0;
        var list = new List<Pt[]>();
        double y = 0;
        for (int band = 0; band < rows; band++)
        {
            if (band % 2 == 0)
            {
                for (int c = 0; c < cols; c++)
                    Add(list, (c * L, y), (c * L + L, y), (c * L + L, y + L), (c * L, y + L));
                y += L;
            }
            else
            {
                for (int c = 0; c < cols * 2; c++)
                {
                    double x = c * L / 2.0;
                    if ((c & 1) == 0)
                        Add(list, (x, y), (x + L, y), (x + L / 2, y + th));
                    else
                        Add(list, (x, y + th), (x + L, y + th), (x + L / 2, y));
                }
                y += th;
            }
        }
        return list;
    }

    // 12. Tilted H: rhombille tiling (each hexagon split into 3 rhombi).
    private static List<Pt[]> Rhombille(int rows, int cols)
    {
        const double R = 20;
        double w = Math.Sqrt(3) * R, v = 1.5 * R;
        var list = new List<Pt[]>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                double cx = R + c * w + ((r & 1) != 0 ? w / 2 : 0);
                double cy = R + r * v;
                var V = new Pt[6];
                for (int k = 0; k < 6; k++)
                {
                    double a = Math.PI / 180.0 * (30 + 60 * k);
                    V[k] = new Pt(cx + R * Math.Cos(a), cy + R * Math.Sin(a));
                }
                Pt O = new(cx, cy);
                list.Add(new[] { O, V[0], V[1], V[2] });
                list.Add(new[] { O, V[2], V[3], V[4] });
                list.Add(new[] { O, V[4], V[5], V[0] });
            }
        return list;
    }

    // 10. Real pentagonal: reconstructed exactly from the original. Its 4 tile
    // sprites are pentagons (28x21 / 21x28) whose polygons were extracted from
    // RPT_PLJ; the board places them in a (4*rows-1) x (2*cols) lattice with
    // per-parity sub-cell offsets (decoded from ctor 0x406f28).
    private static List<Pt[]> DurerExact(int rows, int cols)
    {
        const double cellW = 28;
        // Pentagons in atlas-local coordinates (from BITMAP_RPT_PLJ).
        var shape150 = new (double x, double y)[] { (2, 7), (13, 1), (24, 7), (18, 18), (7, 18) };
        var shape1b0 = new (double x, double y)[] { (2, 11), (7, 1), (18, 1), (24, 12), (12, 17) };
        var shape210 = new (double x, double y)[] { (1, 13), (7, 2), (18, 7), (18, 18), (7, 24) };
        var shape270 = new (double x, double y)[] { (1, 7), (12, 2), (17, 12), (12, 24), (1, 19) };

        int rowsN = 4 * rows - 1;
        int colsN = 2 * cols;
        var list = new List<Pt[]>();

        for (int edi = 0; edi < rowsN; edi++)
            for (int esi = 0; esi < colsN; esi++)
            {
                int cs = ((edi & 1) * 2 + esi) & 3;
                double bx = edi * cellW, by = (esi / 2) * cellW;
                (double x, double y) off;
                (double x, double y)[] shape;
                switch (cs)
                {
                    case 0: off = (cellW / 2, 0); shape = shape270; break;
                    case 1: off = (cellW / 4, cellW / 2); shape = shape150; break;
                    case 2: off = (cellW / 2, cellW / 4); shape = shape210; break;
                    default: off = (0, cellW / 2); shape = shape1b0; break;
                }
                var poly = new Pt[5];
                // Grow slightly about the shape centroid: the extracted polygons are
                // the fill, so they leave the shared border-width as a gap.
                double sx = 0, sy = 0;
                for (int k = 0; k < 5; k++) { sx += shape[k].x; sy += shape[k].y; }
                sx /= 5; sy /= 5;
                const double grow = 1.5;
                for (int k = 0; k < 5; k++)
                    poly[k] = new Pt(
                        bx + off.x + sx + (shape[k].x - sx) * grow,
                        by + off.y + sy + (shape[k].y - sy) * grow);
                list.Add(poly);
            }
        return list;
    }

    // 10b. Floret pentagonal tiling: every hexagon is split into three congruent
    // pentagons (a genuine, connected pentagonal tiling). Used for Real Pentagonal.
    private static List<Pt[]> Floret(int rows, int cols)
    {
        const double R = 20;
        double w = Math.Sqrt(3) * R, v = 1.5 * R;
        var list = new List<Pt[]>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                double cx = R + c * w + ((r & 1) != 0 ? w / 2 : 0);
                double cy = R + r * v;
                var V = new Pt[6];
                for (int k = 0; k < 6; k++)
                {
                    double a = Math.PI / 180.0 * (30 + 60 * k);
                    V[k] = new Pt(cx + R * Math.Cos(a), cy + R * Math.Sin(a));
                }
                Pt O = new(cx, cy);
                for (int k = 0; k < 3; k++)
                {
                    int a = 2 * k, b = 2 * k + 1, d = 2 * k + 2;
                    Pt M = new((V[d % 6].X + V[(d + 1) % 6].X) / 2, (V[d % 6].Y + V[(d + 1) % 6].Y) / 2);
                    list.Add(new[] { O, V[a % 6], V[b % 6], V[d % 6], M });
                }
            }
        return list;
    }

    // 12. Tilted H: a square grid where each square is cut by one diagonal, with
    // the diagonal direction alternating, forming the arrow/H shapes seen in the
    // original.
    private static List<Pt[]> TiltedH(int rows, int cols)
    {
        const double s = 40;
        var list = new List<Pt[]>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                double x = c * s, y = r * s;
                if (((r + c) & 1) == 0)
                {
                    Add(list, (x, y), (x + s, y), (x + s, y + s));   // upper-right triangle
                    Add(list, (x, y), (x + s, y + s), (x, y + s));   // lower-left triangle
                }
                else
                {
                    Add(list, (x, y), (x + s, y), (x, y + s));       // upper-left triangle
                    Add(list, (x + s, y), (x + s, y + s), (x, y + s)); // lower-right triangle
                }
            }
        return list;
    }

    private static (double x, double y)[] Ring(double cx, double cy, double r, int n, double startDeg)
    {
        var pts = new (double x, double y)[n];
        for (int i = 0; i < n; i++)
        {
            double a = (startDeg + 360.0 / n * i) * Math.PI / 180.0;
            pts[i] = (cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }
        return pts;
    }
}
