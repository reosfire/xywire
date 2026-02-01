namespace Leds.core;

public class EffectFactory(string name, Func<LedLine, AbstractEffect> factory)
{
    public string Name { get; private set; } = name;
    public Func<LedLine, AbstractEffect> Factory { get; private set; } = factory;
}
