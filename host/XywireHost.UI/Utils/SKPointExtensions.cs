using SkiaSharp;

namespace XywireHost.UI.Utils;

public static class SKPointExtensions
{
    extension(SKPoint)
    {
        public static SKPoint operator /(SKPoint point, float factor) =>
            new(point.X / factor, point.Y / factor);
    }
}
