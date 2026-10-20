using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfessionalMinesweeper.Core;

public readonly record struct Pt(double X, double Y)
{
    public double DistanceTo(Pt other)
    {
        double dx = X - other.X, dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed class Tile
{
    public Position Pos { get; }
    public Pt[] Polygon { get; }
    public Pt Center { get; }

    public Tile(Position pos, Pt[] polygon)
    {
        Pos = pos;
        Polygon = polygon;
        double sx = 0, sy = 0;
        foreach (var p in polygon) { sx += p.X; sy += p.Y; }
        Center = new Pt(sx / polygon.Length, sy / polygon.Length);
    }
}

public enum AdjacencyMode
{
    Edges,
    EdgesAndCorners,
    Proximity
}

public interface IBoardTopology
{
    BoardType Type { get; }
    int Rows { get; }
    int Columns { get; }
    int TotalCells { get; }
    IReadOnlyList<Tile> Tiles { get; }
    bool IsValid(Position pos);
    IEnumerable<Position> GetNeighbors(Position pos);
    IEnumerable<Position> AllPositions { get; }
}

/// <summary>
/// Describes a board purely by the polygons of its cells. Logical (row, col)
/// indices are assigned automatically, so any tessellation can be used and
/// adjacency is derived from the actual geometry.
/// </summary>
public sealed class PolygonTilingTopology : IBoardTopology
{
    private readonly Dictionary<Position, Tile> _tiles = new();
    private readonly Dictionary<Position, List<Position>> _neighbors = new();

    public BoardType Type { get; }
    public int Rows { get; }
    public int Columns { get; }
    public int TotalCells => Tiles.Count;
    public IReadOnlyList<Tile> Tiles { get; }

    public PolygonTilingTopology(BoardType type, IReadOnlyList<Pt[]> polygons, AdjacencyMode mode)
        : this(type, polygons, mode, 2.6)
    {
    }

    /// <param name="eps">Distance tolerance for touching geometry. Dense irregular
    /// tilings (such as Voronoi cells) need a tighter value so cells that merely
    /// pass close to each other are not treated as neighbours.</param>
    public PolygonTilingTopology(BoardType type, IReadOnlyList<Pt[]> polygons, AdjacencyMode mode, double eps)
    {
        Type = type;

        int n = polygons.Count;
        Columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(n)));
        Rows = Math.Max(1, (int)Math.Ceiling(n / (double)Columns));

        var tiles = new List<Tile>(n);
        for (int i = 0; i < n; i++)
        {
            var pos = new Position(i / Columns, i % Columns);
            var tile = new Tile(pos, polygons[i]);
            tiles.Add(tile);
            _tiles[pos] = tile;
        }
        Tiles = tiles;

        if (mode == AdjacencyMode.Proximity) BuildProximity();
        else BuildShared(mode == AdjacencyMode.EdgesAndCorners, eps);
    }

    public PolygonTilingTopology(BoardType type, IReadOnlyList<Pt[]> polygons, IReadOnlyList<int[]> explicitNeighbors)
    {
        Type = type;

        int n = polygons.Count;
        Columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(n)));
        Rows = Math.Max(1, (int)Math.Ceiling(n / (double)Columns));

        var tiles = new List<Tile>(n);
        for (int i = 0; i < n; i++)
        {
            var pos = new Position(i / Columns, i % Columns);
            var tile = new Tile(pos, polygons[i]);
            tiles.Add(tile);
            _tiles[pos] = tile;
        }
        Tiles = tiles;

        for (int i = 0; i < n; i++)
        {
            var pFrom = tiles[i].Pos;
            var list = new List<Position>(explicitNeighbors[i].Length);
            foreach (var targetIdx in explicitNeighbors[i])
            {
                if (targetIdx >= 0 && targetIdx < n)
                    list.Add(tiles[targetIdx].Pos);
            }
            _neighbors[pFrom] = list;
        }
    }

    public bool IsValid(Position pos) => _tiles.ContainsKey(pos);
    public IEnumerable<Position> AllPositions => _tiles.Keys;
    public Tile GetTile(Position pos) => _tiles[pos];

    public IEnumerable<Position> GetNeighbors(Position pos)
        => _neighbors.TryGetValue(pos, out var list) ? list : Enumerable.Empty<Position>();

    private void BuildShared(bool cornersCount, double eps)
    {
        foreach (var t in Tiles) _neighbors[t.Pos] = new List<Position>();

        // Avoid the quadratic all-pairs scan on large boards. A tile can only
        // touch another tile whose expanded bounding boxes share a spatial bin.
        const double binSize = 64;
        var bins = new Dictionary<(int X, int Y), List<int>>();
        for (int i = 0; i < Tiles.Count; i++)
        {
            var bounds = Bounds(Tiles[i].Polygon, eps);
            int minX = (int)Math.Floor(bounds.minX / binSize);
            int maxX = (int)Math.Floor(bounds.maxX / binSize);
            int minY = (int)Math.Floor(bounds.minY / binSize);
            int maxY = (int)Math.Floor(bounds.maxY / binSize);
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    var key = (x, y);
                    if (!bins.TryGetValue(key, out var list)) bins[key] = list = new List<int>();
                    list.Add(i);
                }
        }

        var tested = new HashSet<(int A, int B)>();
        foreach (var bucket in bins.Values)
            for (int bi = 0; bi < bucket.Count; bi++)
            {
                int i = bucket[bi];
                for (int bj = bi + 1; bj < bucket.Count; bj++)
                {
                    int j = bucket[bj];
                    if (!tested.Add(i < j ? (i, j) : (j, i))) continue;
                    bool adjacent = cornersCount
                        ? BoundaryTouch(Tiles[i].Polygon, Tiles[j].Polygon, eps)
                        : EdgeOverlap(Tiles[i].Polygon, Tiles[j].Polygon, eps);
                    if (adjacent)
                    {
                        _neighbors[Tiles[i].Pos].Add(Tiles[j].Pos);
                        _neighbors[Tiles[j].Pos].Add(Tiles[i].Pos);
                    }
                }
            }
    }

    private static (double minX, double minY, double maxX, double maxY) Bounds(Pt[] polygon, double margin)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in polygon)
        {
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
        }
        return (minX - margin, minY - margin, maxX + margin, maxY + margin);
    }

    private static bool BoundaryTouch(Pt[] a, Pt[] b, double eps)
    {
        return VertexOnEdge(a, b, eps) || VertexOnEdge(b, a, eps);
    }

    private static bool VertexOnEdge(Pt[] a, Pt[] b, double eps)
    {
        for (int i = 0; i < a.Length; i++)
        {
            var p = a[i];
            for (int k = 0; k < b.Length; k++)
            {
                var s1 = b[k];
                var s2 = b[(k + 1) % b.Length];
                if (PointSegmentDistance(p, s1, s2) <= eps) return true;
            }
        }
        return false;
    }

    private static bool EdgeOverlap(Pt[] a, Pt[] b, double eps)
    {
        for (int i = 0; i < a.Length; i++)
        {
            var a1 = a[i];
            var a2 = a[(i + 1) % a.Length];
            double alen = a1.DistanceTo(a2);
            if (alen < eps) continue;
            double ux = (a2.X - a1.X) / alen, uy = (a2.Y - a1.Y) / alen;

            for (int k = 0; k < b.Length; k++)
            {
                var b1 = b[k];
                var b2 = b[(k + 1) % b.Length];
                if (b1.DistanceTo(b2) < eps) continue;

                double perp1 = Math.Abs((b1.X - a1.X) * (-uy) + (b1.Y - a1.Y) * ux);
                double perp2 = Math.Abs((b2.X - a1.X) * (-uy) + (b2.Y - a1.Y) * ux);
                if (perp1 > eps || perp2 > eps) continue;

                double t1 = (b1.X - a1.X) * ux + (b1.Y - a1.Y) * uy;
                double t2 = (b2.X - a1.X) * ux + (b2.Y - a1.Y) * uy;
                double lo = Math.Max(0, Math.Min(t1, t2));
                double hi = Math.Min(alen, Math.Max(t1, t2));
                if (hi - lo > eps) return true;
            }
        }
        return false;
    }

    private static double PointSegmentDistance(Pt p, Pt a, Pt b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-9) return p.DistanceTo(a);
        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = Math.Clamp(t, 0, 1);
        return p.DistanceTo(new Pt(a.X + t * dx, a.Y + t * dy));
    }

    private void BuildProximity()
    {
        foreach (var t in Tiles) _neighbors[t.Pos] = new List<Position>();

        foreach (var t in Tiles)
        {
            double dmin = double.MaxValue;
            foreach (var o in Tiles)
            {
                if (o.Pos == t.Pos) continue;
                dmin = Math.Min(dmin, o.Center.DistanceTo(t.Center));
            }
            if (dmin == double.MaxValue) continue;

            double ring = dmin * 1.15;
            foreach (var o in Tiles)
            {
                if (o.Pos == t.Pos) continue;
                if (o.Center.DistanceTo(t.Center) <= ring)
                    _neighbors[t.Pos].Add(o.Pos);
            }
        }
    }
}
