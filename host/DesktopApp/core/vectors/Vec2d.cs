namespace Leds.core.vectors;

public struct Vec2d
{
    public readonly double X, Y;

    public Vec2d(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vec2d operator +(Vec2d a, Vec2d b) => new(a.X + b.X, a.Y + b.Y);

    public static Vec2d operator -(Vec2d a, Vec2d b) => new(a.X - b.X, a.Y - b.Y);

    public static Vec2d operator *(Vec2d a, double scalar) => new(a.X * scalar, a.Y * scalar);

    public static Vec2d operator /(Vec2d a, double scalar) => new(a.X / scalar, a.Y / scalar);

    public double Magnitude() => Math.Sqrt(X * X + Y * Y);

    public Vec2d Normalize()
    {
        double magnitude = Magnitude();
        return new Vec2d(X / magnitude, Y / magnitude);
    }

    public Vec2d Lerp(Vec2d other, double amount) => new(X + (other.X - X) * amount, Y + (other.Y - Y) * amount);
}
