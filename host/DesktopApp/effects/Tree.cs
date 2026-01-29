using Leds.core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leds.effects
{
    internal class Tree : AbstractEffect
    {
        private readonly Color[][] _colorsBuffer;

        private readonly int[][] _mask = new int[][]
        {
            new int[] { 0, 0, 0, 0, 1,  1, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 1, 1,  1, 1, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 1,  1, 0, 0, 0, 0 },
            new int[] { 0, 0, 1, 1, 1,  1, 1, 1, 0, 0 },
            new int[] { 0, 0, 0, 1, 1,  1, 1, 0, 0, 0 },

            new int[] { 0, 1, 1, 1, 1,  1, 1, 1, 1, 0 },
            new int[] { 0, 0, 1, 1, 1,  1, 1, 1, 0, 0 },
            new int[] { 1, 1, 1, 1, 1,  1, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 1,  1, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 1,  1, 0, 0, 0, 0 },
        };

        public Tree(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    if (_mask[i][j] == 0) continue;
                    int chabgeColor = Random.Shared.Next(100);
                    if (chabgeColor < 10)
                    {
                        _colorsBuffer[i][j] = Color.RGB(Random.Shared.Next(255), Random.Shared.Next(255), Random.Shared.Next(255));
                    }
                    else _colorsBuffer[i][j] = Color.RGB(0, 255, 0);
                }
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 10;
        }
    }
}
