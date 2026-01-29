using System.Numerics;
using Leds.core;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;

namespace Leds.effects._1d
{
    internal class RainbowWave : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private double _hueOffset;

        public RainbowWave(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                double hue = (i * 2 + _hueOffset) % 360; // Smoothly shift hue
                _colorsBuffer[i] = Color.HSV(hue, 1.0, 1.0);
            }

            _hueOffset += 1.0;
            if (_hueOffset > 360) _hueOffset -= 360;

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 60; // 30 FPS for smooth animation
        }
    }

    internal class Sparkles : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private readonly Random _random = new Random();

        public Sparkles(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(value: _colorsBuffer[i].Value * 0.8); // Fade existing sparkles
            }

            int sparkCount = _random.Next(1, 4); // Generate 1-4 new sparkles
            for (int i = 0; i < sparkCount; i++)
            {
                int pos = _random.Next(_colorsBuffer.Length);
                _colorsBuffer[pos] = Color.HSV(_random.Next(360), 1.0, 1.0);
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 20; // Slightly slower for a twinkling effect
        }
    }

    internal class Comet : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private int _headPosition = 0;

        public Comet(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(value: _colorsBuffer[i].Value * 0.8); // Fade existing trail
            }

            _colorsBuffer[_headPosition] = Color.HSV(200, 1.0, 1.0); // Comet head

            _headPosition = (_headPosition + 1) % _colorsBuffer.Length; // Move head

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 60; // Fast comet movement
        }
    }

    internal class DualComet : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private int _headPosition1 = 0;
        private int _headPosition2 = 50; // Start second comet halfway around the strip

        public DualComet(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(value: _colorsBuffer[i].Value * 0.8); // Fade existing trails
            }

            _colorsBuffer[_headPosition1] = Color.HSV(120, 1.0, 1.0); // Green comet
            _colorsBuffer[_headPosition2] = Color.HSV(0, 1.0, 1.0); // Red comet

            _headPosition1 = (_headPosition1 + 1) % _colorsBuffer.Length;
            _headPosition2 = (_headPosition2 + 1) % _colorsBuffer.Length;

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 60; // Fast comet movement
        }
    }

    internal class Fireworks1d : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private readonly Random _random = new Random();

        public Fireworks1d(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(value: _colorsBuffer[i].Value * 0.9); // Fade existing bursts
            }

            if (_random.NextDouble() < 0.1) // Occasionally trigger a new firework
            {
                int center = _random.Next(_colorsBuffer.Length);
                double hue = _random.Next(360);
                for (int i = -5; i <= 5; i++) // Burst effect
                {
                    int pos = (center + i + _colorsBuffer.Length) % _colorsBuffer.Length;
                    double distance = Math.Abs(i) / 5.0;
                    _colorsBuffer[pos] = Color.HSV(hue, 1.0, 1.0 * Math.Pow(1 - distance, 2));
                }
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 30; // Smooth but dramatic effect
        }
    }

    internal class ColorPulse : AbstractEffect
    {
        private double _hue = 10;
        private double _saturation = 0.7;
        private Random _random = new Random();

        private readonly Color[] _colorsBuffer;
        private double _pulsePosition = 0;
        private bool _forward = true;

        public ColorPulse(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                double distance = Math.Abs(i / (double)_colorsBuffer.Length - _pulsePosition);
                _colorsBuffer[i] = Color.HSV(_hue, _saturation, Math.Max(0, 1 - distance * 10)); // Purple pulse
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

        protected override int StabilizeFps()
        {
            return 500; // Smooth animation
        }
    }

    internal class WaveRipple : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private double _waveCenter;

        public WaveRipple(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                double distance = Math.Abs(i / (double)_colorsBuffer.Length - _waveCenter);
                double brightness = Math.Max(0, 1 - distance * 10);
                _colorsBuffer[i] = Color.HSV(240, 1.0, brightness); // Blue ripple
            }

            _waveCenter += 0.02;
            if (_waveCenter > 1.0) _waveCenter -= 1.0;

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 50; // Smooth propagation
        }
    }

    internal class TwinkleStars : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private readonly Random _random = new Random();

        public TwinkleStars(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
        }

        protected override void MoveNext()
        {
            for (int i = 0; i < _colorsBuffer.Length; i++)
            {
                if (_random.NextDouble() < 0.05) // 10% chance of twinkling
                {
                    if (_colorsBuffer[i].Value > 0.01) continue;
                    double hue = _random.Next(360);
                    double saturation = _random.NextDouble() % 0.2 + 0.8;
                    _colorsBuffer[i] = Color.HSV(hue, saturation, 1.0);
                }
                else
                {
                    _colorsBuffer[i] = _colorsBuffer[i].CopyHSV(saturation: _colorsBuffer[i].Saturation * 0.998, value: _colorsBuffer[i].Value * 0.995); // Fade out
                }
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 60; // Gentle twinkling
        }
    }

    internal class EqualizerBars : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private WasapiLoopbackCapture? _capture;
        private readonly object _colorLock = new();
        private double[] _bandAmplitudes = new double[10];

        public EqualizerBars(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
            BeginCapture();
        }

        protected override void MoveNext()
        {
            lock (_colorLock)
            {
                // Map the amplitude of each band to LED brightness
                for (int i = 0; i < _colorsBuffer.Length; i++)
                {
                    int bandIndex = i / (_colorsBuffer.Length / _bandAmplitudes.Length);
                    double amplitude = Math.Min(1.0, _bandAmplitudes[bandIndex]);

                    _colorsBuffer[i] = Color.HSV((bandIndex * 36) % 360, 1.0, amplitude);
                }
            }

            LedLine.SetColors(_colorsBuffer);
        }

        private void BeginCapture()
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += PCMAvailable;
            _capture.RecordingStopped += (s, a) => _capture.Dispose();
            _capture.StartRecording();
        }

        private void PCMAvailable(object? sender, WaveInEventArgs e)
        {
            WaveBuffer buffer = new WaveBuffer(e.Buffer);
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

        protected override void ClearResources()
        {
            _capture?.StopRecording();
        }

        protected override int StabilizeFps()
        {
            return 30; // Smooth animation
        }
    }


    internal class SpectrumWaves : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private WasapiLoopbackCapture? _capture;
        private readonly object _colorLock = new();
        private double[] _frequencyBands = new double[100];

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
                    double hue = (i / (double)_colorsBuffer.Length) * 360;

                    _colorsBuffer[i] = Color.HSV(hue, 1.0, amplitude);
                }
            }

            LedLine.SetColors(_colorsBuffer);
        }

        private void BeginCapture()
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += PCMAvailable;
            _capture.RecordingStopped += (s, a) => _capture.Dispose();
            _capture.StartRecording();
        }

        private void PCMAvailable(object? sender, WaveInEventArgs e)
        {
            WaveBuffer buffer = new WaveBuffer(e.Buffer);
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
                    int startIdx = (i * magnitudes.Length) / _frequencyBands.Length;
                    int endIdx = ((i + 1) * magnitudes.Length) / _frequencyBands.Length;

                    _frequencyBands[i] = magnitudes.Skip(startIdx).Take(endIdx - startIdx).Average();
                }
            }
        }

        protected override void ClearResources()
        {
            _capture?.StopRecording();
        }

        protected override int StabilizeFps()
        {
            return 30; // Smooth animation
        }
    }

    internal class DynamicPulse : AbstractEffect
    {
        private readonly Color[] _colorsBuffer;
        private WasapiLoopbackCapture? _capture;
        private readonly object _colorLock = new();
        private double _currentAmplitude = 0;

        public DynamicPulse(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _colorsBuffer = new Color[attachedLedLine.LedsCount];
            BeginCapture();
        }

        protected override void MoveNext()
        {
            lock (_colorLock)
            {
                double hue = (_currentAmplitude * 360) % 360;
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
            _capture.DataAvailable += PCMAvailable;
            _capture.RecordingStopped += (s, a) => _capture.Dispose();
            _capture.StartRecording();
        }

        private void PCMAvailable(object? sender, WaveInEventArgs e)
        {
            WaveBuffer buffer = new WaveBuffer(e.Buffer);
            int len = e.BytesRecorded / 4;

            Complex[] values = buffer.FloatBuffer
                .Take(len)
                .Select(it => new Complex(it, 0.0))
                .ToArray();

            Fourier.Forward(values, FourierOptions.Default);

            double[] magnitudes = values.Select(it => it.Magnitude).ToArray();
            lock (_colorLock)
            {
                _currentAmplitude = magnitudes.Average(); // Average amplitude of all frequencies
            }
        }

        protected override void ClearResources()
        {
            _capture?.StopRecording();
        }

        protected override int StabilizeFps()
        {
            return 50; // Reactive to beats
        }
    }

    internal class GameOfLife1D : AbstractEffect
    {
        private bool[] _currentState;
        private readonly Color[] _colorsBuffer;
        private readonly Random _random = new Random();

        public GameOfLife1D(LedLine attachedLedLine) : base(attachedLedLine)
        {
            _currentState = new bool[attachedLedLine.LedsCount];
            _colorsBuffer = new Color[attachedLedLine.LedsCount];

            // Initialize with a random state
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
                // Calculate neighbors, wrapping around the ends
                int left2 = (i - 2 + _currentState.Length) % _currentState.Length;
                int left1 = (i - 1 + _currentState.Length) % _currentState.Length;
                int right1 = (i + 1) % _currentState.Length;
                int right2 = (i + 2) % _currentState.Length;

                int aliveNeighbors = (_currentState[left2] ? 1 : 0) +
                                     (_currentState[left1] ? 1 : 0) +
                                     (_currentState[right1] ? 1 : 0) +
                                     (_currentState[right2] ? 1 : 0);

                // Apply custom rules
                if (_currentState[i])
                {
                    // Survival rule: A cell survives if it has 2 or 4 alive neighbors
                    nextState[i] = aliveNeighbors == 2 || aliveNeighbors == 4;
                }
                else
                {
                    // Birth rule: A cell is born if it has 2 or 3 alive neighbors
                    nextState[i] = aliveNeighbors == 2 || aliveNeighbors == 3;
                }
            }

            nextState[_random.Next(100)] = true;

            // Update the current state
            _currentState = nextState;

            // Update the colors based on the new state
            for (int i = 0; i < _currentState.Length; i++)
            {
                _colorsBuffer[i] = _currentState[i]
                    ? Color.HSV(240, 1.0, 1.0) // Alive cells are bright blue
                    : Color.HSV(0, 0, 0.1);   // Dead cells are dim
            }

            LedLine.SetColors(_colorsBuffer);
        }

        protected override int StabilizeFps()
        {
            return 2; // Update at 10 FPS for smooth animation
        }
    }
}

