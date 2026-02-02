using XywireHost.Core.core;

namespace XywireHost.Core.effects._2d;

internal class SnakeGame : AbstractEffect
{
    private readonly Color _emptyColor = Color.RGB(0, 0, 0);
    private readonly Color _foodColor = Color.RGB(255, 0, 0);
    private readonly Color _headColor = Color.RGB(0, 200, 0);
    private readonly Color _snakeColor = Color.RGB(0, 255, 0);

    private readonly Color[][] _colorsBuffer;

    private readonly Random _random = new();
    private readonly List<(int x, int y)> _snake = [];

    private (int dx, int dy) _direction = (1, 0);
    private (int x, int y) _food;
    private (int dx, int dy) _nextDirection = (1, 0);

    private bool _running = true;

    private int _frameNumber = 0;

    public SnakeGame(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

        // Initialize snake in the middle
        int startX = attachedLedLine.Width / 2;
        int startY = attachedLedLine.Height / 2;
        _snake.Add((startX, startY));
        _snake.Add((startX - 1, startY));
        _snake.Add((startX - 2, startY));

        SpawnFood();
    }

    protected override void MoveNext()
    {
        if (_frameNumber++ % 10 != 0)
        {
            LedLine.SetColors(_colorsBuffer);
            return;
        }

        // Update direction (only if it's not opposite to current direction)
        if (_nextDirection.dx != -_direction.dx || _nextDirection.dy != -_direction.dy)
        {
            _direction = _nextDirection;
        }

        // Calculate new head position
        (int x, int y) head = _snake[0];
        (int x, int y) newHead = (x: head.x + _direction.dx, y: head.y + _direction.dy);

        if (newHead.x < 0 || newHead.x >= LedLine.Width || newHead.y < 0 || newHead.y >= LedLine.Height ||
            _snake.Contains(newHead))
        {
            ResetGame();
            return;
        }

        _snake.Insert(0, newHead);

        if (newHead.x == _food.x && newHead.y == _food.y) SpawnFood();
        else _snake.RemoveAt(_snake.Count - 1);

        // Draw
        _colorsBuffer.Fill(_emptyColor);

        for (int i = 0; i < _snake.Count; i++)
        {
            (int x, int y) segment = _snake[i];
            _colorsBuffer[segment.y][segment.x] = i == 0 ? _headColor : _snakeColor;
        }

        _colorsBuffer[_food.y][_food.x] = _foodColor;

        LedLine.SetColors(_colorsBuffer);
    }

    private void SpawnFood()
    {
        List<(int x, int y)> availablePositions = [];

        for (int y = 0; y < LedLine.Height; y++)
        {
            for (int x = 0; x < LedLine.Width; x++)
            {
                if (!_snake.Contains((x, y)))
                {
                    availablePositions.Add((x, y));
                }
            }
        }

        if (availablePositions.Count > 0)
        {
            _food = availablePositions[_random.Next(availablePositions.Count)];
        }
    }

    public override void StartLooping()
    {
        base.StartLooping();

        while (_running)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            HandleInput(key.Key);
        }
    }

    private void HandleInput(ConsoleKey key)
    {
        _nextDirection = key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.W => (0, -1),
            ConsoleKey.DownArrow or ConsoleKey.S => (0, 1),
            ConsoleKey.LeftArrow or ConsoleKey.A => (-1, 0),
            ConsoleKey.RightArrow or ConsoleKey.D => (1, 0),
            _ => _nextDirection,
        };
    }

    private void ResetGame()
    {
        _snake.Clear();
        _direction = (1, 0);
        _nextDirection = (1, 0);

        // Re-initialize snake
        int startX = LedLine.Width / 2;
        int startY = LedLine.Height / 2;
        _snake.Add((startX, startY));
        _snake.Add((startX - 1, startY));
        _snake.Add((startX - 2, startY));

        SpawnFood();
        Console.WriteLine("Game Reset!");
    }

    protected override void ClearResources() => _running = false;

    protected override int StabilizeFps() => 60; // 10 FPS for a smooth but playable snake game
}
