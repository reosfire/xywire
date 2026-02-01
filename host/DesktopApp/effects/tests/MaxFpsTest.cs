using Leds.core;

namespace Leds.effects.tests;

internal class MaxFpsTest(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private int _position;

    protected override void MoveNext()
    {
        int previousPosition = _position;

        _position++;
        if (_position >= LedLine.LedsCount) _position = 0;

        _colorsBuffer[previousPosition] = Color.RGB(0, 0, 0);
        _colorsBuffer[_position] = Color.RGB(255, 255, 255);

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 500;
}
