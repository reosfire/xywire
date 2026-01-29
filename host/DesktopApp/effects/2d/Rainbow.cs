using Leds.core;

namespace Leds.effects._2d
{
    internal class Rainbow : AbstractEffect
    {
        private readonly Color[][] _colorsBuffer;

        private int _counter = 0;

        public Rainbow(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);
        }

        protected override void MoveNext()
        {
            _colorsBuffer.Shift();
            for (int i = 0; i < LedLine.Width; i++)
            {
                _colorsBuffer[0][i] = Color.HSV(_counter++ % 360, 1, 1);
            }
            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 60;
        }
    }
}
