using Leds.core;
using Leds.effects;
using Leds.effects._1d;
using Leds.effects._2d;
using Leds.effects.tests;

namespace Leds.services;

public class EffectService
{
    private LedLine? _ledLine;
    private AbstractEffect? _currentEffect;

    public List<EffectInfo> GetAvailableEffects()
    {
        return new List<EffectInfo>
        {
            new("Coordinate System Test", (line) => new CoordinateSystemTest(line)),
            new("Max fps test", (line) => new MaxFpsTest(line)),
            new("Beautiful bugged fft", (line) => new BuggedBeautifulFurrier(line)),
            new("Beautiful bugged fft. One line", (line) => new StraightBuggedBeautifulFurrier(line)),
            new("Beautiful bugged fft. One line MIC", (line) => new StraightBuggedBeautifulFurrierMic(line)),
            new("Tests fft", (line) => new FurrierTests(line)),
            new("Rainbow", (line) => new Rainbow(line)),
            new("Rain", (line) => new Rain(line)),
            new("Tree", (line) => new Tree(line)),
            new("Tests", (line) => new Tests(line)),
            new("Rainbow wave", (line) => new RainbowWave(line)),
            new("Sparcles", (line) => new Sparkles(line)),
            new("Comet", (line) => new Comet(line)),
            new("Dual Comet", (line) => new DualComet(line)),
            new("Fireworks", (line) => new Fireworks(line)),
            new("Color pulse", (line) => new ColorPulse(line)),
            new("Wave ripple", (line) => new WaveRipple(line)),
            new("Twinkle stars", (line) => new TwinkleStars(line)),
            new("Equalizer bars", (line) => new EqualizerBars(line)),
            new("SpectrumWaves", (line) => new SpectrumWaves(line)),
            new("DynamicPulse", (line) => new DynamicPulse(line)),
            new("GameOfLife1D", (line) => new GameOfLife1D(line)),
            new("Game of Life", (line) => new GameOfLife(line)),
            new("Snake Game", (line) => new SnakeGame(line)),
            new("Rotating Cube", (line) => new RotatingCube(line)),
            new("Julia Set Zoom", (line) => new JuliaSetZoom(line)),
            new("Flashing Letters Text", (line) => new FlashingLettersText(line)),
            new("Self Playing Snake", (line) => new SelfPlayingSnake(line)),
        };
    }

    public void ConnectToDevice(string ipAddress, int brightness = 100)
    {
        DisconnectFromDevice();
        _ledLine = new LedLine(ipAddress: ipAddress, brightness: (byte)brightness);
    }

    public void DisconnectFromDevice()
    {
        StopCurrentEffect();
        _ledLine = null;
    }

    public void StartEffect(EffectInfo effectInfo)
    {
        if (_ledLine == null)
            throw new InvalidOperationException("No device connected");

        StopCurrentEffect();
        _currentEffect = effectInfo.Factory(_ledLine);
        _currentEffect.StartLooping();
    }

    public void StopCurrentEffect()
    {
        _currentEffect?.StopLooping();
        _currentEffect = null;
    }

    public void SetBrightness(int brightness)
    {
        if (_ledLine != null)
        {
            _ledLine.Brightness = (byte)brightness;
        }
    }

    public bool IsConnected => _ledLine != null;
    public bool IsEffectRunning => _currentEffect != null;
}

public class EffectInfo
{
    public string Name { get; }
    public Func<LedLine, AbstractEffect> Factory { get; }

    public EffectInfo(string name, Func<LedLine, AbstractEffect> factory)
    {
        Name = name;
        Factory = factory;
    }
}
