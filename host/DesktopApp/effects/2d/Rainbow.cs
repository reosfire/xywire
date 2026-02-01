using Leds.core;

namespace Leds.effects._2d;

internal class Rainbow(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[][] _colorsBuffer =
        Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

    private int _counter = 0;

    protected override void MoveNext()
    {
        _colorsBuffer.ShiftDown();
        for (int i = 0; i < LedLine.Width; i++)
        {
            _colorsBuffer[0][i] = Color.HSV(_counter++ % 360, 1, 1);
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;
}
