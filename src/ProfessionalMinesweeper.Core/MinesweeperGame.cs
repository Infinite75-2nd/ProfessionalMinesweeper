namespace ProfessionalMinesweeper.Core;

public sealed class MinesweeperGame
{
    private readonly IBoardTopology _topology;
    private readonly Cell[,] _cells;
    private readonly int _mineCount;
    private readonly Random _random;
    private bool _minesPlaced;
    public bool IsLogicallySolvable { get; private set; }
    public int GenerationAttempts { get; private set; }

    public IBoardTopology Topology => _topology;
    public GameStatus Status { get; private set; } = GameStatus.NotStarted;
    public int TotalMines => _mineCount;
    public int RemainingFlags => _mineCount - FlagCount;
    public int FlagCount { get; private set; }
    public int RevealedCount { get; private set; }
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }

    public TimeSpan ElapsedTime
    {
        get
        {
            if (StartTime == null) return TimeSpan.Zero;
            return (EndTime ?? DateTime.UtcNow) - StartTime.Value;
        }
    }

    public event Action? StateChanged;
    public event Action<GameStatus>? GameOver;

    public MinesweeperGame(IBoardTopology topology, int mineCount, int? seed = null)
    {
        _topology = topology ?? throw new ArgumentNullException(nameof(topology));
        if (mineCount <= 0 || mineCount >= topology.TotalCells)
            throw new ArgumentOutOfRangeException(nameof(mineCount), "Mines must be > 0 and < total cells.");

        _mineCount = mineCount;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _cells = new Cell[topology.Rows, topology.Columns];

        for (int r = 0; r < topology.Rows; r++)
        {
            for (int c = 0; c < topology.Columns; c++)
            {
                _cells[r, c] = new Cell(new Position(r, c));
            }
        }
    }

    public Cell GetCell(Position pos) => _cells[pos.Row, pos.Col];
    public Cell GetCell(int r, int c) => _cells[r, c];

    public void Restart()
    {
        Status = GameStatus.NotStarted;
        FlagCount = 0;
        RevealedCount = 0;
        StartTime = null;
        EndTime = null;
        _minesPlaced = false;
        IsLogicallySolvable = false;
        GenerationAttempts = 0;

        for (int r = 0; r < _topology.Rows; r++)
        {
            for (int c = 0; c < _topology.Columns; c++)
            {
                _cells[r, c].Reset();
            }
        }
        StateChanged?.Invoke();
    }

    public bool Reveal(Position pos)
    {
        if (Status == GameStatus.Won || Status == GameStatus.Lost)
            return false;

        if (!_topology.IsValid(pos))
            return false;

        var cell = GetCell(pos);
        if (cell.State != CellState.Covered)
            return false;

        if (Status == GameStatus.NotStarted)
        {
            StartGame(pos);
        }

        if (cell.IsMine)
        {
            cell.Exploded = true;
            cell.State = CellState.Revealed;
            Status = GameStatus.Lost;
            EndTime = DateTime.UtcNow;
            RevealAllMinesOnLoss();
            GameOver?.Invoke(GameStatus.Lost);
            StateChanged?.Invoke();
            return false;
        }

        CascadeReveal(pos);

        CheckWinCondition();
        StateChanged?.Invoke();
        return true;
    }

    public void ToggleFlag(Position pos)
    {
        if (Status == GameStatus.Won || Status == GameStatus.Lost)
            return;

        if (!_topology.IsValid(pos))
            return;

        var cell = GetCell(pos);
        if (cell.State == CellState.Revealed)
            return;

        if (cell.State == CellState.Covered)
        {
            cell.State = CellState.Flagged;
            FlagCount++;
        }
        else if (cell.State == CellState.Flagged)
        {
            cell.State = CellState.Questioned;
            FlagCount--;
        }
        else if (cell.State == CellState.Questioned)
        {
            cell.State = CellState.Covered;
        }

        StateChanged?.Invoke();
    }

    public bool Chord(Position pos)
    {
        if (Status != GameStatus.InProgress)
            return false;

        if (!_topology.IsValid(pos))
            return false;

        var cell = GetCell(pos);
        if (cell.State != CellState.Revealed || cell.AdjacentMines == 0)
            return false;

        var neighbors = _topology.GetNeighbors(pos).ToList();
        int flaggedNeighbors = neighbors.Count(p => GetCell(p).State == CellState.Flagged);

        if (flaggedNeighbors != cell.AdjacentMines)
            return false;

        bool triggeredMine = false;
        var toReveal = neighbors.Where(p => GetCell(p).State == CellState.Covered).ToList();

        foreach (var p in toReveal)
        {
            var neighborCell = GetCell(p);
            if (neighborCell.IsMine)
            {
                neighborCell.Exploded = true;
                neighborCell.State = CellState.Revealed;
                triggeredMine = true;
            }
            else
            {
                CascadeReveal(p);
            }
        }

        if (triggeredMine)
        {
            Status = GameStatus.Lost;
            EndTime = DateTime.UtcNow;
            RevealAllMinesOnLoss();
            GameOver?.Invoke(GameStatus.Lost);
        }
        else
        {
            CheckWinCondition();
        }

        StateChanged?.Invoke();
        return !triggeredMine;
    }

    private void StartGame(Position firstClick)
    {
        Status = GameStatus.InProgress;
        StartTime = DateTime.UtcNow;

        if (!_minesPlaced)
        {
            PlaceMines(firstClick);
            _minesPlaced = true;
        }

        ComputeAdjacency();
    }

    /// <summary>
    /// Places mines only after the first reveal. The first clicked cell and
    /// (when room allows) all of its neighbours are guaranteed to be mine-free.
    /// </summary>
    private void PlaceMines(Position firstClick)
    {
        var safeZone = new HashSet<Position> { firstClick };
        foreach (var neighbor in _topology.GetNeighbors(firstClick))
            safeZone.Add(neighbor);

        var candidates = _topology.AllPositions.Where(p => !safeZone.Contains(p)).ToList();

        if (candidates.Count < _mineCount)
        {
            candidates = _topology.AllPositions.Where(p => p != firstClick).ToList();
        }

        var positions = _topology.AllPositions.ToArray();
        var indices = positions.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);
        var neighbors = positions.Select(p => _topology.GetNeighbors(p).Select(n => indices[n]).ToArray()).ToArray();
        var layout = new bool[positions.Length];
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // Prefer certified layouts without blocking a mobile UI indefinitely.
        // A timed-out layout is explicitly not advertised as no-guess.
        for (int attempt = 0; attempt < 128; attempt++)
        {
            Array.Clear(layout);
            for (int i = 0; i < _mineCount; i++)
            {
                int j = _random.Next(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                layout[indices[candidates[i]]] = true;
            }
            GenerationAttempts++;
            if (LogicalMineSolver.CanSolve(neighbors, layout, indices[firstClick], _mineCount,
                () => watch.ElapsedMilliseconds >= 200))
            {
                IsLogicallySolvable = true;
                break;
            }
            if (watch.ElapsedMilliseconds >= 200) break;
        }
        for (int i = 0; i < positions.Length; i++)
            GetCell(positions[i]).IsMine = layout[i];
    }

    private void ComputeAdjacency()
    {
        foreach (var p in _topology.AllPositions)
        {
            var cell = _cells[p.Row, p.Col];
            if (cell.IsMine)
            {
                cell.AdjacentMines = 0;
                continue;
            }
            cell.AdjacentMines = _topology.GetNeighbors(p).Count(np => _cells[np.Row, np.Col].IsMine);
        }
    }

    private void CascadeReveal(Position start)
    {
        var startCell = GetCell(start);
        if (startCell.State != CellState.Covered || startCell.IsMine) return;
        var queue = new Queue<Position>();
        startCell.State = CellState.Revealed;
        RevealedCount++;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentCell = GetCell(current);

            if (currentCell.AdjacentMines == 0)
            {
                foreach (var neighbor in _topology.GetNeighbors(current))
                {
                    var nCell = GetCell(neighbor);
                    if (nCell.State == CellState.Covered && !nCell.IsMine)
                    {
                        // Claim the cell before enqueueing it. Otherwise every
                        // queued neighbor can enqueue it again before it is visited.
                        nCell.State = CellState.Revealed;
                        RevealedCount++;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
    }

    private void RevealAllMinesOnLoss()
    {
        foreach (var p in _topology.AllPositions)
        {
            var cell = _cells[p.Row, p.Col];
            if (cell.IsMine && cell.State != CellState.Flagged)
            {
                cell.State = CellState.Revealed;
            }
        }
    }

    private void CheckWinCondition()
    {
        int totalSafeCells = _topology.TotalCells - _mineCount;
        if (RevealedCount >= totalSafeCells)
        {
            Status = GameStatus.Won;
            EndTime = DateTime.UtcNow;

            foreach (var p in _topology.AllPositions)
            {
                var cell = _cells[p.Row, p.Col];
                if (cell.IsMine && cell.State != CellState.Flagged)
                {
                    cell.State = CellState.Flagged;
                    FlagCount++;
                }
            }
            GameOver?.Invoke(GameStatus.Won);
        }
    }
}
