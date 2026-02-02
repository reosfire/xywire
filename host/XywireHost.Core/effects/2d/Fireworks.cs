using XywireHost.Core.core;
using XywireHost.Core.core.vectors;

namespace XywireHost.Core.effects._2d;

public class Fireworks(LedLine attachedLedLine) : AbstractEffect(attachedLedLine)
{
    private readonly Color[][] _colorsBuffer =
        Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);

    private readonly List<Particle> _particles = [];
    private readonly Random _random = new();

    private int _framesSinceFirework;

    protected override void MoveNext()
    {
        // Generate new fireworks
        double fireworkChance = Math.Tan(_framesSinceFirework / 5000.0);
        if (_random.Next(100000) < fireworkChance * 100000)
        {
            int fireworksCount = _random.Next(4) + 1;
            for (int i = 0; i < fireworksCount; i++)
            {
                Color color = Color.HSV(
                    _random.Next(360),
                    0.7 + _random.NextDouble() * 0.29,
                    0.7 + _random.NextDouble() * 0.29
                );
                Vec2d position = new(_random.NextDouble(), 0.0);
                Vec2d centerDirection = (new Vec2d(0.5, 0.5) - position).Normalize() * 0.035;
                Vec2d velocity = new Vec2d(0, 0.035 + _random.NextDouble() * 0.01)
                    .Lerp(centerDirection, _random.NextDouble() * 0.5);


                _particles.Add(
                    new Particle(
                        ParticleType.Firework,
                        position,
                        velocity,
                        1000,
                        (_random.NextDouble() * 2 - 1) * 0.008,
                        _ => color
                    )
                );
            }

            _framesSinceFirework = 0;
        }
        else
        {
            _framesSinceFirework++;
        }

        // Explode fireworks and generate new particles
        List<Particle> newParticles = [];
        foreach (Particle particle in _particles)
        {
            particle.Lifetime--;
            if (particle.Type != ParticleType.Firework) continue;
            // Explode only when reaching terminal velocity
            if (Math.Abs(particle.Velocity.Y - particle.TerminalVelocity) >= 0.001) continue;

            int sparkleCount = 20 + _random.Next(30);
            for (int i = 0; i < sparkleCount; i++)
            {
                double angle = 2 * Math.PI / sparkleCount * i;
                double speed = 0.02 + _random.NextDouble() * 0.03;
                Vec2d velocity = new(Math.Cos(angle) * speed, Math.Sin(angle) * speed);

                newParticles.Add(
                    new Particle(
                        ParticleType.Sparkle,
                        particle.Position,
                        velocity,
                        50 + _random.Next(50),
                        -1.0,
                        p =>
                        {
                            Color fireworkColor = particle.ColorFunction(particle);
                            return Color.RGB(
                                Math.Max(0, fireworkColor.Red - (int)(255 * (1 - (double)p.Lifetime / (50 + 50)))),
                                Math.Max(0,
                                    fireworkColor.Green - (int)(255 * (1 - (double)p.Lifetime / (50 + 50)))),
                                Math.Max(0, fireworkColor.Blue - (int)(255 * (1 - (double)p.Lifetime / (50 + 50))))
                            );
                        }
                    )
                );
            }

            particle.Lifetime = 0;
        }

        _particles.AddRange(newParticles);

        _particles.RemoveAll(p => p.Lifetime <= 0);


        // Update particles physics
        foreach (Particle particle in _particles)
        {
            particle.Position += particle.Velocity;
            particle.Velocity += new Vec2d(0, -0.001);
            particle.Velocity *= 0.99;
        }

        double pixelSize = 1.0 / (LedLine.Width - 1);

        for (int y = 0; y < LedLine.Height; y++)
        {
            for (int x = 0; x < LedLine.Width; x++)
            {
                _colorsBuffer[y][x] = Color.RGB(0, 0, 0);

                Vec2d pixelPos = new((double)x / (LedLine.Width - 1), 1 - (double)y / (LedLine.Height - 1));
                double totalRed = 0.0;
                double totalGreen = 0.0;
                double totalBlue = 0.0;

                foreach (Particle particle in _particles)
                {
                    double dist = (particle.Position - pixelPos).Magnitude();
                    double factor = Math.Exp(-dist / pixelSize * 4.0);

                    Color color = particle.ColorFunction(particle);

                    double currentRed = color.Red * factor;
                    double currentGreen = color.Green * factor;
                    double currentBlue = color.Blue * factor;

                    if (currentRed < 1 && currentGreen < 1 && currentBlue < 1) continue;

                    totalRed += color.Red * factor;
                    totalGreen += color.Green * factor;
                    totalBlue += color.Blue * factor;
                }

                _colorsBuffer[y][x] = Color.RGB(
                    Math.Min(255, (int)totalRed),
                    Math.Min(255, (int)totalGreen),
                    Math.Min(255, (int)totalBlue)
                );
            }
        }

        LedLine.SetColors(_colorsBuffer);
    }

    protected override int StabilizeFps() => 60;

    private class Particle(
        ParticleType type,
        Vec2d position,
        Vec2d velocity,
        int lifetime,
        double terminalVelocity,
        Func<Particle, Color> colorFunction)
    {
        public readonly Func<Particle, Color> ColorFunction = colorFunction;
        public readonly double TerminalVelocity = terminalVelocity;
        public readonly ParticleType Type = type;
        public int Lifetime = lifetime;
        public Vec2d Position = position;
        public Vec2d Velocity = velocity;
    }

    private enum ParticleType
    {
        Firework,
        Sparkle,
    }
}
