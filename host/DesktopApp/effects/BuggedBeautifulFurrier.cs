using Leds.core;
using MathNet.Numerics.IntegralTransforms;
using Microsoft.VisualBasic;
using NAudio.Wave;
using System.Numerics;
using NAudio.CoreAudioApi;

namespace Leds.effects
{
    internal class BuggedBeautifulFurrier : AbstractEffect
    {
        private readonly Color[][] _colorsBuffer;

        private WasapiLoopbackCapture? _capture;

        double r = 0;
        double g = 0;
        double b = 0;
        readonly object colorLock = new();

        public BuggedBeautifulFurrier(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

            BeginCapture();
        }

        int frame = 0;

        protected override void MoveNext()
        {
            if (frame++ % 2 == 0)
            {
                LedLine.SetColors(_colorsBuffer);
                return;
            }

            int upMask = 0b1010101_1010101;
            int downMask = ~upMask;
            _colorsBuffer.ShiftUp(upMask);
            _colorsBuffer.ShiftDown(downMask);
            // _colorsBuffer.Shift();

            lock (colorLock)
            {
                double max = Math.Max(r, Math.Max(g, b));
                if (Math.Abs(max) < 0.01) max = 1;

                int tries = LedLine.Width / 2;
                for (int i = 0; i < tries; i++)
                {
                    Color c = Color.RGB(r / max * 255, g / max * 255, b / max * 255);
                    Color resultColor = Color.HSV(c.Saturation * 360, c.Value, c.Hue / 360);

                    int index = Random.Shared.Next(LedLine.Width);
                    if (((upMask >> index) & 1) == 0)
                    {
                        Color currentValue = _colorsBuffer[0][index];
                        _colorsBuffer[0][index] = Color.Lerp(currentValue, resultColor, 0.5);
                    }
                    else
                    {
                        Color currentValue = _colorsBuffer[LedLine.Height - 1][index];
                        _colorsBuffer[LedLine.Height - 1][index] = Color.Lerp(currentValue, resultColor, 0.5);
                    }
                    
                    // Color currentValue = _colorsBuffer[0][index];
                    // _colorsBuffer[0][index] = Color.Lerp(currentValue, resultColor, 0.5);
                }
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override void ClearResources()
        {
            _capture?.StopRecording();
        }

        private void BeginCapture()
        {
            var captureDevice = WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice();
            _capture = new WasapiLoopbackCapture(captureDevice);

            _capture.DataAvailable += PCMAvailable;

            _capture.RecordingStopped += (s, a) =>
            {
                _capture.Dispose();
            };

            _capture.StartRecording();
        }

        private void PCMAvailable(object? sender, WaveInEventArgs e)
        {
            WaveBuffer buffer = new WaveBuffer(e.Buffer);

            int len = e.BytesRecorded / 4;

            Complex[] values = buffer.FloatBuffer
                .Take(len)
                .Select(it => new Complex(it, 0.0)).ToArray();

            Fourier.Forward(values, FourierOptions.Default);

            double[] magnitudes = values.Select(it => it.Magnitude).ToArray();

            double[] slices = new double[3];

            for (int i = 0; i < 3; i++)
            {
                for (int j = len / 3 * i; j < len / 3 * (i + 1); j++)
                {
                    slices[i] += magnitudes[j];
                }
            }

            lock (colorLock)
            {
                r = slices[0];
                g = slices[1];
                b = slices[2];
            }

            // Console.WriteLine($"{r} {g} {b}");
        }

        protected override int StabilizeFps()
        {
            return 144;
        }
    }
    
    internal class StraightBuggedBeautifulFurrier : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;

        private WasapiLoopbackCapture? _capture;

        double r = 0;
        double g = 0;
        double b = 0;
        readonly object colorLock = new();

        public StraightBuggedBeautifulFurrier(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
            BeginCapture();
        }

        double prevR = 0;
        double prevG = 0;
        double prevB = 0;

        protected override void MoveNext()
        {
            _colorsBuffer.Shift();

            lock (colorLock)
            {
                double responsiveness = 0.9;
                r *= responsiveness;
                g *= responsiveness;
                b *= responsiveness;
                r += prevR * (1 - responsiveness);
                g += prevG * (1 - responsiveness);
                b += prevB * (1 - responsiveness);

                double max = Math.Max(r, Math.Max(g, b));
                if (Math.Abs(max) < 0.01) max = 1;

                Color c = Color.RGB(r / max * 255, g / max * 255, b / max * 255);
                _colorsBuffer[0] = Color.HSV(c.Saturation * 360, c.Value, c.Hue / 360);

                prevR = r;
                prevG = g;
                prevB = b;
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override void ClearResources()
        {
            _capture?.StopRecording();
        }

        private void BeginCapture()
        {
            _capture = new WasapiLoopbackCapture();

            _capture.DataAvailable += PCMAvailable;

            _capture.RecordingStopped += (s, a) =>
            {
                _capture.Dispose();
            };

            _capture.StartRecording();
        }

        private void PCMAvailable(object? sender, WaveInEventArgs e)
        {
            WaveBuffer buffer = new WaveBuffer(e.Buffer);

            int len = e.BytesRecorded / 4;

            Complex[] values = buffer.FloatBuffer
                .Take(len)
                .Select(it => new Complex(it, 0.0)).ToArray();

            Fourier.Forward(values, FourierOptions.Default);

            double[] magnitudes = values.Select(it => it.Magnitude).ToArray();

            int slicesCount = 3;

            double[] slices = new double[slicesCount];

            for (int i = 0; i < slicesCount; i++)
            {
                for (int j = len / slicesCount * i; j < len / slicesCount * (i + 1); j++)
                {
                    slices[i] += magnitudes[j];
                }
            }

            lock (colorLock)
            {
                r = slices[0];
                g = slices[1];
                b = slices[2];
            }

            // Console.WriteLine($"{r} {g} {b}");
        }

        protected override int StabilizeFps()
        {
            return 300;
        }
    }

    internal class StraightBuggedBeautifulFurrierMic : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;

        private WasapiCapture? _capture;

        double r = 0;
        double g = 0;
        double b = 0;
        readonly object colorLock = new();

        public StraightBuggedBeautifulFurrierMic(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
            BeginCapture();
        }

        protected override void MoveNext()
        {
            _colorsBuffer.Shift();

            lock (colorLock)
            {
                double max = Math.Max(r, Math.Max(g, b));
                if (Math.Abs(max) < 0.01) max = 1;

                Color c = Color.RGB(r / max * 255, g / max * 255, b / max * 255);
                for (int i = 0; i < 1; i++)
                {
                    _colorsBuffer[i] = Color.HSV(c.Saturation * 360, c.Value, c.Hue / 360);

                }
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override void ClearResources()
        {
            _capture?.StopRecording();
        }

        private void BeginCapture()
        {
            _capture = new WasapiCapture();

            _capture.DataAvailable += PCMAvailable;

            _capture.RecordingStopped += (s, a) =>
            {
                _capture.Dispose();
            };

            _capture.StartRecording();
        }

        private void PCMAvailable(object? sender, WaveInEventArgs e)
        {
            WaveBuffer buffer = new WaveBuffer(e.Buffer);

            int len = e.BytesRecorded / 4;

            Complex[] values = buffer.FloatBuffer
                .Take(len)
                .Select(it => new Complex(it, 0.0)).ToArray();

            Fourier.Forward(values, FourierOptions.Default);

            double[] magnitudes = values.Select(it => it.Magnitude).ToArray();

            int slicesCount = 6;

            double[] slices = new double[slicesCount];

            for (int i = 0; i < slicesCount; i++)
            {
                for (int j = len / slicesCount * i; j < len / slicesCount * (i + 1); j++)
                {
                    slices[i] += magnitudes[j];
                }
            }

            lock (colorLock)
            {
                r = slices[0];
                g = slices[1] + slices[2] + slices[3] + slices[4];
                b = slices[5];
            }

            Console.WriteLine($"{r} {g} {b}");
        }

        protected override int StabilizeFps()
        {
            return 1000;
        }
    }
}
