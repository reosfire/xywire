using Leds.core;

namespace Leds.effects._2d
{
    internal class GameOfLife : AbstractEffect
    {
        private readonly Color[][] _colorsBuffer;
        private List<bool[][]> _statesHistory = new();
        
        private readonly Random _random = new Random();
        private readonly Color _aliveColor = Color.RGB(0, 255, 0);
        private readonly Color _deadColor = Color.RGB(0, 0, 0);
        
        private int _iterationsUntilRestart = -1;

        public GameOfLife(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);
        }

        private int frame = 0;

        protected override void MoveNext()
        {
            if (frame++ % 10 != 0)
            {
                LedLine.SetColors(_colorsBuffer);
                return;
            }
            
            if (_statesHistory.Count == 0) _statesHistory.Add(CreateRandomState());

            var currentState = _statesHistory.Last();
            var nextState = GetNextState(currentState);
            _statesHistory.Add(nextState);
            
            for (int y = 0; y < LedLine.Height; y++)
            {
                for (int x = 0; x < LedLine.Width; x++)
                {
                    _colorsBuffer[y][x] = currentState[y][x] ? _aliveColor : _deadColor;
                }
            }
            LedLine.SetColors(_colorsBuffer);
            
            var stabilized = IsEmptyState(nextState);
            for (int i = 0; i < _statesHistory.Count - 1; i++)
            {
                if (StatesEqual(nextState, _statesHistory[i]))
                {
                    stabilized = true;
                }
            }
            
            if (_iterationsUntilRestart == -1 && stabilized)
            {
                _iterationsUntilRestart = 10;
            }
            
            if (_iterationsUntilRestart > 0)
            {
                _iterationsUntilRestart--;
            }
            
            if (_iterationsUntilRestart == 0)
            {
                _iterationsUntilRestart = -1;
                _statesHistory.Clear();
            }
        }
        
        private bool[][] CreateRandomState()
        {
            var result = new bool[LedLine.Height][];
            
            for (int y = 0; y < LedLine.Height; y++)
            {
                result[y] = new bool[LedLine.Width];
                
                for (int x = 0; x < LedLine.Width; x++)
                {
                    result[y][x] = _random.NextDouble() < 0.3;
                }
            }

            return result;
        }

        private bool[][] GetNextState(bool[][] state)
        {
            var nextState = new bool[LedLine.Height][];
            
            for (int y = 0; y < LedLine.Height; y++)
            {
                nextState[y] = new bool[LedLine.Width];
                for (int x = 0; x < LedLine.Width; x++)
                {
                    int aliveNeighbors = CountAliveNeighbors(state, x, y);
                    bool currentlyAlive = state[y][x];
                    
                    if (currentlyAlive)
                    {
                        nextState[y][x] = aliveNeighbors == 2 || aliveNeighbors == 3;
                    }
                    else
                    {
                        nextState[y][x] = aliveNeighbors == 3;
                    }
                }
            }
            
            return nextState;
        }

        private int CountAliveNeighbors(bool[][] state, int x, int y)
        {
            int count = 0;
            
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    // Skip the center cell
                    if (dx == 0 && dy == 0) continue;
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx >= 0 && nx < LedLine.Width && ny >= 0 && ny < LedLine.Height && state[ny][nx])
                    {
                        count++;
                    }
                }
            }
            
            return count;
        }
        
        private bool StatesEqual(bool[][] state1, bool[][] state2)
        {
            for (int y = 0; y < LedLine.Height; y++)
            {
                for (int x = 0; x < LedLine.Width; x++)
                {
                    if (state1[y][x] != state2[y][x])
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        
        private bool IsEmptyState(bool[][] state)
        {
            for (int y = 0; y < LedLine.Height; y++)
            {
                for (int x = 0; x < LedLine.Width; x++)
                {
                    if (state[y][x])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        protected override int StabilizeFps()
        {
            return 20;
        }
    }
}



