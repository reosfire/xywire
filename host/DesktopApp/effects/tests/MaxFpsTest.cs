using Leds.core;

namespace Leds.effects.tests;

internal class MaxFpsTest : AbstractEffect
{
    private readonly Color[] _colorsBuffer;
    private int _position;

    public MaxFpsTest(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = new Color[attachedLedLine.LedsCount];
    }

    protected override void MoveNext()
    {
        var previousPosition = _position;
        
        _position++;
        if (_position >= LedLine.LedsCount) _position = 0;
        
        _colorsBuffer[previousPosition] = Color.RGB(0, 0, 0);
        _colorsBuffer[_position] = Color.RGB(255, 255, 255);

        LedLine.SetColors(_colorsBuffer);
    }
    
    protected override int StabilizeFps()
    {
        return 500;
    }
}