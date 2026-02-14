using XywireHost.Core.core;

namespace XywireHost.Core.effects._2d;

internal class BeatingHeart(LedLine attachedLedLine) : RaytracedEffect(attachedLedLine, 32)
{
    private int _frame = 0;
    private double _beatScale = 0.0;
    private double _hueOffset = 0.0;

    protected override void MoveNext()
    {
        _frame++;

        double beatTime = (_frame % 60);
        double beatProgress = beatTime / 60.0;
        
        _beatScale = Math.Sin(beatProgress * 6.0 * Math.PI) / Math.Pow(5, beatProgress * 2) * 0.4;
        
        _hueOffset += 1.0;
        if (_hueOffset >= 360.0) _hueOffset -= 360.0;
        
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

    protected override Color RenderPixel(double x, double y)
    {
        double scale = 1.2 + _beatScale;
        double heartX = x / scale;
        double heartY = -y / scale + 0.5 + 1.0 / 14;
        
        double distance = SdHeart(heartX, heartY);
        
        double edgeDist = Math.Abs(distance);
        
        double hue = (_hueOffset + 340.0) % 360.0;
        return Color.HSV(hue, 0.9, Math.Pow(7, -edgeDist * 10.0));
    }

    protected override int StabilizeFps() => 60;
}
