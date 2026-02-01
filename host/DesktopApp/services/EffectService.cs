using Leds.core;
using Leds.effects;
using Leds.effects._1d;
using Leds.effects._2d;
using Leds.effects.tests;

namespace Leds.services;

public class EffectService
{
    private AbstractEffect? _currentEffect;
    private LedLine? _ledLine;

    public bool IsConnected => _ledLine != null;
    public bool IsEffectRunning => _currentEffect != null;

    public static List<EffectInfo> GetAvailableEffects()
    {
        return
        [
            new EffectInfo("Coordinate System Test", line => new CoordinateSystemTest(line)),
            new EffectInfo("Max fps test", line => new MaxFpsTest(line)),
            new EffectInfo("Beautiful bugged fft", line => new BuggedBeautifulFurrier(line)),
            new EffectInfo("Beautiful bugged fft. One line", line => new StraightBuggedBeautifulFurrier(line)),
            new EffectInfo("Beautiful bugged fft. One line MIC", line => new StraightBuggedBeautifulFurrierMic(line)),
            new EffectInfo("Tests fft", line => new FurrierTests(line)),
            new EffectInfo("Rainbow", line => new Rainbow(line)),
            new EffectInfo("Rain", line => new Rain(line)),
            new EffectInfo("Tree", line => new Tree(line)),
            new EffectInfo("Tests", line => new Tests(line)),
            new EffectInfo("Rainbow wave", line => new RainbowWave(line)),
            new EffectInfo("Sparcles", line => new Sparkles(line)),
            new EffectInfo("Comet", line => new Comet(line)),
            new EffectInfo("Fireworks 1D", line => new Fireworks1D(line)),
            new EffectInfo("Fireworks", line => new Fireworks(line)),
            new EffectInfo("Color pulse", line => new ColorPulse(line)),
            new EffectInfo("Wave ripple", line => new WaveRipple(line)),
            new EffectInfo("Twinkle stars", line => new TwinkleStars(line)),
            new EffectInfo("Equalizer bars", line => new EqualizerBars(line)),
            new EffectInfo("SpectrumWaves", line => new SpectrumWaves(line)),
            new EffectInfo("DynamicPulse", line => new DynamicPulse(line)),
            new EffectInfo("GameOfLife1D", line => new GameOfLife1D(line)),
            new EffectInfo("Game of Life", line => new GameOfLife(line)),
            new EffectInfo("Snake Game", line => new SnakeGame(line)),
            new EffectInfo("Rotating Cube", line => new RotatingCube(line)),
            new EffectInfo("Julia Set Zoom", line => new JuliaSetZoom(line)),
            new EffectInfo("Flashing Letters Text", line => new FlashingLettersText(line)),
            new EffectInfo("Self Playing Snake", line => new SelfPlayingSnake(line)),
        ];
    }

    public void ConnectToDevice(string ipAddress, int brightness = 100)
    {
        DisconnectFromDevice();
        _ledLine = new LedLine(ipAddress, brightness: (byte)brightness);
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
}

public class EffectInfo(string name, Func<LedLine, AbstractEffect> factory)
{
    public string Name { get; } = name;
    public Func<LedLine, AbstractEffect> Factory { get; } = factory;
}
