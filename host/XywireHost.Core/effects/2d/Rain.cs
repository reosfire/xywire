using XywireHost.Core.core;

namespace XywireHost.Core.effects._2d;

internal class Rain(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[][] _colorsBuffer =
        Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

    private int _frameNumber = 0;

    protected override void MoveNext()
    {
        if (_frameNumber++ % 5 == 0)
        {
            _colorsBuffer.ShiftDown();

            for (int i = 0; i < LedLine.Width; i++)
            {
                _colorsBuffer[0][i] = _colorsBuffer[1][i].CopyHSV(value: _colorsBuffer[1][i].Value * 0.5);
            }

            _colorsBuffer[0][Random.Shared.Next(LedLine.Width)] = Color.HSV(0, 0, 1);
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;
}
