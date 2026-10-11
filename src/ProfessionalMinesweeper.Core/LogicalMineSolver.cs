using System.Diagnostics;

namespace ProfessionalMinesweeper.Core;

/// <summary>Verifies a layout using only revealed clues and the total mine count.
/// Hidden mines are consulted only when revealing a logically proven safe cell.</summary>
public static class LogicalMineSolver
{
    private sealed record Constraint(int[] Cells, int Mines);

    public static bool CanSolve(int[][] neighbors, bool[] mines, int first, int mineCount,
        Func<bool>? stop = null)
    {
        var knowledge = new byte[mines.Length]; // 0 unknown, 1 revealed, 2 proven mine
        var clues = new int[mines.Length];
        for (int i = 0; i < mines.Length; i++)
            foreach (int n in neighbors[i]) if (mines[n]) clues[i]++;
        int revealed = 0;
        bool Open(int id)
        {
            if (mines[id]) return false;
            var queue = new Queue<int>();
            if (knowledge[id] != 0) return true;
            knowledge[id] = 1; revealed++; queue.Enqueue(id);
            while (queue.TryDequeue(out int p))
                if (clues[p] == 0)
                    foreach (int n in neighbors[p])
                        if (knowledge[n] == 0)
                        {
                            if (mines[n]) return false;
                            knowledge[n] = 1; revealed++; queue.Enqueue(n);
                        }
            return true;
        }
        if (!Open(first)) return false;
        while (revealed < mines.Length - mineCount)
        {
            if (stop?.Invoke() == true) return false;
            var constraints = new List<Constraint>();
            var unique = new HashSet<string>();
            bool progress = false;
            bool Apply(Constraint c)
            {
                if (c.Mines < 0 || c.Mines > c.Cells.Length) return false;
                if (c.Mines == 0 || c.Mines == c.Cells.Length)
                    foreach (int p in c.Cells)
                        if (knowledge[p] == 0)
                        {
                            if (c.Mines == 0) { if (!Open(p)) return false; }
                            else knowledge[p] = 2;
                            progress = true;
                        }
                return true;
            }
            for (int p = 0; p < knowledge.Length; p++)
                if (knowledge[p] == 1)
                {
                    int remaining = clues[p];
                    var unknown = new List<int>();
                    foreach (int n in neighbors[p])
                        if (knowledge[n] == 2) remaining--;
                        else if (knowledge[n] == 0) unknown.Add(n);
                    if (unknown.Count == 0) continue;
                    unknown.Sort();
                    var c = new Constraint(unknown.ToArray(), remaining);
                    if (!Apply(c)) return false;
                    if (unique.Add(string.Join(',', c.Cells))) constraints.Add(c);
                }
            if (progress) continue; // rebuild constraints after knowledge changed
            var all = Enumerable.Range(0, mines.Length).Where(p => knowledge[p] == 0).ToArray();
            int left = mineCount - knowledge.Count(k => k == 2);
            if (!Apply(new Constraint(all, left))) return false;
            if (progress) continue;
            // Close local constraints under subset subtraction. Bound both work
            // and storage so unusual/high-degree topologies cannot blow up memory.
            for (int i = 0; i < constraints.Count && i < 512 && !progress; i++)
            {
                if (stop?.Invoke() == true) return false;
                for (int j = 0; j < constraints.Count && j < 512 && !progress; j++)
                {
                    var a = constraints[i]; var b = constraints[j];
                    if (a.Cells.Length >= b.Cells.Length || !a.Cells.All(p => Array.BinarySearch(b.Cells, p) >= 0)) continue;
                    var difference = b.Cells.Where(p => Array.BinarySearch(a.Cells, p) < 0).ToArray();
                    var c = new Constraint(difference, b.Mines - a.Mines);
                    if (!Apply(c)) return false;
                    if (!progress && constraints.Count < 512 && unique.Add(string.Join(',', difference))) constraints.Add(c);
                }
            }
            if (!progress) return false;
        }
        return true;
    }
}
