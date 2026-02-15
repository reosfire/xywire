using XywireHost.Core.core;

namespace XywireHost.Core.effects._2d;

internal class BeatingHeart(LedLine attachedLedLine) : RaytracedEffect(attachedLedLine, 8)
{
    private int _frame = 0;
    private double _beatScale = 0.0;
    private double _hueOffset = 0.0;
    private double _angle = 0.0;

    protected override void MoveNext()
    {
        _frame++;

        double beatTime = (_frame % 60);
        double beatProgress = beatTime / 60.0;

        _beatScale = Math.Sin(beatProgress * 6.0 * Math.PI)
                     / Math.Pow(5, beatProgress * 2) * 0.4;

        _hueOffset = (_hueOffset + 1.0) % 360.0;

        _angle = (_frame / 60.0 - 1.0 / 6) * Math.PI / 4;

        base.MoveNext();
    }

    private static double Dot2(double x, double y)
    {
        return x * x + y * y;
    }

    private static double SdHeart(double px, double py)
    {
        px = Math.Abs(px);

        if (py + px > 1.0)
        {
            double dx = px - 0.25;
            double dy = py - 0.75;
            return Math.Sqrt(Dot2(dx, dy)) - Math.Sqrt(2.0) / 4.0;
        }

        double dist1 = Dot2(px, py - 1.00);
        double maxVal = Math.Max(px + py, 0.0);
        double dist2 = Dot2(px - 0.5 * maxVal, py - 0.5 * maxVal);
        double sign = Math.Sign(px - py);

        return Math.Sqrt(Math.Min(dist1, dist2)) * sign;
    }

    // Tube extrusion of 2D heart
    private static double SdHeartTube(double x, double y, double z)
    {
        double d2 = SdHeart(x, y);
        double tubeRadius = 0.08;
        return Math.Sqrt(d2 * d2 + z * z) - tubeRadius;
    }

    private void RotateY(ref double x, ref double z)
    {
        double c = Math.Cos(_angle);
        double s = Math.Sin(_angle);

        double nx = c * x + s * z;
        double nz = -s * x + c * z;

        x = nx;
        z = nz;
    }

    protected override Color RenderPixel(double x, double y)
    {
        double roX = x * 2.0;
        double roY = y * 2.0;
        double roZ = -3.0;

        // All rays parallel
        double rdX = 0.0;
        double rdY = 0.0;
        double rdZ = 1.0;

        double t = 0.0;
        double dist = 0.0;

        for (int i = 0; i < 64; i++)
        {
            double px = roX + rdX * t;
            double py = roY + rdY * t;
            double pz = roZ + rdZ * t;

            double scale = 1.2 + _beatScale;
            scale *= 2.2;

            px /= scale;
            py = (-py / scale) + 0.5 + 1.0 / 14;
            pz /= scale;

            RotateY(ref px, ref pz);

            dist = SdHeartTube(px, py, pz);

            if (Math.Abs(dist) < 0.001)
                break;

            t += dist;

            if (t > 10.0)
                break;
        }

        if (t > 10.0)
            return new Color(0, 0, 0);

        double glow = Math.Exp(-t * 0.4);

        double hue = (_hueOffset + 340.0) % 360.0;

        return Color.HSV(hue, 0.9, glow);
    }

    protected override int StabilizeFps() => 60;
}
