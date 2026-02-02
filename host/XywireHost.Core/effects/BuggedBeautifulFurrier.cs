using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using XywireHost.Core.core;

namespace XywireHost.Core.effects;

internal class BuggedBeautifulFurrier : AbstractEffect
{
    private readonly Color[][] _colorsBuffer;
    private readonly object _colorLock = new();

    private WasapiLoopbackCapture? _capture;
    private double _r = 0;
    private double _g = 0;
    private double _b = 0;

    private int _frameNumber = 0;

    public BuggedBeautifulFurrier(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

        BeginCapture();
    }

    protected override void MoveNext()
    {
        if (_frameNumber++ % 2 == 0)
        {
            LedLine.SetColors(_colorsBuffer);
            return;
        }

        const int upMask = 0b1010101_1010101;
        const int downMask = ~upMask;
        _colorsBuffer.ShiftUp(upMask);
        _colorsBuffer.ShiftDown(downMask);
        // _colorsBuffer.Shift();

        lock (_colorLock)
        {
            double max = Math.Max(_r, Math.Max(_g, _b));
            if (Math.Abs(max) < 0.01) max = 1;

            int tries = LedLine.Width / 2;
            for (int i = 0; i < tries; i++)
            {
                Color c = Color.RGB(_r / max * 255, _g / max * 255, _b / max * 255);
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

    protected override void ClearResources() => _capture?.StopRecording();

    private void BeginCapture()
    {
        MMDevice? captureDevice = WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice();
        _capture = new WasapiLoopbackCapture(captureDevice);

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

        lock (_colorLock)
        {
            _r = slices[0];
            _g = slices[1];
            _b = slices[2];
        }

        // Console.WriteLine($"{r} {g} {b}");
    }

    protected override int StabilizeFps() => 144;
}

internal class StraightBuggedBeautifulFurrier : AbstractEffect
{
    private readonly Color[] _colorsBuffer;
    private readonly object _colorLock = new();

    private WasapiLoopbackCapture? _capture;
    private double _r = 0;
    private double _g = 0;
    private double _b = 0;
    private double _prevR = 0;
    private double _prevG = 0;
    private double _prevB = 0;

    public StraightBuggedBeautifulFurrier(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = new Color[attachedLedLine.LedsCount];
        BeginCapture();
    }

    protected override void MoveNext()
    {
        _colorsBuffer.Shift();

        const double responsiveness = 0.9;
        lock (_colorLock)
        {
            _r *= responsiveness;
            _g *= responsiveness;
            _b *= responsiveness;
            _r += _prevR * (1 - responsiveness);
            _g += _prevG * (1 - responsiveness);
            _b += _prevB * (1 - responsiveness);

            double max = Math.Max(_r, Math.Max(_g, _b));
            if (Math.Abs(max) < 0.01) max = 1;

            Color c = Color.RGB(_r / max * 255, _g / max * 255, _b / max * 255);
            _colorsBuffer[0] = Color.HSV(c.Saturation * 360, c.Value, c.Hue / 360);

            _prevR = _r;
            _prevG = _g;
            _prevB = _b;
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override void ClearResources() => _capture?.StopRecording();

    private void BeginCapture()
    {
        _capture = new WasapiLoopbackCapture();

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

        int len = e.BytesRecorded / 4;

        Complex[] values = buffer.FloatBuffer
            .Take(len)
            .Select(it => new Complex(it, 0.0)).ToArray();

        Fourier.Forward(values, FourierOptions.Default);

        double[] magnitudes = values.Select(it => it.Magnitude).ToArray();

        const int slicesCount = 3;

        double[] slices = new double[slicesCount];

        for (int i = 0; i < slicesCount; i++)
        {
            for (int j = len / slicesCount * i; j < len / slicesCount * (i + 1); j++)
            {
                slices[i] += magnitudes[j];
            }
        }

        lock (_colorLock)
        {
            _r = slices[0];
            _g = slices[1];
            _b = slices[2];
        }

        // Console.WriteLine($"{r} {g} {b}");
    }

    protected override int StabilizeFps() => 300;
}

internal class StraightBuggedBeautifulFurrierMic : AbstractEffect
{
    private readonly Color[] _colorsBuffer;
    private readonly object _colorLock = new();

    private WasapiCapture? _capture;
    private double _r = 0;
    private double _g = 0;
    private double _b = 0;


    public StraightBuggedBeautifulFurrierMic(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = new Color[attachedLedLine.LedsCount];
        BeginCapture();
    }

    protected override void MoveNext()
    {
        _colorsBuffer.Shift();

        lock (_colorLock)
        {
            double max = Math.Max(_r, Math.Max(_g, _b));
            if (Math.Abs(max) < 0.01) max = 1;

            Color c = Color.RGB(_r / max * 255, _g / max * 255, _b / max * 255);
            for (int i = 0; i < 1; i++)
            {
                _colorsBuffer[i] = Color.HSV(c.Saturation * 360, c.Value, c.Hue / 360);
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override void ClearResources() => _capture?.StopRecording();

    private void BeginCapture()
    {
        _capture = new WasapiCapture();

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

        int len = e.BytesRecorded / 4;

        Complex[] values = buffer.FloatBuffer
            .Take(len)
            .Select(it => new Complex(it, 0.0)).ToArray();

        Fourier.Forward(values, FourierOptions.Default);

        double[] magnitudes = values.Select(it => it.Magnitude).ToArray();

        const int slicesCount = 6;

        double[] slices = new double[slicesCount];

        for (int i = 0; i < slicesCount; i++)
        {
            for (int j = len / slicesCount * i; j < len / slicesCount * (i + 1); j++)
            {
                slices[i] += magnitudes[j];
            }
        }

        lock (_colorLock)
        {
            _r = slices[0];
            _g = slices[1] + slices[2] + slices[3] + slices[4];
            _b = slices[5];
        }

        Console.WriteLine($"{_r} {_g} {_b}");
    }

    protected override int StabilizeFps() => 1000;
}
