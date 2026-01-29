using Accord.Math;
using Leds.core;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wasapi.CoreAudioApi;
using NAudio.Wave;
using System.Numerics;

namespace Leds.effects
{
    internal class FurrierTests : AbstractEffect
    {
        private Color[] _colorsBuffer;

        private WasapiLoopbackCapture? _capture;

        double[] prev;

        Queue<Complex> fullBuffer = new Queue<Complex>();

        double r = 0;
        double g = 0;
        double b = 0;
        int f = 0;
        readonly object colorLock = new();

        public FurrierTests(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
            BeginCapture();
        }

        protected override void MoveNext()
        {
            _colorsBuffer.Shift();

            lock (colorLock)
            {
                if (prev != null)
                {
                    double max = prev.Max();
                    for (int i = 0; i < 100; i++)
                    {
                        _colorsBuffer[i] = Color.HSV(0, 0, prev[i] / max);
                    }
                }


                //double max = Math.Max(r, Math.Max(g, b));
                //if (Math.Abs(max) < 0.01) max = 1;

                //double sum = r + g + b;

                //if (sum == 0) sum = 1;

                //for (int i = 0; i < 10; i++)
                //{
                //    Color c = Color.RGB(r / max * 255, g / max * 255, b / max * 255);

                //    _colorsBuffer[0][Random.Shared.Next(4)] = Color.HSV(c.Saturation * 360, c.Value, c.Hue / 360);
                //}
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
            Console.WriteLine(_capture.WaveFormat);

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

            int len = e.Buffer.Length / 4;

            Complex[] complexBuffer = new Complex[buffer.FloatBufferCount];
            for (int i = 0; i < complexBuffer.Length; i++)
            {
                complexBuffer[i] = new Complex(buffer.FloatBuffer[i], 0.0);
            }

            foreach (Complex value in complexBuffer)
            {
                fullBuffer.Enqueue(value);
            }
            while (fullBuffer.Count > 10000)
            {
                fullBuffer.Dequeue();
            }

            Complex[] values = fullBuffer.ToArray();

            Fourier.Forward(values, FourierOptions.Default);

            double[] magnitudes = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                magnitudes[i] = values[i].Magnitude;
            }

            int colors = 100;

            double[] results = new double[colors];
            for (int i = 1; i <= colors; i++)
            {
                for (int j = len * (i - 1) / colors; j < len * i / colors; j++)
                {
                    results[i - 1] += magnitudes[j];
                }
            }

            double[] diffs = new double[colors];
            if (prev != null)
            {
                for (int i = 0; i < diffs.Length; i++)
                {
                    double delta = Math.Abs(prev[i] - results[i]);
                    diffs[i] = delta; // / Math.Max(prev[i], results[i]);
                }
            }

            //double[] exped = new double[colors];
            //for (int i = 0; i < exped.Length; i++)
            //{
            //    int start = Math.Max(0, i - 10);
            //    int end = Math.Min(colors - 1, i + 10);
            //    for (int j = start; j < end; j++)
            //    {
            //        exped[i] += diffs[j] / Math.Pow(Math.E, Math.Abs(i - j));
            //    }
            //}

            int maxF = 0;
            double max = 0;
            for (int i = 0; i < diffs.Length; i++)
            {
                if (diffs[i] > max)
                {
                    max = diffs[i];
                    maxF = i;
                }
            }

            double first = 0;
            double second = 0;
            double third = 0;

            for (int i = 0; i < len / 21; i++)
            {
                first += values[i].Magnitude;
            }

            for (int i = len / 21; i < len / 21 * 11; i++)
            {
                second += values[i].Magnitude;
            }

            for (int i = len / 21 * 11; i < len; i++)
            {
                third += values[i].Magnitude;
            }

            lock (colorLock)
            {
                r = first / 6;
                g = second / 6;
                b = third / 6;
                f = maxF;
            }

            int lastNonZero = magnitudes.Length - 1;
            while (lastNonZero > 0)
            {
                if (Math.Abs(magnitudes[lastNonZero]) > 0.1) break; 
                lastNonZero--;
            }

            // Console.WriteLine($"{first} {second} {third}   {f} {lastNonZero}");
            //Console.WriteLine($"{f}");

            //Pair[] indexedResults = new Pair[results.Length];
            //for (int i = 0; i < results.Length; i++)
            //{
            //    indexedResults[i] = new Pair(results[i], i);
            //}
            //indexedResults.Sort((a, b) => b.value.CompareTo(a.value));
            //Console.WriteLine(string.Join(" ", indexedResults.First(50).Select(it => string.Format("{0:000}", it.index))));

            prev = results;
        }

        protected override int StabilizeFps()
        {
            return 60;
        }
    }

    struct Pair
    {
        public double value;
        public int index;

        public Pair(double value, int index)
        {
            this.value = value;
            this.index = index;
        }
    }
}
