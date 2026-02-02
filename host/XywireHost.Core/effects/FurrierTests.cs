using System.Numerics;
using Accord.Math;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;
using XywireHost.Core.core;

namespace XywireHost.Core.effects;

internal class FurrierTests : AbstractEffect
{
    private readonly Color[] _colorsBuffer;
    private readonly object _colorLock = new();

    private readonly Queue<Complex> _fullBuffer = new();

    private WasapiLoopbackCapture? _capture;
    private double _r = 0;
    private double _g = 0;
    private double _b = 0;
    private int _f = 0;

    private double[]? _prev = null;


    public FurrierTests(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = new Color[attachedLedLine.LedsCount];
        BeginCapture();
    }

    protected override void MoveNext()
    {
        _colorsBuffer.Shift();

        lock (_colorLock)
        {
            if (_prev != null)
            {
                double max = _prev.Max();
                for (int i = 0; i < 100; i++)
                {
                    _colorsBuffer[i] = Color.HSV(0, 0, _prev[i] / max);
                }
            }


            //double max = Math.Max(r, Math.Max(g, b));
            //if (Math.Abs(max) < 0.01) max = 1;

            //double sum = _r + _g + _b;

            //if (sum == 0) sum = 1;

            //for (int i = 0; i < 10; i++)
            //{
            //    Color c = Color.RGB(r / max * 255, g / max * 255, b / max * 255);

            //    _colorsBuffer[0][Random.Shared.Next(4)] = Color.HSV(c.Saturation * 360, c.Value, c.Hue / 360);
            //}
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override void ClearResources() => _capture?.StopRecording();

    private void BeginCapture()
    {
        _capture = new WasapiLoopbackCapture();
        Console.WriteLine(_capture.WaveFormat);

        _capture.DataAvailable += PcmAvailable;

        _capture.RecordingStopped += (_, _) =>
        {
            _capture.Dispose();
        };

        _capture.StartRecording();
    }

    private void PcmAvailable(object? sender, WaveInEventArgs e)
    {
        WaveBuffer buffer = new(e.Buffer);

        int len = e.Buffer.Length / 4;

        Complex[] complexBuffer = new Complex[buffer.FloatBufferCount];
        for (int i = 0; i < complexBuffer.Length; i++)
        {
            complexBuffer[i] = new Complex(buffer.FloatBuffer[i], 0.0);
        }

        foreach (Complex value in complexBuffer)
        {
            _fullBuffer.Enqueue(value);
        }

        while (_fullBuffer.Count > 10000)
        {
            _fullBuffer.Dequeue();
        }

        Complex[] values = _fullBuffer.ToArray();

        Fourier.Forward(values, FourierOptions.Default);

        double[] magnitudes = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            magnitudes[i] = values[i].Magnitude;
        }

        const int colors = 100;

        double[] results = new double[colors];
        for (int i = 1; i <= colors; i++)
        {
            for (int j = len * (i - 1) / colors; j < len * i / colors; j++)
            {
                results[i - 1] += magnitudes[j];
            }
        }

        double[] diffs = new double[colors];
        if (_prev != null)
        {
            for (int i = 0; i < diffs.Length; i++)
            {
                double delta = Math.Abs(_prev[i] - results[i]);
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
            if (!(diffs[i] > max)) continue;
            max = diffs[i];
            maxF = i;
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

        lock (_colorLock)
        {
            _r = first / 6;
            _g = second / 6;
            _b = third / 6;
            _f = maxF;
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

        _prev = results;
    }

    protected override int StabilizeFps() => 60;
}

internal struct Pair(double value, int index)
{
    public double Value = value;
    public int Index = index;
}
