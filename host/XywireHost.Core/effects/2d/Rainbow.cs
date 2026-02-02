using XywireHost.Core.core;

namespace XywireHost.Core.effects._2d;

internal class Rainbow(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[][] _colorsBuffer =
        Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

    private int _counter = 0;

    protected override void MoveNext()
    {
        _colorsBuffer.ShiftDown();

        double stepHueOffset = 360.0 / LedLine.Width;

        for (int i = 0; i < LedLine.Width; i++)
        {
            _colorsBuffer[0][i] = Color.HSV((_counter + stepHueOffset * i) % 360, 1, 1);
        }

        _counter++;

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;
}
