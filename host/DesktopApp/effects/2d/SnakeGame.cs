using Leds.core;

namespace Leds.effects._2d
{
    internal class SnakeGame : AbstractEffect
    {
        private Color[][] _colorsBuffer;
        private List<(int x, int y)> _snake = new();
        private (int x, int y) _food;
        private (int dx, int dy) _direction = (1, 0); // Start moving right
        private (int dx, int dy) _nextDirection = (1, 0);
        private Random _random = new Random();
        
        private readonly Color _snakeColor = Color.RGB(0, 255, 0);
        private readonly Color _headColor = Color.RGB(0, 200, 0);
        private readonly Color _foodColor = Color.RGB(255, 0, 0);
        private readonly Color _emptyColor = Color.RGB(0, 0, 0);
        
        private bool _running = true;

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
        
        private int currentFrame = 0;
        
        protected override void MoveNext()
        {
            if (currentFrame++ % 10 != 0)
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
            var head = _snake[0];
            var newHead = (x: head.x + _direction.dx, y: head.y + _direction.dy);
            
            // Check for collision with walls or self
            if (newHead.x < 0 || newHead.x >= LedLine.Width || newHead.y < 0 || newHead.y >= LedLine.Height || _snake.Contains(newHead))
            {
                ResetGame();
                return;
            }
            
            // Add new head
            _snake.Insert(0, newHead);
            
            // Check if food is eaten
            if (newHead.x == _food.x && newHead.y == _food.y)
            {
                SpawnFood();
            }
            else
            {
                // Remove tail if no food eaten
                _snake.RemoveAt(_snake.Count - 1);
            }
            
            // Clear buffer
            for (int y = 0; y < LedLine.Height; y++)
            {
                for (int x = 0; x < LedLine.Width; x++)
                {
                    _colorsBuffer[y][x] = _emptyColor;
                }
            }
            
            // Draw snake
            for (int i = 0; i < _snake.Count; i++)
            {
                var segment = _snake[i];
                _colorsBuffer[segment.y][segment.x] = i == 0 ? _headColor : _snakeColor;
            }
            
            // Draw food
            _colorsBuffer[_food.y][_food.x] = _foodColor;
            
            LedLine.SetColors(_colorsBuffer);
        }
        
        private void SpawnFood()
        {
            List<(int x, int y)> availablePositions = new();
            
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
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }
                HandleInput(key.Key);
            }
        }
        
        private void HandleInput(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    _nextDirection = (0, -1);
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    _nextDirection = (0, 1);
                    break;
                case ConsoleKey.LeftArrow:
                case ConsoleKey.A:
                    _nextDirection = (-1, 0);
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.D:
                    _nextDirection = (1, 0);
                    break;
            }
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

        protected override void ClearResources()
        {
            _running = false;
        }

        protected override int StabilizeFps()
        {
            return 60; // 10 FPS for a smooth but playable snake game
        }
    }
}

