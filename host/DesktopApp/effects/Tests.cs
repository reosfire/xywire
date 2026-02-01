using Leds.core;

namespace Leds.effects;

internal class Tests(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];

    private readonly Random _random = new();

    private double _hue = 60.0;
    private double _saturation = 0.7;
    private double _value = 0;
    private double _valueShift = 0.005;

    protected override void MoveNext()
    {
        _colorsBuffer.Shift();

        if (_value + _valueShift < 0)
        {
            _valueShift *= -1;
            _hue = _random.NextDouble() * 360;
            _saturation = _random.NextDouble() * 0.3 + 0.7;
        }

        if (_value + _valueShift > 1.0)
        {
            _valueShift *= -1;
        }

        _value += _valueShift;

        for (int i = 0; i < 100; i++)
        {
            double distanceToAccent = Math.Abs(i / 100.0 - _value);
            _colorsBuffer[i] = Color.HSV(_hue, _saturation, _value * Math.Pow(1 - distanceToAccent, 6));
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 1000;
}
