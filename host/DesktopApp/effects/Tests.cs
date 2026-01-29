using Leds.core;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leds.effects
{
    internal class Tests : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;

        private Random _random = new Random();

        private double hue = 60.0;
        private double saturation = 0.7;
        private double value = 0;
        private double valueShift = 0.005;

        public Tests(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            _colorsBuffer.Shift();

            if (value + valueShift < 0)
            {
                valueShift *= -1;
                hue = _random.NextDouble() * 360;
                saturation = _random.NextDouble() * 0.3 + 0.7;
            }
            if (value + valueShift > 1.0)
            {
                valueShift *= -1;
            }
            value += valueShift;

            for (int i = 0; i < 100; i++)
            {
                double distanceToAccent = Math.Abs(i / 100.0 - value);
                _colorsBuffer[i] = Color.HSV(hue, saturation, value * Math.Pow(1 - distanceToAccent, 6));
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 1000;
        }
    }
}
