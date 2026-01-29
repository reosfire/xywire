using Leds.core;
using Leds.core.vectors;

namespace Leds.effects._2d;

public class Fireworks : AbstractEffect
{
    private readonly Color[][] _colorsBuffer;
    private readonly List<Particle> _particles = [];

    private readonly Random _random = new();

    public Fireworks(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);
    }

    private int _framesCount = 0;
    private int _framesSinceFirework = 0;

    protected override void MoveNext()
    {
        var fireworkChance = Math.Tan(_framesSinceFirework / 5000.0);
        if (_random.Next(100000) < fireworkChance * 100000)
        {
            var firworksCount = _random.Next(4) + 1;
            for (int i = 0; i < firworksCount; i++)
            {
                var color = Color.HSV(
                    _random.Next(360),
                    0.7 + _random.NextDouble() * 0.29,
                    0.7 + _random.NextDouble() * 0.29
                );
                var position = new Vec2d(_random.NextDouble(), 0.0);
                var centerDirection = (new Vec2d(0.5, 0.5) - position).Normalize() * 0.035;
                var velocity =
                    new Vec2d(0, 0.035 + _random.NextDouble() * 0.01).Lerp(centerDirection, _random.NextDouble() * 0.5);


                _particles.Add(
                    new Particle(
                        ParticleType.Firework,
                        position,
                        velocity,
                        1000,
                        (_random.NextDouble() * 2 - 1) * 0.008,
                        (p) => color
                    )
                );
            }

            _framesSinceFirework = 0;
        }
        else
        {
            _framesSinceFirework++;
        }

        var newParticles = new List<Particle>();

        // Update particles logic
        foreach (var particle in _particles)
        {
            particle.Lifetime--;

            if (particle.Type == ParticleType.Firework &&
                Math.Abs(particle.Velocity.Y - particle.TerminalVelocity) < 0.001)
            {
                int sparkleCount = 20 + _random.Next(30);
                for (int i = 0; i < sparkleCount; i++)
                {
                    double angle = (2 * Math.PI / sparkleCount) * i;
                    double speed = 0.02 + _random.NextDouble() * 0.03;
                    var velocity = new Vec2d(Math.Cos(angle) * speed, Math.Sin(angle) * speed);

                    newParticles.Add(
                        new Particle(
                            ParticleType.Sparkle,
                            particle.Position,
                            velocity,
                            50 + _random.Next(50),
                            -1.0,
                            (p) =>
                            {
                                var fireworkColor = particle.ColorFunction(particle);
                                return Color.RGB(
                                    Math.Max(0, fireworkColor.Red - (int)(255 * (1 - (double)p.Lifetime / (50 + 50)))),
                                    Math.Max(0,
                                        fireworkColor.Green - (int)(255 * (1 - (double)p.Lifetime / (50 + 50)))),
                                    Math.Max(0, fireworkColor.Blue - (int)(255 * (1 - (double)p.Lifetime / (50 + 50))))
                                );
                            })
                    );
                }

                // Remove the firework particle
                particle.Lifetime = 0;
            }
        }

        _particles.AddRange(newParticles);

        _particles.RemoveAll((p) => p.Lifetime <= 0);


        // Update particles physics
        foreach (var particle in _particles)
        {
            particle.Position += particle.Velocity;
            particle.Velocity += new Vec2d(0, -0.001);
            particle.Velocity *= 0.99;
        }

        // Render particles to _colorsBuffer
        double pixelSize = 1.0 / (LedLine.Width - 1);

        for (int y = 0; y < LedLine.Height; y++)
        {
            for (int x = 0; x < LedLine.Width; x++)
            {
                _colorsBuffer[y][x] = Color.RGB(0, 0, 0);

                var pixelPos = new Vec2d((double)x / (LedLine.Width - 1), 1 - (double)y / (LedLine.Height - 1));
                var totalRed = 0.0;
                var totalGreen = 0.0;
                var totalBlue = 0.0;
                var count = 0;

                foreach (var particle in _particles)
                {
                    var dist = (particle.Position - pixelPos).Magnitude();
                    var factor = Math.Exp(-dist / pixelSize * 4.0);

                    var color = particle.ColorFunction(particle);

                    var currentRed = color.Red * factor;
                    var currentGreen = color.Green * factor;
                    var currentBlue = color.Blue * factor;

                    if (currentRed < 1 && currentGreen < 1 && currentBlue < 1) continue;

                    totalRed += color.Red * factor;
                    totalGreen += color.Green * factor;
                    totalBlue += color.Blue * factor;
                    count++;
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

    protected override int StabilizeFps()
    {
        return 60;
    }

    private class Particle(
        ParticleType type,
        Vec2d position,
        Vec2d velocity,
        int lifetime,
        double terminalVelocity,
        Func<Particle, Color> colorFunction)
    {
        public readonly ParticleType Type = type;
        public Vec2d Position = position;
        public Vec2d Velocity = velocity;
        public int Lifetime = lifetime;
        public double TerminalVelocity = terminalVelocity;
        public Func<Particle, Color> ColorFunction = colorFunction;
    }

    private enum ParticleType
    {
        Firework,
        Sparkle,
    }
}