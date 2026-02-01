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
            new EffectInfo("Self Playing Snake", line => new SelfPlayingSnake(line)),
            new EffectInfo("Rotating Cube", line => new RotatingCube(line)),
            new EffectInfo("Beautiful bugged fft", line => new BuggedBeautifulFurrier(line)),
            new EffectInfo("Fireworks", line => new Fireworks(line)),
            new EffectInfo("Game of Life", line => new GameOfLife(line)),
            new EffectInfo("Flashing Letters Text", line => new FlashingLettersText(line)),
            new EffectInfo("Rain", line => new Rain(line)),
            new EffectInfo("Rainbow", line => new Rainbow(line)),
            new EffectInfo("GameOfLife1D", line => new GameOfLife1D(line)),
            new EffectInfo("Rainbow 1D", line => new Rainbow1D(line)),
            new EffectInfo("Fireworks 1D", line => new Fireworks1D(line)),
            new EffectInfo("Twinkle stars 1D", line => new TwinkleStars(line)),
            new EffectInfo("Moving accent 1D", line => new MovingAccent(line)),
            new EffectInfo("Color pulse 1D", line => new ColorPulse(line)),
            
            // TODO: Need huge fixing to be usable
            new EffectInfo("Snake Game", line => new SnakeGame(line)),
            new EffectInfo("Julia Set Zoom", line => new JuliaSetZoom(line)),
            new EffectInfo("Tree", line => new Tree(line)),
            new EffectInfo("Beautiful bugged fft. 1D", line => new StraightBuggedBeautifulFurrier(line)),
            new EffectInfo("Beautiful bugged fft. MIC 1D", line => new StraightBuggedBeautifulFurrierMic(line)),
            new EffectInfo("Tests fft", line => new FurrierTests(line)),
            // Testing effects
            new EffectInfo("Coordinate System Test", line => new CoordinateSystemTest(line)),
        ];
    }

    public async Task ConnectToDevice(string ipAddress)
    {
        DisconnectFromDevice();
        _ledLine = new LedLine(ipAddress);
        await _ledLine.SendClearPacket();
    }

    public void DisconnectFromDevice()
    {
        _currentEffect?.StopLooping();
        _currentEffect = null;
        
        _ledLine?.Dispose();
        _ledLine = null;
    }

    public async Task StartEffect(EffectInfo effectInfo)
    {
        if (_ledLine == null)
            throw new InvalidOperationException("No device connected");

        await StopCurrentEffect();
        _currentEffect = effectInfo.Factory(_ledLine);
        _currentEffect.StartLooping();
    }

    public async Task StopCurrentEffect()
    {
        _currentEffect?.StopLooping();
        Task? clearTask = _ledLine?.SendClearPacket();
        if (clearTask != null) await clearTask;
        _currentEffect = null;
    }

    public Task? SetBrightness(byte brightness)
    {
        return _ledLine?.SendBrightnessPacket(brightness);
    }
}

public class EffectInfo(string name, Func<LedLine, AbstractEffect> factory)
{
    public string Name { get; } = name;
    public Func<LedLine, AbstractEffect> Factory { get; } = factory;
}
