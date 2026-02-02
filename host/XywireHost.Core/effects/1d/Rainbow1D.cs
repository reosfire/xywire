using XywireHost.Core.core;

namespace XywireHost.Core.effects._1d;

internal class Rainbow1D(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private double _hueOffset;

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            double hue = (i * 2 + _hueOffset) % 360;
            _colorsBuffer[i] = Color.HSV(hue, 1.0, 1.0);
        }

        _hueOffset += 1.0;
        if (_hueOffset > 360) _hueOffset -= 360;

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;
}

internal class Fireworks1D(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private readonly Random _random = new();

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(value: _colorsBuffer[i].Value * 0.9);
        }

        if (_random.NextDouble() < 0.2)
        {
            int center = _random.Next(_colorsBuffer.Length);
            double hue = _random.Next(360);
            for (int i = -5; i <= 5; i++)
            {
                int pos = (center + i + _colorsBuffer.Length) % _colorsBuffer.Length;
                double distance = Math.Abs(i) / 5.0;
                _colorsBuffer[pos] = Color.HSV(hue, 1.0, 1.0 * Math.Pow(1 - distance, 2));
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 30;
}

internal class ColorPulse(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private readonly Random _random = new();
    private double _hue = 10;
    private double _saturation = 0.7;
    private double _pulsePosition;
    private bool _forward = true;

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            double distance = Math.Abs(i / (double)_colorsBuffer.Length - _pulsePosition);
            _colorsBuffer[i] = Color.HSV(_hue, _saturation, Math.Max(0, 1 - distance * 10));
        }

        if (_forward)
        {
            _pulsePosition += 0.01;
            if (_pulsePosition >= 1.0)
            {
                _forward = false;
                _hue = _random.NextDouble() * 360;
                _saturation = _random.NextDouble() * 0.3 + 0.7;
            }
        }
        else
        {
            _pulsePosition -= 0.01;
            if (_pulsePosition <= 0.0)
            {
                _forward = true;
                _hue = _random.NextDouble() * 360;
                _saturation = _random.NextDouble() * 0.3 + 0.7;
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 30;
}

internal class TwinkleStars(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private const double TwinkleProbability = 0.05;
    private const double FadeFactorSaturation = 0.998;
    private const double FadeFactorValue = 0.995;

    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private readonly Random _random = new();

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            if (_random.NextDouble() < TwinkleProbability)
            {
                if (_colorsBuffer[i].Value > 0.01) continue;
                double hue = _random.Next(360);
                double saturation = _random.NextDouble() % 0.2 + 0.8;
                _colorsBuffer[i] = Color.HSV(hue, saturation, 1.0);
            }
            else
            {
                _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(
                    saturation: _colorsBuffer[i].Saturation * FadeFactorSaturation,
                    value: _colorsBuffer[i].Value * FadeFactorValue
                );
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;
}

internal class GameOfLife1D : AbstractEffect
{
    private readonly Color[] _colorsBuffer;
    private readonly Random _random = new();
    private bool[] _currentState;

    public GameOfLife1D(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _currentState = new bool[attachedLedLine.LedsCount];
        _colorsBuffer = new Color[attachedLedLine.LedsCount];

        for (int i = 0; i < _currentState.Length; i++)
        {
            _currentState[i] = _random.NextDouble() > 0.5;
        }
    }

    protected override void MoveNext()
    {
        bool[] nextState = new bool[_currentState.Length];

        for (int i = 0; i < _currentState.Length; i++)
        {
            int left2 = (i - 2 + _currentState.Length) % _currentState.Length;
            int left1 = (i - 1 + _currentState.Length) % _currentState.Length;
            int right1 = (i + 1) % _currentState.Length;
            int right2 = (i + 2) % _currentState.Length;

            int aliveNeighbors = (_currentState[left2] ? 1 : 0) +
                                 (_currentState[left1] ? 1 : 0) +
                                 (_currentState[right1] ? 1 : 0) +
                                 (_currentState[right2] ? 1 : 0);

            if (_currentState[i])
            {
                nextState[i] = aliveNeighbors is 2 or 4;
            }
            else
            {
                nextState[i] = aliveNeighbors is 2 or 3;
            }
        }

        nextState[_random.Next(100)] = true;
        _currentState = nextState;

        // Update the colors based on the new state
        for (int i = 0; i < _currentState.Length; i++)
        {
            _colorsBuffer[i] = _currentState[i]
                ? Color.HSV(240, 1.0, 1.0)
                : Color.HSV(0, 0, 0.1);
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 2;
}
