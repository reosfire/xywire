using System.Numerics;
using Leds.core;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;

namespace Leds.effects._1d;

internal class RainbowWave(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private double _hueOffset;

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            double hue = (i * 2 + _hueOffset) % 360;
            _colorsBuffer[i] = Color.HSV(hue, 1.0, 1.0);
        }

        _hueOffset += 1.0;
        if (_hueOffset > 360) _hueOffset -= 360;

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;
}

internal class Sparkles(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private readonly Random _random = new();

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(value: _colorsBuffer[i].Value * 0.8);
        }

        int sparkCount = _random.Next(1, 4);
        for (int i = 0; i < sparkCount; i++)
        {
            int pos = _random.Next(_colorsBuffer.Length);
            _colorsBuffer[pos] = Color.HSV(_random.Next(360), 1.0, 1.0);
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 20;
}

internal class Comet(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private int _headPosition;

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(value: _colorsBuffer[i].Value * 0.8);
        }

        _colorsBuffer[_headPosition] = Color.HSV(200, 1.0, 1.0);

        _headPosition = (_headPosition + 1) % _colorsBuffer.Length;

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;
}

internal class Fireworks1D(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private readonly Random _random = new();

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(value: _colorsBuffer[i].Value * 0.9);
        }

        if (_random.NextDouble() < 0.1)
        {
            int center = _random.Next(_colorsBuffer.Length);
            double hue = _random.Next(360);
            for (int i = -5; i <= 5; i++)
            {
                int pos = (center + i + _colorsBuffer.Length) % _colorsBuffer.Length;
                double distance = Math.Abs(i) / 5.0;
                _colorsBuffer[pos] = Color.HSV(hue, 1.0, 1.0 * Math.Pow(1 - distance, 2));
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 30;
}

internal class ColorPulse(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private readonly Random _random = new();
    private double _hue = 10;
    private double _saturation = 0.7;
    private double _pulsePosition;
    private bool _forward = true;

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            double distance = Math.Abs(i / (double)_colorsBuffer.Length - _pulsePosition);
            _colorsBuffer[i] = Color.HSV(_hue, _saturation, Math.Max(0, 1 - distance * 10));
        }

        if (_forward)
        {
            _pulsePosition += 0.01;
            if (_pulsePosition >= 1.0)
            {
                _forward = false;
                _hue = _random.NextDouble() * 360;
                _saturation = _random.NextDouble() * 0.3 + 0.7;
            }
        }
        else
        {
            _pulsePosition -= 0.01;
            if (_pulsePosition <= 0.0)
            {
                _forward = true;
                _hue = _random.NextDouble() * 360;
                _saturation = _random.NextDouble() * 0.3 + 0.7;
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 500;
}

internal class WaveRipple(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private double _waveCenter;

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            double distance = Math.Abs(i / (double)_colorsBuffer.Length - _waveCenter);
            double brightness = Math.Max(0, 1 - distance * 10);
            _colorsBuffer[i] = Color.HSV(240, 1.0, brightness);
        }

        _waveCenter += 0.02;
        if (_waveCenter > 1.0) _waveCenter -= 1.0;

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 50;
}

internal class TwinkleStars(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[] _colorsBuffer = new Color[attachedLedLine.LedsCount];
    private readonly Random _random = new();

    protected override void MoveNext()
    {
        for (int i = 0; i < _colorsBuffer.Length; i++)
        {
            if (_random.NextDouble() < 0.05)
            {
                if (_colorsBuffer[i].Value > 0.01) continue;
                double hue = _random.Next(360);
                double saturation = _random.NextDouble() % 0.2 + 0.8;
                _colorsBuffer[i] = Color.HSV(hue, saturation, 1.0);
            }
            else
            {
                _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(saturation: _colorsBuffer[i].Saturation * 0.998,
                    value: _colorsBuffer[i].Value * 0.995);
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;
}

internal class EqualizerBars : AbstractEffect
{
    private readonly object _colorLock = new();
    private readonly double[] _bandAmplitudes = new double[10];
    private readonly Color[] _colorsBuffer;
    private WasapiLoopbackCapture? _capture;

    public EqualizerBars(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = new Color[attachedLedLine.LedsCount];
        BeginCapture();
    }

    protected override void MoveNext()
    {
        lock (_colorLock)
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                int bandIndex = i / (_colorsBuffer.Length / _bandAmplitudes.Length);
                double amplitude = Math.Min(1.0, _bandAmplitudes[bandIndex]);

                _colorsBuffer[i] = Color.HSV(bandIndex * 36 % 360, 1.0, amplitude);
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    private void BeginCapture()
    {
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += PcmAvailable;
        _capture.RecordingStopped += (_, _) => _capture.Dispose();
        _capture.StartRecording();
    }

    private void PcmAvailable(object? sender, WaveInEventArgs e)
    {
        WaveBuffer buffer = new(e.Buffer);
        int len = e.BytesRecorded / 4;

        Complex[] values = buffer.FloatBuffer
            .Take(len)
            .Select(it => new Complex(it, 0.0))
            .ToArray();

        Fourier.Forward(values, FourierOptions.Default);

        double[] magnitudes = values.Select(it => it.Magnitude).ToArray();

        lock (_colorLock)
        {
            for (int i = 0; i < _bandAmplitudes.Length; i++)
            {
                _bandAmplitudes[i] = magnitudes.Skip(i * (magnitudes.Length / _bandAmplitudes.Length))
                    .Take(magnitudes.Length / _bandAmplitudes.Length)
                    .Average();
            }
        }
    }

    protected override void ClearResources() => _capture?.StopRecording();

    protected override int StabilizeFps() => 30;
}

internal class SpectrumWaves : AbstractEffect
{
    private readonly object _colorLock = new();
    private readonly Color[] _colorsBuffer;
    private readonly double[] _frequencyBands = new double[100];
    private WasapiLoopbackCapture? _capture;

    public SpectrumWaves(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = new Color[attachedLedLine.LedsCount];
        BeginCapture();
    }

    protected override void MoveNext()
    {
        lock (_colorLock)
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                double amplitude = Math.Min(1.0, _frequencyBands[i]);
                double hue = i / (double)_colorsBuffer.Length * 360;

                _colorsBuffer[i] = Color.HSV(hue, 1.0, amplitude);
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    private void BeginCapture()
    {
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += PcmAvailable;
        _capture.RecordingStopped += (_, _) => _capture.Dispose();
        _capture.StartRecording();
    }

    private void PcmAvailable(object? sender, WaveInEventArgs e)
    {
        WaveBuffer buffer = new(e.Buffer);
        int len = e.BytesRecorded / 4;

        Complex[] values = buffer.FloatBuffer
            .Take(len)
            .Select(it => new Complex(it, 0.0))
            .ToArray();

        Fourier.Forward(values, FourierOptions.Default);

        double[] magnitudes = values.Select(it => it.Magnitude).ToArray();

        lock (_colorLock)
        {
            for (int i = 0; i < _frequencyBands.Length; i++)
            {
                int startIdx = i * magnitudes.Length / _frequencyBands.Length;
                int endIdx = (i + 1) * magnitudes.Length / _frequencyBands.Length;

                _frequencyBands[i] = magnitudes.Skip(startIdx).Take(endIdx - startIdx).Average();
            }
        }
    }

    protected override void ClearResources() => _capture?.StopRecording();

    protected override int StabilizeFps() => 30;
}

internal class DynamicPulse : AbstractEffect
{
    private readonly object _colorLock = new();
    private readonly Color[] _colorsBuffer;
    private WasapiLoopbackCapture? _capture;
    private double _currentAmplitude;

    public DynamicPulse(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = new Color[attachedLedLine.LedsCount];
        BeginCapture();
    }

    protected override void MoveNext()
    {
        lock (_colorLock)
        {
            double hue = _currentAmplitude * 360 % 360;
            double brightness = Math.Min(1.0, _currentAmplitude);

            Color c = Color.HSV(hue, 1.0, brightness);
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                _colorsBuffer[i] = c;
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    private void BeginCapture()
    {
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += PcmAvailable;
        _capture.RecordingStopped += (_, _) => _capture.Dispose();
        _capture.StartRecording();
    }

    private void PcmAvailable(object? sender, WaveInEventArgs e)
    {
        WaveBuffer buffer = new(e.Buffer);
        int len = e.BytesRecorded / 4;

        Complex[] values = buffer.FloatBuffer
            .Take(len)
            .Select(it => new Complex(it, 0.0))
            .ToArray();

        Fourier.Forward(values, FourierOptions.Default);

        double[] magnitudes = values.Select(it => it.Magnitude).ToArray();
        lock (_colorLock)
        {
            _currentAmplitude = magnitudes.Average();
        }
    }

    protected override void ClearResources() => _capture?.StopRecording();

    protected override int StabilizeFps() => 50;
}

internal class GameOfLife1D : AbstractEffect
{
    private readonly Color[] _colorsBuffer;
    private readonly Random _random = new();
    private bool[] _currentState;

    public GameOfLife1D(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _currentState = new bool[attachedLedLine.LedsCount];
        _colorsBuffer = new Color[attachedLedLine.LedsCount];

        for (int i = 0; i < _currentState.Length; i++)
        {
            _currentState[i] = _random.NextDouble() > 0.5;
        }
    }

    protected override void MoveNext()
    {
        bool[] nextState = new bool[_currentState.Length];

        for (int i = 0; i < _currentState.Length; i++)
        {
            int left2 = (i - 2 + _currentState.Length) % _currentState.Length;
            int left1 = (i - 1 + _currentState.Length) % _currentState.Length;
            int right1 = (i + 1) % _currentState.Length;
            int right2 = (i + 2) % _currentState.Length;

            int aliveNeighbors = (_currentState[left2] ? 1 : 0) +
                                 (_currentState[left1] ? 1 : 0) +
                                 (_currentState[right1] ? 1 : 0) +
                                 (_currentState[right2] ? 1 : 0);

            if (_currentState[i])
            {
                nextState[i] = aliveNeighbors is 2 or 4;
            }
            else
            {
                nextState[i] = aliveNeighbors is 2 or 3;
            }
        }

        nextState[_random.Next(100)] = true;
        _currentState = nextState;

        // Update the colors based on the new state
        for (int i = 0; i < _currentState.Length; i++)
        {
            _colorsBuffer[i] = _currentState[i]
                ? Color.HSV(240, 1.0, 1.0)
                : Color.HSV(0, 0, 0.1);
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 2;
}
