using Leds.core;

namespace Leds.effects._2d;

internal class GameOfLife(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color _aliveColor = Color.RGB(0, 255, 0);
    private readonly Color _deadColor = Color.RGB(0, 0, 0);

    private readonly Color[][] _colorsBuffer =
        Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

    private readonly Random _random = new();
    private readonly List<bool[][]> _statesHistory = [];

    private int _frameNumber = 0;
    private int _iterationsUntilRestart = -1;

    protected override void MoveNext()
    {
        if (_frameNumber++ % 10 != 0)
        {
            LedLine.SetColors(_colorsBuffer);
            return;
        }

        if (_statesHistory.Count == 0) _statesHistory.Add(CreateRandomState());

        bool[][] currentState = _statesHistory.Last();
        bool[][] nextState = GetNextState(currentState);
        _statesHistory.Add(nextState);

        for (int y = 0; y < LedLine.Height; y++)
        {
            for (int x = 0; x < LedLine.Width; x++)
            {
                _colorsBuffer[y][x] = currentState[y][x] ? _aliveColor : _deadColor;
            }
        }

        LedLine.SetColors(_colorsBuffer);

        bool stabilized = IsEmptyState(nextState);
        for (int i = 0; i < _statesHistory.Count - 1; i++)
        {
            if (StatesEqual(nextState, _statesHistory[i]))
            {
                stabilized = true;
                break;
            }
        }

        if (_iterationsUntilRestart == -1 && stabilized) _iterationsUntilRestart = 10;

        if (_iterationsUntilRestart > 0) _iterationsUntilRestart--;

        if (_iterationsUntilRestart == 0)
        {
            _iterationsUntilRestart = -1;
            _statesHistory.Clear();
        }
    }

    private bool[][] CreateRandomState()
    {
        bool[][] result = new bool[LedLine.Height][];

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
        bool[][] nextState = new bool[LedLine.Height][];

        for (int y = 0; y < LedLine.Height; y++)
        {
            nextState[y] = new bool[LedLine.Width];
            for (int x = 0; x < LedLine.Width; x++)
            {
                int aliveNeighbors = CountAliveNeighbors(state, x, y);
                bool currentlyAlive = state[y][x];

                if (currentlyAlive)
                {
                    nextState[y][x] = aliveNeighbors is 2 or 3;
                }
                else
                {
                    nextState[y][x] = aliveNeighbors == 3;
                }
            }
        }

        return nextState;
    }

    private int CountAliveNeighbors(bool[][] state, int i, int j)
    {
        int count = 0;

        for (int di = -1; di <= 1; di++)
        {
            for (int dj = -1; dj <= 1; dj++)
            {
                if (dj == 0 && di == 0) continue;

                int offsetI = i + di;
                int offsetJ = j + dj;

                if (offsetI < 0 || offsetI >= LedLine.Height) continue;
                if (offsetJ < 0 || offsetJ >= LedLine.Width) continue;
                if (!state[offsetI][offsetJ]) continue;

                count++;
            }
        }

        return count;
    }

    private bool StatesEqual(bool[][] state1, bool[][] state2)
    {
        for (int i = 0; i < LedLine.Height; i++)
        {
            for (int j = 0; j < LedLine.Width; j++)
            {
                if (state1[i][j] != state2[i][j]) return false;
            }
        }

        return true;
    }

    private bool IsEmptyState(bool[][] state)
    {
        for (int i = 0; i < LedLine.Height; i++)
        {
            for (int j = 0; j < LedLine.Width; j++)
            {
                if (state[i][j]) return false;
            }
        }

        return true;
    }

    protected override int StabilizeFps() => 20;
}
