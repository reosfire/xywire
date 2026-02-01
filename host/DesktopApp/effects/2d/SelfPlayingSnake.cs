using System.Collections;
using System.Numerics;
using Leds.core;

namespace Leds.effects._2d;

internal class SelfPlayingSnake : AbstractEffect
{
    private const int AttemptsToFindShortestSafePathToApple = 200;
    private const int AttemptsToFindLongestPathToTail = 50;

    private readonly Color _emptyColor = Color.RGB(0, 0, 0);
    private readonly Color _foodColor = Color.RGB(255, 0, 0);

    private readonly Color[][] _colorsBuffer;

    private readonly PathFinder _pathFinder;

    private readonly List<Index2D> _randomPathBuffer = [];
    private readonly Random _seedPickerRandom = new();

    private readonly CellType[][] _simGrid;
    private readonly CircularBuffer<Index2D> _simSnake;
    private readonly CircularBuffer<Index2D> _snake;

    private List<Index2D> _cachedPathToApple = [];
    private List<Index2D> _cachedPathToTail = [];

    private int _currentFrame;

    private int _currentSeed;
    private Index2D _foodIndex;

    private int _previousLength;
    private Random _random = new();
    private int _sameLengthIterations;

    public SelfPlayingSnake(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);
        _snake = new CircularBuffer<Index2D>(attachedLedLine.Width * attachedLedLine.Height + 1);
        _simSnake = new CircularBuffer<Index2D>(attachedLedLine.Width * attachedLedLine.Height + 1);
        _simGrid = Array2D.CreateJagged(attachedLedLine.Height, attachedLedLine.Width, CellType.Empty);
        _pathFinder = new PathFinder(attachedLedLine.Width, attachedLedLine.Height);

        RestoreInitialState();
    }

    protected override void MoveNext()
    {
        if (_currentFrame++ % 3 != 0)
        {
            LedLine.SetColors(_colorsBuffer);
            return;
        }

        if (_previousLength == _snake.Count)
        {
            _sameLengthIterations++;
        }
        else
        {
            _sameLengthIterations = 0;
            _previousLength = _snake.Count;
        }

        if (_sameLengthIterations >= 14 * 14 * 2)
        {
            Console.WriteLine("Snake ended up circling on seed: " + _currentSeed + "; empty cells count: " +
                              (LedLine.Width * LedLine.Height - _snake.Count));
            RestoreInitialState();
            return;
        }

        CellType[][] currentGrid = GetCurrentGrid();

        Direction direction = GetNextDirection(currentGrid);

        Index2D head = _snake.GetFront();
        Index2D newHead = head + direction;

        if (IsIndexOutsideBounds(newHead))
        {
            Console.WriteLine("Snake ended up hitting wall on seed: " + _currentSeed + "; empty cells count: " +
                              (LedLine.Width * LedLine.Height - _snake.Count));
            RestoreInitialState();
            return;
        }

        CellType cellAtNext = currentGrid.Get(newHead);
        if (cellAtNext is CellType.SnakeBody)
        {
            Console.WriteLine("Snake ended up biting itself on seed: " + _currentSeed + "; empty cells count: " +
                              (LedLine.Width * LedLine.Height - _snake.Count));
            RestoreInitialState();
            return;
        }

        _snake.AddFront(newHead);

        // Update food and tail
        if (newHead == _foodIndex) SpawnFood(currentGrid);
        else _snake.RemoveBack();

        // Draw state to buffer
        _colorsBuffer.Fill(_emptyColor);
        for (int i = 0; i < _snake.Count; i++)
        {
            _colorsBuffer.Set(_snake[i], Color.HSV(i * 2 % 360, 1, 1));
        }

        _colorsBuffer.Set(_foodIndex, _foodColor);

        LedLine.SetColors(_colorsBuffer);
    }

    private void SpawnFood(CellType[][] grid)
    {
        List<Index2D> availablePositions = [];

        for (int i = 0; i < LedLine.Height; i++)
        {
            for (int j = 0; j < LedLine.Width; j++)
            {
                if (grid[i][j] == CellType.Empty)
                {
                    availablePositions.Add(new Index2D(i, j));
                }
            }
        }

        if (availablePositions.Count > 0)
        {
            _foodIndex = availablePositions[_random.Next(availablePositions.Count)];
        }
    }

    private Direction GetNextDirection(CellType[][] grid)
    {
        if (_cachedPathToApple.Count > 0) _cachedPathToApple.RemoveAt(0);
        if (_cachedPathToTail.Count > 0) _cachedPathToTail.RemoveAt(0);

        Index2D head = _snake.GetFront();
        Index2D tail = _snake.GetBack();

        List<Index2D>? shortestToFood = _pathFinder.GetShortestPath(head, _foodIndex, grid);
        if (shortestToFood != null)
        {
            // If shortest path to food is safe, take it
            if (shortestToFood.Count > 1 && PathSafe(shortestToFood))
            {
                Direction direction = GetDirectionFromPath(shortestToFood);
                _cachedPathToApple = [];
                _cachedPathToTail = [];
                return direction;
            }

            List<Index2D> shortestSafeAppleEndingPath = _cachedPathToApple;
            for (int i = 0; i < AttemptsToFindShortestSafePathToApple; i++)
            {
                GetRandomPath(head, grid, _randomPathBuffer);
                if (_randomPathBuffer.Count == 0) continue;
                if (_randomPathBuffer.Count >= shortestSafeAppleEndingPath.Count &&
                    shortestSafeAppleEndingPath.Count > 1) continue;
                if (_randomPathBuffer.Last() != _foodIndex) continue;
                if (!PathSafe(_randomPathBuffer)) continue;
                shortestSafeAppleEndingPath.Clear();
                shortestSafeAppleEndingPath.AddRange(_randomPathBuffer);
            }

            // If we have safe paths, pick the shortest and take its first move
            if (shortestSafeAppleEndingPath.Count > 1)
            {
                _cachedPathToApple = shortestSafeAppleEndingPath;
                Direction direction = GetDirectionFromPath(shortestSafeAppleEndingPath);
                _cachedPathToTail = [];
                return direction;
            }
        }

        // Searching for longest path to tail as fallback
        for (int i = 0; i < AttemptsToFindLongestPathToTail; i++)
        {
            GetRandomPath(head, grid, _randomPathBuffer);
            if (!PathSafe(_randomPathBuffer)) continue;
            if (_randomPathBuffer.Count <= _cachedPathToTail.Count) continue;

            _cachedPathToTail.Clear();
            _cachedPathToTail.AddRange(_randomPathBuffer);
        }

        if (_cachedPathToTail.Count > 1)
        {
            Direction direction = GetDirectionFromPath(_cachedPathToTail);
            _cachedPathToApple = [];
            return direction;
        }

        // As final fallback, use the shortest path to the tail
        List<Index2D>? shortestToTail = _pathFinder.GetShortestPath(head, tail, grid);
        if (shortestToTail != null && shortestToTail.Count >= 2 && PathSafe(shortestToTail))
        {
            Direction direction = GetDirectionFromPath(shortestToTail);
            _cachedPathToApple = [];
            _cachedPathToTail = [];
            return direction;
        }

        // This should happen only on win
        Console.WriteLine("Cannot find a valid move. It's either win or algorithm failure");

        // Return any legal move
        foreach (Direction direction in Direction.PossibleDirections)
        {
            Index2D next = head + direction;

            if (IsIndexOutsideBounds(next)) continue;
            if (grid.Get(next) is CellType.SnakeHead or CellType.SnakeBody) continue;

            return direction;
        }

        // Return any move to just fail on next iteration
        return Direction.PossibleDirections[0];
    }

    private bool PathSafe(List<Index2D> path)
    {
        if (path.Count == 0) return false;
        _simSnake.Clear();
        _simSnake.FillFrom(_snake);
        bool foodPresent = true;

        for (int i = 1; i < path.Count; i++)
        {
            Index2D next = path[i];
            _simSnake.AddFront(next);

            if (foodPresent && next == _foodIndex) foodPresent = false;
            else _simSnake.RemoveBack();
        }

        // Build simulated grid
        _simGrid.Fill(CellType.Empty);
        if (foodPresent) _simGrid.Set(_foodIndex, CellType.Food);

        Index2D simHead = _simSnake.GetFront();
        Index2D simTail = _simSnake.GetBack();

        _simGrid.Set(simHead, CellType.SnakeHead);
        for (int i = 1; i < _simSnake.Count - 1; i++)
        {
            _simGrid.Set(_simSnake[i], CellType.SnakeBody);
        }

        _simGrid.Set(simTail, CellType.SnakeTail);

        return _pathFinder.PathExists(simHead, simTail, _simGrid);
    }

    private void GetRandomPath(Index2D from, CellType[][] grid, List<Index2D> path)
    {
        path.Clear();
        path.Add(from);

        BitArray inPath = new(LedLine.Height * LedLine.Width) { [from.Row * LedLine.Width + from.Col] = true };

        while (true)
        {
            Index2D current = path.Last();
            int possibleDirectionsMask = 0;
            int possibleDirectionsCount = 0;

            for (int i = 0; i < Direction.PossibleDirections.Length; i++)
            {
                Direction direction = Direction.PossibleDirections[i];
                Index2D next = current + direction;

                if (IsIndexOutsideBounds(next)) continue;
                if (grid.Get(next) is CellType.SnakeHead or CellType.SnakeBody)
                    continue;
                if (inPath[next.Row * LedLine.Width + next.Col]) continue;

                possibleDirectionsMask |= 1 << i;
                possibleDirectionsCount++;
            }

            if (possibleDirectionsCount == 0) break;
            int randomDirectionIndex = _random.Next(possibleDirectionsCount);

            int takenDirectionIndex = GetIthSetBitIndex(possibleDirectionsMask, randomDirectionIndex);

            Index2D moveTaken = current + Direction.PossibleDirections[takenDirectionIndex];
            path.Add(moveTaken);
            inPath[moveTaken.Row * LedLine.Width + moveTaken.Col] = true;

            if (moveTaken == _foodIndex) break;
        }
    }

    private bool IsIndexOutsideBounds(Index2D index) =>
        index.Row < 0 || index.Row >= LedLine.Height || index.Col < 0 || index.Col >= LedLine.Width;

    private static Direction GetDirectionFromPath(List<Index2D> path)
    {
        if (path.Count < 2)
            throw new ArgumentException("Path must contain at least two points to determine direction.");

        Index2D start = path[0];
        Index2D next = path[1];

        return new Direction(next.Row - start.Row, next.Col - start.Col);
    }

    private static int GetIthSetBitIndex(int x, int i)
    {
        while (i-- > 0)
            x &= x - 1;

        return BitOperations.TrailingZeroCount(x);
    }

    private CellType[][] GetCurrentGrid()
    {
        CellType[][] grid = Array2D.CreateJagged(LedLine.Height, LedLine.Width, CellType.Empty);

        grid.Set(_foodIndex, CellType.Food);

        grid.Set(_snake.GetFront(), CellType.SnakeHead);
        for (int i = 1; i < _snake.Count - 1; i++)
        {
            grid.Set(_snake[i], CellType.SnakeBody);
        }

        grid.Set(_snake.GetBack(), CellType.SnakeTail);

        return grid;
    }

    private void RestoreInitialState()
    {
        _currentSeed = _seedPickerRandom.Next();
        _random = new Random(_currentSeed);

        _previousLength = 0;
        _sameLengthIterations = 0;

        _cachedPathToApple.Clear();
        _cachedPathToTail.Clear();

        RestoreInitialSnakeState();
        SpawnFood(GetCurrentGrid());
        Console.WriteLine("Starting new game with seed: " + _currentSeed);
    }

    private void RestoreInitialSnakeState()
    {
        _snake.Clear();

        int startRow = LedLine.Width / 2;
        int startCol = LedLine.Height / 2;
        _snake.AddFront(new Index2D(startRow, startCol - 2));
        _snake.AddFront(new Index2D(startRow, startCol - 1));
        _snake.AddFront(new Index2D(startRow, startCol));
    }

    protected override int StabilizeFps() => 60;
}

internal enum CellType
{
    Empty,
    Food,
    SnakeHead,
    SnakeBody,
    SnakeTail,
}

internal readonly record struct Index2D(int Row, int Col)
{
    public static Index2D operator +(Index2D index, Direction direction)
        => new(index.Row + direction.DRow, index.Col + direction.DCol);
}

internal readonly record struct Direction(int DRow, int DCol)
{
    public static readonly Direction[] PossibleDirections =
    [
        new(0, 1), // Right
        new(1, 0), // Down
        new(0, -1), // Left
        new(-1, 0), // Up
    ];
}

internal static class Extensions
{
    public static T Get<T>(this T[][] array, Index2D index) => array[index.Row][index.Col];

    public static void Set<T>(this T[][] array, Index2D index, T value) => array[index.Row][index.Col] = value;
}

internal class PathFinder(int width, int height)
{
    private readonly Index2D[][] _cameFromMap = Array2D.CreateJagged(height, width, new Index2D(-1, -1));
    private readonly Queue<Index2D> _pathExistsQueue = new(height * width);

    private readonly BitArray _pathExistsVisited = new(height * width);
    private readonly Queue<Index2D> _shortestPathQueue = new(height * width);

    public List<Index2D>? GetShortestPath(Index2D from, Index2D to, CellType[][] grid)
    {
        _cameFromMap.Fill(new Index2D(-1, -1));
        _cameFromMap[from.Row][from.Col] = from;

        _shortestPathQueue.Clear();
        _shortestPathQueue.Enqueue(from);

        while (_shortestPathQueue.Count > 0)
        {
            Index2D currentPos = _shortestPathQueue.Dequeue();

            if (currentPos == to) break;

            foreach (Direction direction in Direction.PossibleDirections)
            {
                Index2D next = currentPos + direction;

                if (IsIndexOutsideBounds(next)) continue;

                // If the next cell is the target, we assume that we can always step onto it even if it is occupied.
                if (next != to)
                {
                    if (grid.Get(next) is CellType.SnakeHead or CellType.SnakeBody)
                        continue;
                }

                if (_cameFromMap.Get(next) != new Index2D(-1, -1)) continue;

                _cameFromMap[next.Row][next.Col] = currentPos;
                _shortestPathQueue.Enqueue(next);
            }
        }

        if (_cameFromMap[to.Row][to.Col] == new Index2D(-1, -1)) return null;

        List<Index2D> path = new();
        Index2D current = to;
        while (current != from)
        {
            path.Add(current);
            current = _cameFromMap.Get(current);
        }

        path.Add(current);

        path.Reverse();
        return path;
    }

    public bool PathExists(Index2D from, Index2D to, CellType[][] grid)
    {
        _pathExistsVisited.SetAll(false);
        _pathExistsVisited[from.Row * width + from.Col] = true;

        _pathExistsQueue.Clear();
        _pathExistsQueue.Enqueue(from);

        while (_pathExistsQueue.Count > 0)
        {
            Index2D currentPos = _pathExistsQueue.Dequeue();

            if (currentPos == to) return true;

            foreach (Direction direction in Direction.PossibleDirections)
            {
                Index2D next = currentPos + direction;

                if (IsIndexOutsideBounds(next)) continue;
                if (grid.Get(next) is CellType.SnakeHead or CellType.SnakeBody)
                    continue;
                if (_pathExistsVisited[next.Row * width + next.Col]) continue;
                _pathExistsVisited[next.Row * width + next.Col] = true;

                _pathExistsQueue.Enqueue(next);
            }
        }

        return false;
    }

    private bool IsIndexOutsideBounds(Index2D index) =>
        index.Row < 0 || index.Row >= height || index.Col < 0 || index.Col >= width;
}

internal class CircularBuffer<T>(int capacity)
{
    private readonly T[] _buffer = new T[capacity];
    private int _start;

    public CircularBuffer(CircularBuffer<T> other) : this(other._buffer.Length)
    {
        _buffer = new T[other._buffer.Length];
        Array.Copy(other._buffer, _buffer, other._buffer.Length);
        _start = other._start;
        Count = other.Count;
    }

    public int Count { get; private set; }

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            int actualIndex = (_start + index) % _buffer.Length;
            return _buffer[actualIndex];
        }
    }

    public bool AddFront(T item)
    {
        if (Count == _buffer.Length) return false;

        _start = (_start - 1 + _buffer.Length) % _buffer.Length;
        _buffer[_start] = item;
        Count++;
        return true;
    }

    public bool AddBack(T item)
    {
        if (Count == _buffer.Length) return false;

        int end = (_start + Count) % _buffer.Length;
        _buffer[end] = item;
        Count++;
        return true;
    }

    public T RemoveFront()
    {
        if (Count == 0) throw new InvalidOperationException("Buffer is empty");

        T item = _buffer[_start];
        _start = (_start + 1) % _buffer.Length;
        Count--;
        return item;
    }

    public T RemoveBack()
    {
        if (Count == 0) throw new InvalidOperationException("Buffer is empty");

        int endIndex = (_start + Count - 1) % _buffer.Length;
        T item = _buffer[endIndex];
        Count--;
        return item;
    }

    public T GetBack()
    {
        if (Count == 0) throw new InvalidOperationException("Buffer is empty");

        int endIndex = (_start + Count - 1) % _buffer.Length;
        return _buffer[endIndex];
    }

    public T GetFront()
    {
        if (Count == 0) throw new InvalidOperationException("Buffer is empty");

        return _buffer[_start];
    }

    public void Clear()
    {
        _start = 0;
        Count = 0;
    }

    public void FillFrom(CircularBuffer<T> other)
    {
        Array.Copy(other._buffer, _buffer, other._buffer.Length);
        _start = other._start;
        Count = other.Count;
    }
}
