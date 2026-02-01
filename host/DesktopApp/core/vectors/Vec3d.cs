namespace Leds.core.vectors;

public readonly struct Vec3d
{
    public readonly double X, Y, Z;

    public Vec3d(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3d operator *(Vec3d v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vec3d operator *(double s, Vec3d v) => new(v.X * s, v.Y * s, v.Z * s);

    public double Dot(Vec3d other) => X * other.X + Y * other.Y + Z * other.Z;

    public Vec3d Cross(Vec3d other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X
    );

    public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vec3d Normalize()
    {
        double len = Length();
        return len > 0 ? new Vec3d(X / len, Y / len, Z / len) : new Vec3d(0, 0, 0);
    }
}
