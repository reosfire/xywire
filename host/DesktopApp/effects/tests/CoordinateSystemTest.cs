using Leds.core;

namespace Leds.effects.tests
{
    internal class CoordinateSystemTest : AbstractEffect
    {
        private readonly Color[][] _colorsBuffer;

        public CoordinateSystemTest(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);
            
            for (int row = 0; row < LedLine.Height; row++)
            {
                for (int col = 0; col < LedLine.Width; col++)
                {
                    _colorsBuffer[row][col] = Color.RGB(0, 0, 0);
                }
            }
            
            for (int col = 0; col < LedLine.Width; col++)
            {
                int redValue = (int)((double)col / (LedLine.Width - 1) * 255);
                _colorsBuffer[0][col] = Color.RGB(redValue, 0, 0);
            }
            
            for (int row = 0; row < LedLine.Height; row++)
            {
                int greenValue = (int)((double)row / (LedLine.Height - 1) * 255);
                _colorsBuffer[row][0] = Color.RGB(0, greenValue, 0);
            }
        }

        protected override void MoveNext()
        {
            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 5;
        }
    }
}

