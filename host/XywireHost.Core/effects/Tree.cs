using XywireHost.Core.core;

namespace XywireHost.Core.effects;

internal class Tree(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[][] _colorsBuffer =
        Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

    private readonly int[][] _mask =
    [
        [0, 0, 0, 0, 1, 1, 0, 0, 0, 0],
        [0, 0, 0, 1, 1, 1, 1, 0, 0, 0],
        [0, 0, 0, 0, 1, 1, 0, 0, 0, 0],
        [0, 0, 1, 1, 1, 1, 1, 1, 0, 0],
        [0, 0, 0, 1, 1, 1, 1, 0, 0, 0],
        [0, 1, 1, 1, 1, 1, 1, 1, 1, 0],
        [0, 0, 1, 1, 1, 1, 1, 1, 0, 0],
        [1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [0, 0, 0, 0, 1, 1, 0, 0, 0, 0],
        [0, 0, 0, 0, 1, 1, 0, 0, 0, 0],
    ];

    protected override void MoveNext()
    {
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                if (_mask[i][j] == 0) continue;
                int changeColor = Random.Shared.Next(100);
                if (changeColor < 10)
                {
                    _colorsBuffer[i][j] = Color.RGB(Random.Shared.Next(255), Random.Shared.Next(255),
                        Random.Shared.Next(255));
                }
                else
                {
                    _colorsBuffer[i][j] = Color.RGB(0, 255, 0);
                }
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 10;
}
